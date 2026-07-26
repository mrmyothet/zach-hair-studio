"use client";

import { useMemo, useState } from "react";
import type { TimeOffRange } from "@/lib/useAvailability";
import { addDays, parseDateOnly, toDateOnly } from "@/lib/scheduleTime";
import { ChevronLeftIcon, ChevronRightIcon } from "@/components/icons";

const WEEKDAY_HEADERS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

/**
 * Fixed Asia/Yangon offset (+06:30, no DST — Phase 2 decision, STATE.md) used
 * to construct whole-day time-off boundaries client-side. This editor only
 * ever paints whole calendar days; StartsAt is salon-local midnight of the
 * first day, EndsAt is salon-local midnight of the day AFTER the last day
 * (exclusive), matching how SlotService blocks by instant range.
 */
const SALON_OFFSET = "+06:30";

function salonMidnightIso(dateOnly: string): string {
  return `${dateOnly}T00:00:00${SALON_OFFSET}`;
}

function firstOfMonth(dateOnly: string): string {
  const [y, m] = dateOnly.split("-");
  return `${y}-${m}-01`;
}

function addMonths(firstDay: string, months: number): string {
  const [y, m] = firstDay.split("-").map(Number);
  const total = y * 12 + (m - 1) + months;
  const ny = Math.floor(total / 12);
  const nm = (total % 12) + 1;
  return `${ny}-${String(nm).padStart(2, "0")}-01`;
}

function daysInMonth(firstDay: string): number {
  const [y, m] = firstDay.split("-").map(Number);
  return new Date(Date.UTC(y, m, 0)).getUTCDate();
}

/** Monday=0 .. Sunday=6 (weeks start Monday, matching scheduleTime's convention). */
function mondayIndex(dateOnly: string): number {
  const day = parseDateOnly(dateOnly).getUTCDay();
  return day === 0 ? 6 : day - 1;
}

function monthLabel(firstDay: string): string {
  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(parseDateOnly(firstDay));
}

function dayLabel(dateOnly: string): string {
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(parseDateOnly(dateOnly));
}

function coversDate(range: TimeOffRange, dateOnly: string): boolean {
  const start = toDateOnly(range.startsAt);
  const endExclusive = toDateOnly(range.endsAt);
  return dateOnly >= start && dateOnly < endExclusive;
}

function isRangeAnchor(range: TimeOffRange, dateOnly: string): boolean {
  const prev = addDays(dateOnly, -1);
  return !coversDate(range, prev);
}

function rangeDateLabel(range: TimeOffRange): string {
  const start = toDateOnly(range.startsAt);
  const lastDay = addDays(toDateOnly(range.endsAt), -1);
  return start === lastDay ? dayLabel(start) : `${dayLabel(start)} – ${dayLabel(lastDay)}`;
}

type Props = {
  value: TimeOffRange[];
  onChange: (ranges: TimeOffRange[]) => void;
  isLoading?: boolean;
};

/**
 * Single-month time-off calendar (D-07). "Add Time Off" arms a click-start /
 * click-end paint flow; painted ranges render as dashed-muted bands (never
 * gold, never red — time off is routine, not "available" or an error).
 */
