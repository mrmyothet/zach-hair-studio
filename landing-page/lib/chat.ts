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

const MONTH_NAME_PATTERN =
  "jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?";

/**
 * Resolves day/month from a numeric pair. When one side is > 12 the order is
 * unambiguous (23/07 → day-first, 7/26 → month-first). When both are ≤ 12,
 * prefer day-first — the salon locale is Myanmar (DD/MM).
 */
function resolveDayAndMonth(
  first: number,
  second: number
): { day: number; month: number } | null {
  if (first > 12 && second >= 1 && second <= 12) {
    return { day: first, month: second };
  }
  if (second > 12 && first >= 1 && first <= 12) {
    return { day: second, month: first };
  }
  if (first >= 1 && first <= 12 && second >= 1 && second <= 12) {
    return { day: first, month: second };
  }
  return null;
}

function isValidCalendarDate(year: number, month: number, day: number): boolean {
  if (year < 2000 || year > 2100) return false;
  const probe = new Date(year, month - 1, day);
  return (
    probe.getFullYear() === year &&
    probe.getMonth() === month - 1 &&
    probe.getDate() === day
  );
}

function parseYearToken(raw: string | undefined): number | null {
  if (!raw) return null;
  if (raw.length === 2) return 2000 + Number(raw);
  return Number(raw);
}

/** Builds an ISO date, rolling a missing year forward past today when needed. */
function finalizeDate(
  year: number | null,
  month: number,
  day: number,
  today: Date
): string | null {
  let resolvedYear = year ?? today.getFullYear();
  if (year == null && new Date(resolvedYear, month - 1, day) < startOfDay(today)) {
    resolvedYear += 1;
  }
  if (!isValidCalendarDate(resolvedYear, month, day)) return null;
  return toIsoDate(resolvedYear, month, day);
}

function monthIndexFromName(name: string): number {
  return MONTH_ABBREVIATIONS.indexOf(name.slice(0, 3).toLowerCase());
}

/**
 * Pulls a target date out of free-text chat input. Supports common forms users
 * type in chat:
 * - ISO / year-first: 2026-07-23, 2026/07/23, 2026.07.23
 * - Month then day: Jul 23, July 23rd 2026, July 23, 2026
 * - Day then month: 23 Jul, 23rd of July 2026
 * - Numeric DD/MM (Myanmar default) or unambiguous MM/DD: 23/07/2026,
 *   23-07-2026, 23.07.2026, 23,07,2026, 23 07 2026, 7/26
 * - Compact: 20260723, 23072026
 * - Relative: today, tomorrow
 *
 * A bare month/day with no year rolls forward to next year if that date has
 * already passed this year — chat is always asking about a future booking.
 * Exported for coverage of the format matrix.
 */
