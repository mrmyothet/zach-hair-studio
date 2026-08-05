import { api } from "@/lib/api/client";
import { ApiError, extractErrorMessage, handleUnauthorized } from "@/lib/auth";
import {
  addDays,
  formatSalonTime,
  minutesSinceOpen,
  todayDateOnly,
} from "@/lib/scheduleTime";
import type { components } from "@/lib/api/schema";

type ServiceResponseDto = components["schemas"]["ServiceResponseDto"];
type StylistResponseDto = components["schemas"]["StylistResponseDto"];

export type ChatRole = "user" | "assistant";

export type ChatMessage = {
  id: string;
  role: ChatRole;
  text: string;
};

export const STARTER_PROMPTS: readonly string[] = [
  "Who's booked today?",
  "What's on tomorrow?",
  "List services and prices",
  "Open slots for haircut today",
];

// Module-scoped counter so ids are deterministic (no crypto.randomUUID / Date.now,
// which would risk SSR/hydration id mismatches on a client-only chat surface).
let messageIdCounter = 0;

export function createMessage(role: ChatRole, text: string): ChatMessage {
  messageIdCounter += 1;
  return { id: `msg-${messageIdCounter}`, role, text };
}

export type Intent = "bookings" | "services" | "availability" | "help";

const WEEKDAYS = [
  "sunday",
  "monday",
  "tuesday",
  "wednesday",
  "thursday",
  "friday",
  "saturday",
] as const;

/**
 * Keyword intent routing. Order matters: "open slots for a haircut" mentions a
 * service but is an availability question, so availability is tested first.
 */