export function TimeOffCalendar({ value, onChange, isLoading = false }: Props) {
  const [monthStart, setMonthStart] = useState(() => firstOfMonth(toDateOnly(new Date())));
  const [armed, setArmed] = useState(false);
  const [pendingStart, setPendingStart] = useState<string | null>(null);

  const days = useMemo(() => {
    const count = daysInMonth(monthStart);
    return Array.from({ length: count }, (_, i) => addDays(monthStart, i));
  }, [monthStart]);

  const leadingBlanks = mondayIndex(monthStart);

  function startAdd() {
    setArmed(true);
    setPendingStart(null);
  }

  function cancelAdd() {
    setArmed(false);
    setPendingStart(null);
  }

  function handleDayClick(dateOnly: string) {
    if (isLoading || !armed) return;

    if (!pendingStart) {
      setPendingStart(dateOnly);
      return;
    }

    const start = pendingStart <= dateOnly ? pendingStart : dateOnly;
    const end = pendingStart <= dateOnly ? dateOnly : pendingStart;
    const range: TimeOffRange = {
      startsAt: salonMidnightIso(start),
      endsAt: salonMidnightIso(addDays(end, 1)),
      reason: null,
    };
    onChange([...value, range]);
    setArmed(false);
    setPendingStart(null);
  }

  function removeRange(target: TimeOffRange) {
    onChange(value.filter((r) => r !== target));
  }

  function updateReason(target: TimeOffRange, reason: string) {
    onChange(
      value.map((r) => (r === target ? { ...r, reason: reason || null } : r))
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-3 gap-2 flex-wrap">
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setMonthStart((d) => addMonths(d, -1))}
            aria-label="Previous month"
            className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink hover:border-gold-dark/40"
          >
            <ChevronLeftIcon className="h-5 w-5" />
          </button>
          <h3 className="text-sm font-semibold text-ink w-36 text-center">
            {monthLabel(monthStart)}
          </h3>
          <button
            type="button"
            onClick={() => setMonthStart((d) => addMonths(d, 1))}
            aria-label="Next month"
            className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink hover:border-gold-dark/40"
          >
            <ChevronRightIcon className="h-5 w-5" />
          </button>
        </div>

        {armed ? (
          <div className="flex items-center gap-3">
            <p className="text-xs uppercase tracking-wider text-muted">
              {pendingStart ? "Select an end date" : "Select a start date"}
            </p>
            <button
              type="button"
              onClick={cancelAdd}
              className="text-sm text-muted hover:text-ink"
            >
              Cancel
            </button>
          </div>
        ) : (
          <button
            type="button"
            disabled={isLoading}
            onClick={startAdd}
            className="min-h-11 px-4 rounded-xl border border-border text-sm text-ink hover:border-gold-dark/40 disabled:opacity-60"
          >
            Add Time Off
          </button>
        )}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-7 gap-1">
          {Array.from({ length: 35 }, (_, i) => (
            <div key={i} className="h-10 w-10 rounded-lg bg-surface-alt animate-pulse" />
          ))}
        </div>
      ) : (
        <>
          <div className="grid grid-cols-7 gap-1 mb-1">
            {WEEKDAY_HEADERS.map((h) => (
              <div
                key={h}
                className="h-6 flex items-center justify-center text-xs uppercase tracking-wider text-muted"
              >
                {h}
              </div>
            ))}
          </div>
          <div className="grid grid-cols-7 gap-1">
            {Array.from({ length: leadingBlanks }, (_, i) => (
              <div key={`blank-${i}`} className="h-10 w-10" />
            ))}
            {days.map((dateOnly) => {
              const range = value.find((r) => coversDate(r, dateOnly));
              const isAnchor = range ? isRangeAnchor(range, dateOnly) : false;
              const isPendingStart = pendingStart === dateOnly;

              let cellClass =
                "relative h-10 w-10 rounded-lg border text-xs flex items-center justify-center";
              if (range) {
                cellClass += " bg-muted/15 border-2 border-dashed border-muted text-ink";
              } else if (isPendingStart) {
                cellClass += " border-2 border-gold-dark bg-gold-dark/10 text-ink";
              } else {
                cellClass += " border border-border text-ink";
              }
              if (armed && !range) {
                cellClass += " hover:border-gold-dark/60 cursor-pointer";
              }

              return (
                <button
                  key={dateOnly}
                  type="button"
                  disabled={(!armed && !range) || (armed && Boolean(range))}
                  onClick={() => handleDayClick(dateOnly)}
                  className={cellClass}
                  title={range?.reason ?? undefined}
                >
                  <span>{Number(dateOnly.slice(8, 10))}</span>
                  {range && isAnchor && range.reason ? (
                    <span className="absolute -bottom-4 left-0 right-0 text-[10px] text-muted truncate px-0.5">
                      {range.reason}
                    </span>
                  ) : null}
                </button>
              );
            })}
          </div>
        </>
      )}

      {!isLoading && value.length === 0 ? (
        <p className="text-sm text-muted mt-4">No time off scheduled.</p>
      ) : null}

      {!isLoading && value.length > 0 ? (
        <ul className="mt-6 flex flex-col gap-2">
          {value
            .slice()
            .sort((a, b) => a.startsAt.localeCompare(b.startsAt))
            .map((range, i) => (
              <li
                key={range.id ?? `pending-${i}`}
                className="flex flex-wrap items-center gap-2 rounded-lg border border-border px-3 py-2"
              >
                <span className="text-sm text-ink">{rangeDateLabel(range)}</span>
                {range.id == null ? (
                  <input
                    type="text"
                    value={range.reason ?? ""}
                    onChange={(e) => updateReason(range, e.target.value)}
                    placeholder="Reason (optional)"
                    className="min-h-11 flex-1 min-w-[10rem] rounded-lg border border-border bg-surface px-3 text-sm text-ink"
                  />
                ) : (
                  <span className="text-sm text-muted truncate flex-1">
                    {range.reason || "—"}
                  </span>
                )}
                <button
                  type="button"
                  onClick={() => removeRange(range)}
                  className="min-h-11 px-3 rounded-lg border border-border text-sm text-ink hover:border-rose-600/40 hover:text-rose-600"
                >
                  Remove
                </button>
              </li>
            ))}
        </ul>
      ) : null}
    </div>
  );
}