export function parseDateFromText(input: string, today: Date = new Date()): string | null {
  // Year-first: 2026-07-23 / 2026/07/23 / 2026.07.23
  const yearFirst = input.match(/\b(\d{4})([-/.])(\d{1,2})\2(\d{1,2})\b/);
  if (yearFirst) {
    return finalizeDate(
      Number(yearFirst[1]),
      Number(yearFirst[3]),
      Number(yearFirst[4]),
      today
    );
  }

  // Month name then day: "Jul 23", "July 23rd, 2026"
  const monthThenDay = input.match(
    new RegExp(
      `\\b(${MONTH_NAME_PATTERN})\\.?\\s+(\\d{1,2})(?:st|nd|rd|th)?(?:,?\\s+(\\d{2,4}))?\\b`,
      "i"
    )
  );
  if (monthThenDay) {
    const monthIndex = monthIndexFromName(monthThenDay[1]);
    if (monthIndex >= 0) {
      return finalizeDate(
        parseYearToken(monthThenDay[3]),
        monthIndex + 1,
        Number(monthThenDay[2]),
        today
      );
    }
  }

  // Day then month name: "23 Jul", "23rd of July 2026"
  const dayThenMonth = input.match(
    new RegExp(
      `\\b(\\d{1,2})(?:st|nd|rd|th)?\\s+(?:of\\s+)?(${MONTH_NAME_PATTERN})\\.?(?:,?\\s+(\\d{2,4}))?\\b`,
      "i"
    )
  );
  if (dayThenMonth) {
    const monthIndex = monthIndexFromName(dayThenMonth[2]);
    if (monthIndex >= 0) {
      return finalizeDate(
        parseYearToken(dayThenMonth[3]),
        monthIndex + 1,
        Number(dayThenMonth[1]),
        today
      );
    }
  }

  // Space-separated day month year: "23 07 2026"
  const spaced = input.match(/\b(\d{1,2})\s+(\d{1,2})\s+(\d{4})\b/);
  if (spaced) {
    const resolved = resolveDayAndMonth(Number(spaced[1]), Number(spaced[2]));
    if (resolved) {
      return finalizeDate(Number(spaced[3]), resolved.month, resolved.day, today);
    }
  }

  // Separators / - , . — e.g. 23/07/2026, 23,07,2026, 7/26
  const numeric = input.match(
    /\b(\d{1,2})[/.,-](\d{1,2})(?:[/.,-](\d{2,4}))?\b/
  );
  if (numeric) {
    const resolved = resolveDayAndMonth(Number(numeric[1]), Number(numeric[2]));
    if (resolved) {
      return finalizeDate(
        parseYearToken(numeric[3]),
        resolved.month,
        resolved.day,
        today
      );
    }
  }

  // Compact 8 digits: YYYYMMDD or DDMMYYYY
  const compact = input.match(/\b(\d{8})\b/);
  if (compact) {
    const digits = compact[1];
    const ymd = finalizeDate(
      Number(digits.slice(0, 4)),
      Number(digits.slice(4, 6)),
      Number(digits.slice(6, 8)),
      today
    );
    if (ymd) return ymd;

    const dmy = finalizeDate(
      Number(digits.slice(4, 8)),
      Number(digits.slice(2, 4)),
      Number(digits.slice(0, 2)),
      today
    );
    if (dmy) return dmy;
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

/** True when the message is asking about open times / availability, not just service info. */
function isAvailabilityQuestion(normalizedInput: string): boolean {
  return /\b(available|availability|schedule|slot|slots|openings?|free|bookable)\b/.test(
    normalizedInput
  );
}

/** Salon-local calendar date as YYYY-MM-DD (matches booking min-date / Asia/Yangon). */
function salonTodayIso(now: Date): string {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: SALON_TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(now);

  const year = Number(parts.find((part) => part.type === "year")?.value);
  const month = Number(parts.find((part) => part.type === "month")?.value);
  const day = Number(parts.find((part) => part.type === "day")?.value);
  return toIsoDate(year, month, day);
}

/**
 * Checks real slot availability (via GET /api/appointments/slots) for a
 * service the caller mentioned alongside a specific date, and replies with
 * what's actually open — instead of the static price/duration blurb.
 * Past calendar days (and past clock times on today) are refused — same
 * future-only rule as the booking form and StartsAt validator.
 */
async function checkAvailabilityReply(
  service: Service,
  normalizedInput: string,
  isoDate: string,
  stylists: Stylist[],
  now: Date = new Date()
): Promise<string> {
  const matchedStylist = findMatchingStylist(normalizedInput, stylists);
  const requestedTime = parseTimeFromText(normalizedInput);
  const dateLabel = availabilityDateFormatter.format(new Date(`${isoDate}T00:00:00Z`));
  const stylistLabel = matchedStylist ? ` with ${matchedStylist.name}` : "";
  const todayIso = salonTodayIso(now);

  if (isoDate < todayIso) {
    return (
      `${dateLabel} has already passed — I can only check availability for today or a future date. ` +
      `${bookLink(service)}`
    );
  }

  let slots: OpenSlot[];
  try {
    slots = await fetchOpenSlots(service.id, matchedStylist?.id ?? null, isoDate);
  } catch {
    return (
      `I couldn't check availability for ${dateLabel} right now — ` +
      `please try booking directly. ${bookLink(service)}`
    );
  }

  // Align with API BeInTheFuture — drop starts that are already gone (esp. today).
  const bookableSlots = slots.filter((slot) => new Date(slot.startsAt) > now);

  if (requestedTime && isoDate === todayIso) {
    const requestedMinutes = requestedTime.hour * 60 + requestedTime.minute;
    const stillOnGrid = slots.some((slot) => {
      const { hour, minute } = slotTimeOfDay(slot);
      return hour * 60 + minute === requestedMinutes;
    });
    const stillBookable = bookableSlots.some((slot) => {
      const { hour, minute } = slotTimeOfDay(slot);
      return hour * 60 + minute === requestedMinutes;
    });
    if (stillOnGrid && !stillBookable) {
      return (
        `That time has already passed on ${dateLabel}. ` +
        `Pick a later time, or choose another day. ${bookLink(service)}`
      );
    }
  }

  if (bookableSlots.length === 0) {
    return `Sorry, there's no availability for ${service.name}${stylistLabel} on ${dateLabel}. ${bookLink(service)}`;
  }

  if (requestedTime) {
    const requestedMinutes = requestedTime.hour * 60 + requestedTime.minute;
    const matchingSlot = bookableSlots.find((slot) => {
      const { hour, minute } = slotTimeOfDay(slot);
      return hour * 60 + minute === requestedMinutes;
    });

    if (matchingSlot) {
      return (
        `Yes! ${service.name}${stylistLabel} is available at ` +
        `${slotTimeFormatter.format(new Date(matchingSlot.startsAt))} on ${dateLabel}. ${bookLink(service)}`
      );
    }

    const nearby = bookableSlots
      .slice(0, 5)
      .map((slot) => slotTimeFormatter.format(new Date(slot.startsAt)))
      .join(", ");
    return (
      `That exact time isn't open, but ${service.name}${stylistLabel} has these times ` +
      `available on ${dateLabel}: ${nearby}. ${bookLink(service)}`
    );
  }

  const times = bookableSlots
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
  const now = new Date();
  // Parse relative to salon calendar day so "today" / year roll-forward match
  // the past-date guard (Asia/Yangon), not the visitor's browser timezone.
  const [salonYear, salonMonth, salonDay] = salonTodayIso(now)
    .split("-")
    .map(Number);
  const salonCalendarToday = new Date(salonYear, salonMonth - 1, salonDay);

  const matchedService = findMatchingService(normalizedInput, services);
  const requestedDate = parseDateFromText(normalizedInput, salonCalendarToday);

  // a. Service + specific date → check real availability, not price/duration.
  if (matchedService && requestedDate) {
    return checkAvailabilityReply(
      matchedService,
      normalizedInput,
      requestedDate,
      stylists,
      now
    );
  }

  // a2. Availability asked without a date → prompt for one (don't fall through
  // to the static price/duration blurb).
  if (matchedService && isAvailabilityQuestion(normalizedInput)) {
    const matchedStylist = findMatchingStylist(normalizedInput, stylists);
    const stylistHint = matchedStylist ? ` with ${matchedStylist.name}` : "";
    return (
      `I can check openings for ${matchedService.name}${stylistHint} — ` +
      `what date works for you? For example: Aug 12, or 12/08/2026. ` +
      `${bookLink(matchedService)}`
    );
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
