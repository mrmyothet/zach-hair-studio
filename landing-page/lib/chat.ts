import type { Service } from "@/lib/services";
import { fetchOpenSlots, type OpenSlot, type Stylist } from "@/lib/appointments";
import { formatDuration } from "@/lib/formatDuration";

/**
 * Chat message model shared by the mock engine below and the ChatWidget UI.
 */
export type ChatRole = "user" | "assistant";

export type ChatMessage = {
  id: string;
  role: ChatRole;
  text: string;
};

/**
 * Booking-assistant openers shown as chips while the conversation is empty.
 */
export const STARTER_PROMPTS: readonly string[] = [
  "What services do you offer?",
  "How much does a haircut cost?",
  "What are your opening hours?",
  "I'd like to book an appointment",
];

// Module-scoped counter so ids are deterministic (no crypto.randomUUID / Date.now,
// which would risk SSR/hydration id mismatches on a client-only chat surface).
let messageIdCounter = 0;

export function createMessage(role: ChatRole, text: string): ChatMessage {
  messageIdCounter += 1;
  return { id: `msg-${messageIdCounter}`, role, text };
}

const MOCK_REPLY_DELAY_MS = 600;

// Mirrors the salon hours copy shown in Contact.tsx.
const SALON_HOURS = "Open Daily: 9:00 AM – 7:30 PM";

// Mirrors AppointmentBookingForm's SALON_TIME_ZONE — every slot time must be
// rendered in the salon's zone, never the visitor's local zone (D-16).
const SALON_TIME_ZONE = "Asia/Yangon";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const slotTimeFormatter = new Intl.DateTimeFormat("en-US", {
  hour: "numeric",
  minute: "2-digit",
  timeZone: SALON_TIME_ZONE,
});

const slotHourPartsFormatter = new Intl.DateTimeFormat("en-US", {
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: SALON_TIME_ZONE,
});

// Formats a bare "YYYY-MM-DD" as a calendar date. Interpreted as UTC so the
// label never shifts a day depending on the visitor's local timezone offset.
const availabilityDateFormatter = new Intl.DateTimeFormat("en-US", {
  month: "long",
  day: "numeric",
  year: "numeric",
  timeZone: "UTC",
});

const BOOK_LINK = "[Book an appointment](/book)";

function byDisplayOrder(services: Service[]): Service[] {
  return services.toSorted((a, b) => a.displayOrder - b.displayOrder);
}

function findMatchingService(
  normalizedInput: string,
  services: Service[]
): Service | undefined {
  return byDisplayOrder(services).find((service) => {
    const name = service.name.toLowerCase();
    const slugAsWords = service.slug.replace(/-/g, " ").toLowerCase();
    return normalizedInput.includes(name) || normalizedInput.includes(slugAsWords);
  });
}

function findMatchingStylist(
  normalizedInput: string,
  stylists: Stylist[]
): Stylist | undefined {
  return stylists.find((stylist) =>
    normalizedInput.includes(stylist.name.toLowerCase())
  );
}

const MONTH_ABBREVIATIONS = [
  "jan", "feb", "mar", "apr", "may", "jun",
  "jul", "aug", "sep", "oct", "nov", "dec",
];

