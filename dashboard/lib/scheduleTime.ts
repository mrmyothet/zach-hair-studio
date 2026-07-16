/**
 * Salon-local (Asia/Yangon) time math for the staff schedule.
 * Components must use these helpers — never hardcode a UTC offset.
 */

export const SALON_TIME_ZONE = "Asia/Yangon";
export const SALON_ZONE_LABEL = "Myanmar Time";

/** UI-SPEC Spacing exception: 20px per 15 minutes. */
export const PX_PER_15MIN = 20;

/** Default salon book hours (widened by callers when appointments fall outside). */
export const OPEN_HOUR = 9;
export const CLOSE_HOUR = 19;

const timePartsFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: SALON_TIME_ZONE,
  hour: "numeric",
  minute: "2-digit",
  hour12: true,
});

const datePartsFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: SALON_TIME_ZONE,
  month: "short",
  day: "numeric",
});

const dateTimePartsFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: SALON_TIME_ZONE,
  month: "short",
  day: "numeric",
  hour: "numeric",
  minute: "2-digit",
  hour12: true,
});

const ymdPartsFormatter = new Intl.DateTimeFormat("en-CA", {
  timeZone: SALON_TIME_ZONE,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

const wallClockPartsFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: SALON_TIME_ZONE,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false,
  weekday: "short",
});

type PartMap = Record<string, string>;

function partsOf(date: Date, formatter: Intl.DateTimeFormat): PartMap {
  const map: PartMap = {};
  for (const part of formatter.formatToParts(date)) {
    if (part.type !== "literal") map[part.type] = part.value;
  }
  return map;
}

/** Format a wall-clock time in salon local zone, e.g. "10:30 AM". */
export function formatSalonTime(date: Date | string): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return timePartsFormatter.format(d);
}

/** Format a short date in salon local zone, e.g. "Jul 16". */
export function formatSalonDate(date: Date | string): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return datePartsFormatter.format(d);
}

/** Format date+time for audit lines, e.g. "Jul 10, 2:14 PM". */
export function formatSalonDateTime(date: Date | string): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return dateTimePartsFormatter.format(d);
}

/** YYYY-MM-DD in Asia/Yangon for API from/to query params. */
export function toDateOnly(date: Date | string): string {
  const d = typeof date === "string" ? new Date(date) : date;
  const p = partsOf(d, ymdPartsFormatter);
  return `${p.year}-${p.month}-${p.day}`;
}

/** Today's date-only string in salon local time. */
export function todayDateOnly(): string {
  return toDateOnly(new Date());
}

/**
 * Parse a YYYY-MM-DD as a Date anchored at noon UTC so weekday/week math
 * doesn't drift across day boundaries when the browser zone differs from MMT.
 */
export function parseDateOnly(dateOnly: string): Date {
  const [y, m, d] = dateOnly.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d, 12, 0, 0));
}

/** Monday (YYYY-MM-DD) of the week containing `date` — weeks start Monday (D-07). */
export function startOfWeekMonday(date: Date | string): string {
  const dateOnly = typeof date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(date)
    ? date
    : toDateOnly(date);
  const anchor = parseDateOnly(dateOnly);
  // getUTCDay: 0=Sun … 6=Sat; convert so Monday=0
  const day = anchor.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const monday = new Date(anchor);
  monday.setUTCDate(anchor.getUTCDate() + mondayOffset);
  return toDateOnly(monday);
}

export function dayWindow(date: Date | string): { from: string; to: string } {
  const from = typeof date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(date)
    ? date
    : toDateOnly(date);
  return { from, to: from };
}

export function weekWindow(date: Date | string): { from: string; to: string } {
  const from = startOfWeekMonday(date);
  const monday = parseDateOnly(from);
  const sunday = new Date(monday);
  sunday.setUTCDate(monday.getUTCDate() + 6);
  return { from, to: toDateOnly(sunday) };
}

/** Add N calendar days to a YYYY-MM-DD (salon-date arithmetic). */
export function addDays(dateOnly: string, days: number): string {
  const d = parseDateOnly(dateOnly);
  d.setUTCDate(d.getUTCDate() + days);
  return toDateOnly(d);
}

function salonWallClock(date: Date): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
} {
  const p = partsOf(date, wallClockPartsFormatter);
  return {
    year: Number(p.year),
    month: Number(p.month),
    day: Number(p.day),
    hour: Number(p.hour) % 24,
    minute: Number(p.minute),
    second: Number(p.second),
  };
}

/** Minutes since OPEN_HOUR:00 in salon-local wall clock. */
export function minutesSinceOpen(
  startsAt: Date | string,
  openHour: number = OPEN_HOUR
): number {
  const d = typeof startsAt === "string" ? new Date(startsAt) : startsAt;
  const { hour, minute } = salonWallClock(d);
  return hour * 60 + minute - openHour * 60;
}

export function blockTopPx(
  startsAt: Date | string,
  openHour: number = OPEN_HOUR
): number {
  return (minutesSinceOpen(startsAt, openHour) / 15) * PX_PER_15MIN;
}

export function blockHeightPx(durationMinutes: number): number {
  return (durationMinutes / 15) * PX_PER_15MIN;
}

/** Weekday short label in salon zone for a YYYY-MM-DD. */
export function weekdayShort(dateOnly: string): string {
  const d = parseDateOnly(dateOnly);
  const p = partsOf(d, wallClockPartsFormatter);
  return p.weekday ?? "";
}