export function classifyIntent(text: string): Intent {
  const t = text.toLowerCase();

  // Stems use \w* / s? so plurals ("slots", "services", "prices") still match —
  // a trailing \b after a singular stem rejects its own plural.
  if (/\b(open\w*|free|availab\w*|slots?|vacan\w*)\b/.test(t)) {
    return "availability";
  }
  if (/\b(book\w*|appointments?|schedule|clients?|who'?s|busy|today|tomorrow)\b/.test(t)) {
    return "bookings";
  }
  if (/\b(services?|pric\w*|costs?|how much|menu|offers?)\b/.test(t)) {
    return "services";
  }
  return "help";
}

/**
 * Resolves a salon-local YYYY-MM-DD from loose date language. Defaults to today
 * when nothing matches, so every query lands on a concrete day.
 *
 * `today` is injected for testability — callers omit it.
 */
export function resolveDate(text: string, today: string = todayDateOnly()): string {
  const t = text.toLowerCase();

  const explicit = t.match(/\b(\d{4}-\d{2}-\d{2})\b/);
  if (explicit) return explicit[1];

  // Doubled m / dropped r are the common misspellings (tommorow, tomorow,
  // tommorrow). An unmatched date word silently means "today", so being strict
  // here reads as a wrong answer rather than a typo.
  if (/\btom+or+ow\b/.test(t)) return addDays(today, 1);
  if (/\byester+day\b/.test(t)) return addDays(today, -1);

  // Nearest upcoming occurrence of a named weekday (today counts as itself).
  const named = WEEKDAYS.findIndex((day) => new RegExp(`\\b${day}\\b`).test(t));
  if (named >= 0) {
    // parseDateOnly anchors at noon UTC, so getUTCDay is the salon weekday.
    const todayDow = new Date(`${today}T12:00:00Z`).getUTCDay();
    return addDays(today, (named - todayDow + 7) % 7);
  }

  return today;
}

/** Longest name match wins so "color correction" beats a bare "color". */
export function matchService(
  text: string,
  services: ServiceResponseDto[]
): ServiceResponseDto | undefined {
  const t = text.toLowerCase();
  return services
    .filter((s) => {
      const name = (s.name ?? "").toLowerCase();
      const slugWords = (s.slug ?? "").replace(/-/g, " ").toLowerCase();
      return (name && t.includes(name)) || (slugWords && t.includes(slugWords));
    })
    .sort((a, b) => (b.name?.length ?? 0) - (a.name?.length ?? 0))[0];
}

/** Case-insensitive name match; longest wins so "Zin Min" beats a "Zin". */
export function matchStylist(
  text: string,
  stylists: StylistResponseDto[]
): StylistResponseDto | undefined {
  const t = text.toLowerCase();
  return stylists
    .filter((s) => {
      const name = (s.name ?? "").toLowerCase();
      const slugWords = (s.slug ?? "").replace(/-/g, " ").toLowerCase();
      return (name && t.includes(name)) || (slugWords && t.includes(slugWords));
    })
    .sort((a, b) => (b.name?.length ?? 0) - (a.name?.length ?? 0))[0];
}

/**
 * Extracts a wall-clock time as minutes-since-midnight: "9:30 AM", "9 am",
 * "14:00". Returns null when the text names no time.
 *
 * Guards against swallowing a bare date: "2026-08-05" must not read as 20:26.
 */
export function parseTimeOfDay(text: string): number | null {
  const t = text.toLowerCase().replace(/\b\d{4}-\d{2}-\d{2}\b/g, "");

  const match = t.match(/\b(\d{1,2})(?::(\d{2}))?\s*(am|pm)\b/) ??
    t.match(/\b(\d{1,2}):(\d{2})\b/);
  if (!match) return null;

  let hour = Number(match[1]);
  const minute = Number(match[2] ?? 0);
  const meridiem = match[3];

  if (meridiem === "pm" && hour !== 12) hour += 12;
  if (meridiem === "am" && hour === 12) hour = 0;

  if (hour > 23 || minute > 59) return null;
  return hour * 60 + minute;
}

/** Minutes-since-midnight back to "9:30 AM", for echoing the asked-for time. */
function formatMinutes(total: number): string {
  const hour = Math.floor(total / 60);
  const minute = total % 60;
  const meridiem = hour < 12 ? "AM" : "PM";
  const hour12 = hour % 12 === 0 ? 12 : hour % 12;
  return `${hour12}:${String(minute).padStart(2, "0")} ${meridiem}`;
}

function money(value: unknown): string {
  return value == null ? "—" : `$${Number(value).toFixed(2)}`;
}

/** Shared unwrap: 401 clears the session, other failures raise ApiError. */
async function unwrap<T>(result: {
  data?: T;
  response: Response;
  error?: unknown;
}): Promise<T | undefined> {
  if (result.response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }
  if (!result.response.ok || result.error) {
    throw new ApiError(
      extractErrorMessage(result.error, result.response.status),
      result.response.status || null
    );
  }
  return result.data;
}

function dayLabel(date: string, today: string): string {
  if (date === today) return "today";
  if (date === addDays(today, 1)) return "tomorrow";
  if (date === addDays(today, -1)) return "yesterday";
  return date;
}

async function answerBookings(text: string): Promise<string> {
  const today = todayDateOnly();
  const date = resolveDate(text, today);
  const label = dayLabel(date, today);

  const appointments =
    (await unwrap(
      await api.GET("/api/Schedule", { params: { query: { from: date, to: date } } })
    )) ?? [];

  // The endpoint returns terminal statuses too; the booked view excludes them.
  const active = appointments
    .filter((a) => a.status !== "Cancelled" && a.status !== "NoShow")
    .sort((a, b) => (a.startsAt ?? "").localeCompare(b.startsAt ?? ""));

  if (active.length === 0) {
    return `Nothing on the books ${label}. [Open the schedule](/schedule)`;
  }

  const lines = active
    .map((a) => {
      const time = a.startsAt ? formatSalonTime(a.startsAt) : "—";
      const client = [a.firstName, a.lastName].filter(Boolean).join(" ") || "Client";
      return `• ${time} — ${client} · ${a.serviceName ?? "Service"} w/ ${
        a.stylistName ?? "unassigned"
      }`;
    })
    .join("\n");

  const cancelled = appointments.length - active.length;
  const footnote = cancelled > 0 ? `\n(${cancelled} cancelled/no-show hidden)` : "";

  return `${active.length} booked ${label}:\n${lines}${footnote}\n[Open the schedule](/schedule)`;
}

async function answerServices(text: string): Promise<string> {
  const services =
    (await unwrap(
      await api.GET("/api/Services", { params: { query: { includeInactive: false } } })
    )) ?? [];

  if (services.length === 0) {
    return "No active services in the catalog. [Manage services](/services)";
  }

  const matched = matchService(text, services);
  if (matched) {
    return (
      `${matched.name} — ${money(matched.price)}, ${matched.durationMinutes} min.\n` +
      `${matched.shortDescription ?? ""}\n[Manage services](/services)`
    );
  }

  const lines = services
    .toSorted((a, b) => Number(a.displayOrder ?? 0) - Number(b.displayOrder ?? 0))
    .map((s) => `• ${s.name} — ${money(s.price)} · ${s.durationMinutes} min`)
    .join("\n");

  return `${services.length} active services:\n${lines}\n[Manage services](/services)`;
}

async function answerAvailability(text: string): Promise<string> {
  const today = todayDateOnly();
  const date = resolveDate(text, today);
  const label = dayLabel(date, today);

  const services =
    (await unwrap(
      await api.GET("/api/Services", { params: { query: { includeInactive: false } } })
    )) ?? [];

  const service = matchService(text, services);
  if (!service?.id) {
    const names = services.map((s) => s.name).join(", ");
    return names
      ? `Which service? I can check openings for: ${names}.`
      : "No active services to check availability against. [Manage services](/services)";
  }

  const stylists = (await unwrap(await api.GET("/api/Stylists", {}))) ?? [];
  const stylist = matchStylist(text, stylists);
  const atMinutes = parseTimeOfDay(text);

  const slots =
    (await unwrap(
      await api.GET("/api/Appointments/slots", {
        params: {
          query: {
            serviceId: service.id,
            date,
            // Omitted entirely when no stylist was named — the API treats a
            // missing stylistId as the any-stylist union view.
            ...(stylist?.id != null ? { stylistId: stylist.id } : {}),
          },
        },
      })
    )) ?? [];

  const who = stylist ? ` with ${stylist.name}` : "";

  // A named time narrows to that exact slot; report the near misses when it's taken.
  if (atMinutes != null) {
    const exact = slots.find(
      (s) => s.startsAt && minutesSinceOpen(s.startsAt, 0) === atMinutes
    );
    const asked = formatMinutes(atMinutes);

    if (exact) {
      const by = exact.stylistName ? ` with ${exact.stylistName}` : who;
      return `Yes — ${asked} ${label} is open for ${service.name}${by}. [Open the schedule](/schedule)`;
    }

    const nearby = slots
      .filter((s) => {
        if (!s.startsAt) return false;
        return Math.abs(minutesSinceOpen(s.startsAt, 0) - atMinutes) <= 90;
      })
      .map((s) => formatSalonTime(s.startsAt!));

    return nearby.length > 0
      ? `${asked} ${label} is not open for ${service.name}${who}. Nearest: ${nearby.join(", ")}\n[Open the schedule](/schedule)`
      : `${asked} ${label} is not open for ${service.name}${who}, and nothing else is free within 90 minutes. [Open the schedule](/schedule)`;
  }

  if (slots.length === 0) {
    return `No openings for ${service.name}${who} ${label}. [Open the schedule](/schedule)`;
  }

  const times = slots
    .map((s) => {
      const time = s.startsAt ? formatSalonTime(s.startsAt) : "—";
      // The union view labels each slot; a stylist-filtered list already has one.
      return !stylist && s.stylistName ? `${time} (${s.stylistName})` : time;
    })
    .join(", ");

  return `${slots.length} openings for ${service.name}${who} ${label}: ${times}\n[Open the schedule](/schedule)`;
}

const HELP_TEXT =
  "I can check the books, the service menu, and open slots. Try:\n" +
  "• “Who's booked tomorrow?”\n" +
  "• “List services and prices”\n" +
  "• “Open slots for haircut on Friday”";

/**
 * Single seam the widget calls. Reads live salon data through the authenticated
 * API client — the same SlotService/Schedule data the MCP `get_appointment_slots`
 * tool exposes to external clients. Intent routing is keyword-based, not an LLM;
 * swapping in a real model means replacing this body only.
 */
export async function sendChatMessage(userText: string): Promise<string> {
  switch (classifyIntent(userText)) {
    case "bookings":
      return answerBookings(userText);
    case "services":
      return answerServices(userText);
    case "availability":
      return answerAvailability(userText);
    default:
      return HELP_TEXT;
  }
}