function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function toIsoDate(year: number, month: number, day: number): string {
  return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

/**
 * Pulls a target date out of free-text chat input. Recognizes ISO (2026-07-26),
 * "Jul 26" / "July 26th" (optionally with a year), and numeric "7/26" formats.
 * A bare month/day with no year rolls forward to next year if that date has
 * already passed this year — chat is always asking about a future booking.
 */
function parseDateFromText(input: string, today: Date): string | null {
  const iso = input.match(/\b(\d{4})-(\d{2})-(\d{2})\b/);
  if (iso) return `${iso[1]}-${iso[2]}-${iso[3]}`;

  const monthName = input.match(
    /\b(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\.?\s+(\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(\d{4}))?\b/i
  );
  if (monthName) {
    const monthIndex = MONTH_ABBREVIATIONS.indexOf(monthName[1].toLowerCase());
    const day = Number(monthName[2]);
    let year = monthName[3] ? Number(monthName[3]) : today.getFullYear();
    if (!monthName[3] && new Date(year, monthIndex, day) < startOfDay(today)) {
      year += 1;
    }
    return toIsoDate(year, monthIndex + 1, day);
  }

  const numeric = input.match(/\b(\d{1,2})\/(\d{1,2})(?:\/(\d{2,4}))?\b/);
  if (numeric) {
    const month = Number(numeric[1]);
    const day = Number(numeric[2]);
    let year = numeric[3]
      ? (numeric[3].length === 2 ? 2000 + Number(numeric[3]) : Number(numeric[3]))
      : today.getFullYear();
    if (!numeric[3] && new Date(year, month - 1, day) < startOfDay(today)) {
      year += 1;
    }
    return toIsoDate(year, month, day);
  }

  if (/\btoday\b/.test(input)) {
    return toIsoDate(today.getFullYear(), today.getMonth() + 1, today.getDate());
  }
  if (/\btomorrow\b/.test(input)) {
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    return toIsoDate(tomorrow.getFullYear(), tomorrow.getMonth() + 1, tomorrow.getDate());
  }

  return null;
}

/** Parses a clock time like "9:30 AM" / "2pm" out of free text. */
function parseTimeFromText(input: string): { hour: number; minute: number } | null {
  const match = input.match(/\b(\d{1,2})(?::(\d{2}))?\s*(am|pm)\b/i);
  if (!match) return null;

  let hour = Number(match[1]) % 12;
  const minute = match[2] ? Number(match[2]) : 0;
  if (match[3].toLowerCase() === "pm") hour += 12;

  return { hour, minute };
}

function slotTimeOfDay(slot: OpenSlot): { hour: number; minute: number } {
  const parts = slotHourPartsFormatter.formatToParts(new Date(slot.startsAt));
  const hour = Number(parts.find((part) => part.type === "hour")?.value ?? "0");
  const minute = Number(parts.find((part) => part.type === "minute")?.value ?? "0");
  return { hour, minute };
}

function bookLink(service: Service): string {
  return `[Book ${service.name}](/book?service=${service.slug})`;
}

/**
 * Checks real slot availability (via GET /api/appointments/slots) for a
 * service the caller mentioned alongside a specific date, and replies with
 * what's actually open — instead of the static price/duration blurb.
 */
async function checkAvailabilityReply(
  service: Service,
  normalizedInput: string,
  isoDate: string,
  stylists: Stylist[]
): Promise<string> {
  const matchedStylist = findMatchingStylist(normalizedInput, stylists);
  const requestedTime = parseTimeFromText(normalizedInput);
  const dateLabel = availabilityDateFormatter.format(new Date(`${isoDate}T00:00:00Z`));
  const stylistLabel = matchedStylist ? ` with ${matchedStylist.name}` : "";

  let slots: OpenSlot[];
  try {
    slots = await fetchOpenSlots(service.id, matchedStylist?.id ?? null, isoDate);
  } catch {
    return (
      `I couldn't check availability for ${dateLabel} right now — ` +
      `please try booking directly. ${bookLink(service)}`
    );
  }

  if (slots.length === 0) {
    return `Sorry, there's no availability for ${service.name}${stylistLabel} on ${dateLabel}. ${bookLink(service)}`;
  }

  if (requestedTime) {
    const requestedMinutes = requestedTime.hour * 60 + requestedTime.minute;
    const matchingSlot = slots.find((slot) => {
      const { hour, minute } = slotTimeOfDay(slot);
      return hour * 60 + minute === requestedMinutes;
    });

    if (matchingSlot) {
      return (
        `Yes! ${service.name}${stylistLabel} is available at ` +
        `${slotTimeFormatter.format(new Date(matchingSlot.startsAt))} on ${dateLabel}. ${bookLink(service)}`
      );
    }

    const nearby = slots
      .slice(0, 5)
      .map((slot) => slotTimeFormatter.format(new Date(slot.startsAt)))
      .join(", ");
    return (
      `That exact time isn't open, but ${service.name}${stylistLabel} has these times ` +
      `available on ${dateLabel}: ${nearby}. ${bookLink(service)}`
    );
  }

  const times = slots
    .slice(0, 6)
    .map((slot) => slotTimeFormatter.format(new Date(slot.startsAt)))
    .join(", ");
  return `${service.name}${stylistLabel} is available on ${dateLabel} at: ${times}. ${bookLink(service)}`;
}

/**
 * Single seam for a real backend: this is the ONLY function a future chat API
 * needs to replace. Swap the body below for a `fetch` to the real endpoint —
 * the signature and return type (Promise<string>) stay identical, so no
 * ChatWidget code needs to change. `history` is accepted now (for a future
 * request payload) but unused by this mock.
 */
export async function sendChatMessage(
  userText: string,
  history: ChatMessage[],
  services: Service[],
  stylists: Stylist[] = []
): Promise<string> {
  await new Promise((resolve) => setTimeout(resolve, MOCK_REPLY_DELAY_MS));

  const normalizedInput = userText.toLowerCase().trim();

  const matchedService = findMatchingService(normalizedInput, services);
  const requestedDate = parseDateFromText(normalizedInput, new Date());

  // a. Service + specific date → check real availability, not price/duration.
  if (matchedService && requestedDate) {
    return checkAvailabilityReply(matchedService, normalizedInput, requestedDate, stylists);
  }

  // b. Service name match
  if (matchedService) {
    return (
      `${matchedService.name} is ${priceFormatter.format(matchedService.price)} ` +
      `and takes about ${formatDuration(matchedService.durationMinutes)}. ` +
      `${matchedService.shortDescription} ` +
      `[Book ${matchedService.name}](/book?service=${matchedService.slug})`
    );
  }

  // c. book / appointment / schedule
  if (/\b(book|appointment|schedule)\b/.test(normalizedInput)) {
    return `Let's get you booked in! Head over to our booking page to pick a time that works for you. ${BOOK_LINK}`;
  }

  // d. price / cost / how much
  if (/\b(price|cost|how much)\b/.test(normalizedInput)) {
    const cheapest = byDisplayOrder(services).slice(0, 3);
    if (cheapest.length === 0) {
      return `Pricing varies by service — head to our booking page to see current rates. ${BOOK_LINK}`;
    }
    const lines = cheapest
      .map((service) => `${service.name} — ${priceFormatter.format(service.price)}`)
      .join(", ");
    return `Here are a few of our services and prices: ${lines}. ${BOOK_LINK}`;
  }

  // e. hour / open / close
  if (/\b(hours?|open|close)\b/.test(normalizedInput)) {
    return `${SALON_HOURS}. ${BOOK_LINK}`;
  }

  // f. service / offer / do you do
  if (/\b(service|offer|do you do)\b/.test(normalizedInput)) {
    const allServices = byDisplayOrder(services);
    if (allServices.length === 0) {
      return `We offer a full range of hair styling and coloring services — head to our booking page to see them. ${BOOK_LINK}`;
    }
    const names = allServices.map((service) => service.name).join(", ");
    return `We offer: ${names}. ${BOOK_LINK}`;
  }

  // g. fallback
  return (
    "I'm your booking assistant — I can help with services, pricing, hours, " +
    `or getting you booked in. What would you like to know? ${BOOK_LINK}`
  );
}
