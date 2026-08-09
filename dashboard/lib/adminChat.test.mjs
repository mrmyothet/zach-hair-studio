/**
 * Self-check for adminChat's pure routing logic — the only non-obvious part.
 * Run: node lib/adminChat.test.mjs   (from dashboard/)
 *
 * Mirrors the implementations in adminChat.ts. Kept as a standalone copy because
 * the dashboard has no test runner or TS loader wired up; if you change the
 * regexes or date math there, change them here and re-run.
 */
import assert from "node:assert/strict";

const WEEKDAYS = [
  "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday",
];

function classifyIntent(text) {
  const t = text.toLowerCase();
  if (/\b(open\w*|free|availab\w*|slots?|vacan\w*)\b/.test(t)) return "availability";
  if (/\b(book\w*|appointments?|schedule|clients?|who'?s|busy|today|tomorrow)\b/.test(t)) return "bookings";
  if (/\b(services?|pric\w*|costs?|how much|menu|offers?)\b/.test(t)) return "services";
  return "help";
}

function addDays(dateOnly, days) {
  const d = new Date(`${dateOnly}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

function resolveDate(text, today) {
  const t = text.toLowerCase();
  const explicit = t.match(/\b(\d{4}-\d{2}-\d{2})\b/);
  if (explicit) return explicit[1];
  if (/\btom+or+ow\b/.test(t)) return addDays(today, 1);
  if (/\byester+day\b/.test(t)) return addDays(today, -1);
  const words = t.split(/\W+/);
  const named = WEEKDAYS.findIndex((day) => words.includes(day));
  if (named >= 0) {
    const todayDow = new Date(`${today}T12:00:00Z`).getUTCDay();
    return addDays(today, (named - todayDow + 7) % 7);
  }
  return today;
}

function parseTimeOfDay(text) {
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

function matchService(text, services) {
  const t = text.toLowerCase();
  return services
    .filter((s) => {
      const name = (s.name ?? "").toLowerCase();
      const slugWords = (s.slug ?? "").replace(/-/g, " ").toLowerCase();
      return (name && t.includes(name)) || (slugWords && t.includes(slugWords));
    })
    .sort((a, b) => (b.name?.length ?? 0) - (a.name?.length ?? 0))[0];
}

// --- intent routing ---------------------------------------------------------
assert.equal(classifyIntent("Who's booked today?"), "bookings");
assert.equal(classifyIntent("What's on tomorrow?"), "bookings");
assert.equal(classifyIntent("list services and prices"), "services");
assert.equal(classifyIntent("how much is a cut"), "services");
assert.equal(classifyIntent("any open slots friday"), "availability");
// Availability wins over both other intents when the words collide.
assert.equal(classifyIntent("open slots for a haircut today"), "availability");
assert.equal(classifyIntent("what is the wifi password"), "help");

// --- date resolution (2026-08-05 is a Wednesday) ----------------------------
const WED = "2026-08-05";
assert.equal(resolveDate("who's booked", WED), WED, "defaults to today");
assert.equal(resolveDate("what's on tomorrow", WED), "2026-08-06");
assert.equal(resolveDate("yesterday's no-shows", WED), "2026-08-04");
assert.equal(resolveDate("bookings on 2026-12-24", WED), "2026-12-24");
assert.equal(resolveDate("openings friday", WED), "2026-08-07", "next upcoming weekday");
assert.equal(resolveDate("openings monday", WED), "2026-08-10", "wraps to next week");
assert.equal(resolveDate("openings wednesday", WED), WED, "today counts as itself");
// Regression: a misspelling used to fall through to today and read as a wrong answer.
assert.equal(resolveDate("available tommorow", WED), "2026-08-06", "doubled m");
assert.equal(resolveDate("available tomorow", WED), "2026-08-06", "single r");
assert.equal(resolveDate("available tommorrow", WED), "2026-08-06", "doubled both");
// The reported query, in full.
assert.equal(
  resolveDate("Is there available schedule for Precision Cut, Zin Min, tommorow, 9:30 AM", WED),
  "2026-08-06"
);

// --- time-of-day parsing ----------------------------------------------------
assert.equal(parseTimeOfDay("9:30 AM"), 570);
assert.equal(parseTimeOfDay("anything at 9 am"), 540);
assert.equal(parseTimeOfDay("2:15 pm"), 855);
assert.equal(parseTimeOfDay("12:00 am"), 0, "midnight is hour 0");
assert.equal(parseTimeOfDay("12:30 pm"), 750, "noon stays 12");
assert.equal(parseTimeOfDay("14:00"), 840, "24-hour form");
assert.equal(parseTimeOfDay("who's booked tomorrow"), null, "no time named");
assert.equal(parseTimeOfDay("bookings on 2026-08-05"), null, "date is not a time");
assert.equal(parseTimeOfDay("at 25:00"), null, "out of range");

// --- service matching -------------------------------------------------------
const services = [
  { name: "Color", slug: "color" },
  { name: "Color Correction", slug: "color-correction" },
  { name: "Haircut", slug: "haircut" },
];
assert.equal(matchService("slots for color correction", services).slug, "color-correction",
  "longest name wins over the substring match");
assert.equal(matchService("slots for color", services).slug, "color");
assert.equal(matchService("slots for a haircut", services).slug, "haircut");
assert.equal(matchService("slots for balayage", services), undefined);

// matchStylist shares matchService's implementation shape.
const stylists = [{ name: "Zin", slug: "zin" }, { name: "Zin Min", slug: "zin-min" }];
assert.equal(
  matchService("available for Zin Min tomorrow", stylists).slug,
  "zin-min",
  "longest stylist name wins"
);
assert.equal(matchService("available for zin tomorrow", stylists).slug, "zin");

console.log("adminChat self-check: all assertions passed");
