"use client";

import { useEffect, useRef, useState } from "react";
import type { DayOfWeekName, WorkingHoursSegment } from "@/lib/useAvailability";
import { WEEKDAYS } from "@/lib/useAvailability";

/**
 * Default business window (UI-SPEC Open Question 1) — deliberately wider than
 * the Phase 2 seed data (every stylist seeded 09:00-18:00) so staff have room
 * to paint earlier/later hours than the placeholder default, rather than
 * clamping the editor to exactly what was seeded.
 */
const OPEN_HOUR = 6;
const CLOSE_HOUR = 22;
const HOURS = CLOSE_HOUR - OPEN_HOUR;
const PX_PER_HOUR = 24;
const TRACK_WIDTH = HOURS * PX_PER_HOUR;
const PX_PER_MINUTE = PX_PER_HOUR / 60;
const SNAP_MINUTES = 15;
const TOTAL_MINUTES = HOURS * 60;

const WEEKDAY_LABEL: Record<DayOfWeekName, string> = {
  Monday: "Mon",
  Tuesday: "Tue",
  Wednesday: "Wed",
  Thursday: "Thu",
  Friday: "Fri",
  Saturday: "Sat",
  Sunday: "Sun",
};

/** Minutes-from-OPEN_HOUR, the internal working unit for painting/merging. */
type MinuteSegment = { start: number; end: number };

/** Which edge of an existing segment is being dragged in resize mode. */
type ResizeEdge = "start" | "end";

/** Identifies the single segment (day + index) and edge currently being resized. */
type ResizeTarget = { day: DayOfWeekName; index: number; edge: ResizeEdge };

function timeToMinutes(time: string): number {
  const [h, m] = time.split(":").map(Number);
  return h * 60 + m - OPEN_HOUR * 60;
}

function minutesToTime(minutesFromOpen: number): string {
  const total = minutesFromOpen + OPEN_HOUR * 60;
  const h = Math.floor(total / 60);
  const m = total % 60;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:00`;
}

function snap(minutes: number): number {
  return Math.round(minutes / SNAP_MINUTES) * SNAP_MINUTES;
}

function clamp(n: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, n));
}

/** D-06 gap-as-break: overlapping/touching ranges collapse into one segment. */
function mergeSegments(segments: MinuteSegment[]): MinuteSegment[] {
  const sorted = [...segments].sort((a, b) => a.start - b.start);
  const merged: MinuteSegment[] = [];
  for (const seg of sorted) {
    const last = merged[merged.length - 1];
    if (last && seg.start <= last.end) {
      last.end = Math.max(last.end, seg.end);
    } else {
      merged.push({ ...seg });
    }
  }
  return merged;
}

function groupByDay(value: WorkingHoursSegment[]): Record<DayOfWeekName, MinuteSegment[]> {
  const byDay = WEEKDAYS.reduce((acc, day) => {
    acc[day] = [];
    return acc;
  }, {} as Record<DayOfWeekName, MinuteSegment[]>);
  for (const segment of value) {
    if (!byDay[segment.dayOfWeek]) continue;
    byDay[segment.dayOfWeek].push({
      start: timeToMinutes(segment.startTime),
      end: timeToMinutes(segment.endTime),
    });
  }
  return byDay;
}

type Props = {
  value: WorkingHoursSegment[];
  onChange: (segments: WorkingHoursSegment[]) => void;
  isLoading?: boolean;
};

/**
 * 7-row Mon-Sun weekly hours painter (D-05). Click-drag paints a segment
 * snapped to 15 minutes (BOOK-01 alignment); dragging again on the same row
 * adds a non-contiguous segment (D-06 gap-as-break). Controlled: the parent
 * owns `value` and receives the full replacement segment list on every edit.
 */
export function WeekStripEditor({ value, onChange, isLoading = false }: Props) {
  const trackRefs = useRef<Partial<Record<DayOfWeekName, HTMLDivElement>>>({});
  const [dragDay, setDragDay] = useState<DayOfWeekName | null>(null);
  const [previewRange, setPreviewRange] = useState<{ a: number; b: number }>({
    a: 0,
    b: 0,
  });
  const previewRangeRef = useRef<{ a: number; b: number }>({ a: 0, b: 0 });
  const [resizeTarget, setResizeTarget] = useState<ResizeTarget | null>(null);
  const [resizePreview, setResizePreview] = useState<number>(0);
  const resizeRef = useRef<{ target: ResizeTarget; value: number } | null>(null);

  const byDay = groupByDay(value);

  function posToMinutes(day: DayOfWeekName, clientX: number): number {
    const track = trackRefs.current[day];
    if (!track) return 0;
    const rect = track.getBoundingClientRect();
    const x = clamp(clientX - rect.left, 0, TRACK_WIDTH);
    return clamp(snap(x / PX_PER_MINUTE), 0, TOTAL_MINUTES);
  }

  function emitChange(day: DayOfWeekName, segments: MinuteSegment[]) {
    const rest = value.filter((s) => s.dayOfWeek !== day);
    const next = [
      ...rest,
      ...segments.map((s) => ({
        dayOfWeek: day,
        startTime: minutesToTime(s.start),
        endTime: minutesToTime(s.end),
      })),
    ].sort((a, b) => {
      const dayDiff = WEEKDAYS.indexOf(a.dayOfWeek) - WEEKDAYS.indexOf(b.dayOfWeek);
      return dayDiff !== 0 ? dayDiff : a.startTime.localeCompare(b.startTime);
    });
    onChange(next);
  }

  function removeSegment(day: DayOfWeekName, index: number) {
    const remaining = byDay[day].filter((_, i) => i !== index);
    emitChange(day, remaining);
  }

  function startResize(day: DayOfWeekName, index: number, edge: ResizeEdge) {
    const seg = byDay[day][index];
    const initial = edge === "start" ? seg.start : seg.end;
    resizeRef.current = { target: { day, index, edge }, value: initial };
    setResizeTarget({ day, index, edge });
    setResizePreview(initial);
  }

  useEffect(() => {
    if (!dragDay) return;

    function handleMove(e: PointerEvent) {
      if (!dragDay) return;
      const b = posToMinutes(dragDay, e.clientX);
      const next = { ...previewRangeRef.current, b };
      previewRangeRef.current = next;
      setPreviewRange(next);
    }

    function handleUp() {
      if (!dragDay) return;
      const { a, b } = previewRangeRef.current;
      const start = Math.min(a, b);
      const end = Math.max(a, b);
      if (end - start >= SNAP_MINUTES) {
        emitChange(dragDay, mergeSegments([...byDay[dragDay], { start, end }]));
      }
      setDragDay(null);
    }

    window.addEventListener("pointermove", handleMove);
    window.addEventListener("pointerup", handleUp);
    return () => {
      window.removeEventListener("pointermove", handleMove);
      window.removeEventListener("pointerup", handleUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dragDay]);

  useEffect(() => {
    if (!resizeTarget) return;

    function handleResizeMove(e: PointerEvent) {
      if (!resizeTarget) return;
      const dayList = byDay[resizeTarget.day];
      const seg = dayList[resizeTarget.index];
      const prev = dayList[resizeTarget.index - 1];
      const next = dayList[resizeTarget.index + 1];
      const raw = posToMinutes(resizeTarget.day, e.clientX);
      const value =
        resizeTarget.edge === "start"
          ? clamp(raw, prev ? prev.end : 0, seg.end - SNAP_MINUTES)
          : clamp(raw, seg.start + SNAP_MINUTES, next ? next.start : TOTAL_MINUTES);
      resizeRef.current = { target: resizeTarget, value };
      setResizePreview(value);
    }

    function handleResizeUp() {
      if (!resizeRef.current) {
        setResizeTarget(null);
        return;
      }
      const { target, value } = resizeRef.current;
      const updated = byDay[target.day].map((seg, i) => {
        if (i !== target.index) return seg;
        return target.edge === "start" ? { ...seg, start: value } : { ...seg, end: value };
      });
      emitChange(target.day, updated);
      setResizeTarget(null);
      resizeRef.current = null;
    }

    window.addEventListener("pointermove", handleResizeMove);
    window.addEventListener("pointerup", handleResizeUp);
    return () => {
      window.removeEventListener("pointermove", handleResizeMove);
      window.removeEventListener("pointerup", handleResizeUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resizeTarget]);

  return (
    <div>
      <p className="text-xs uppercase tracking-wider text-muted mb-3">
        Drag across a row to paint hours (06:00–22:00, 15-min snap). Drag again
        on the same day to add a break. Hover a segment and drag its edge to
        resize it.
      </p>
      <div className="overflow-x-auto">
        <div style={{ minWidth: TRACK_WIDTH + 56 }} className="flex flex-col gap-1.5">
          {WEEKDAYS.map((day) => {
            const segments = byDay[day];
            const showClosedOverlay = !isLoading && segments.length === 0 && dragDay !== day;

            return (
              <div key={day} className="flex items-center gap-2">
                <span className="w-10 shrink-0 text-xs uppercase tracking-wider text-muted">
                  {WEEKDAY_LABEL[day]}
                </span>
                <div
                  ref={(el) => {
                    trackRefs.current[day] = el ?? undefined;
                  }}
                  onPointerDown={(e) => {
                    if (isLoading) return;
                    const target = e.target as HTMLElement;
                    if (target.closest("[data-segment-remove]")) return;
                    const m = posToMinutes(day, e.clientX);
                    setDragDay(day);
                    const next = { a: m, b: m };
                    previewRangeRef.current = next;
                    setPreviewRange(next);
                    e.preventDefault();
                  }}
                  className={
                    isLoading
                      ? "relative h-10 rounded-lg border border-border bg-surface-alt animate-pulse"
                      : "relative h-10 rounded-lg border border-border bg-surface cursor-crosshair select-none"
                  }
                  style={{ width: TRACK_WIDTH }}
                >
                  {showClosedOverlay ? (
                    <span className="absolute inset-0 flex items-center justify-center text-xs uppercase tracking-wider text-muted">
                      Closed
                    </span>
                  ) : null}

                  {!isLoading &&
                    segments.map((seg, i) => {
                      const effectiveStart =
                        resizeTarget &&
                        resizeTarget.day === day &&
                        resizeTarget.index === i &&
                        resizeTarget.edge === "start"
                          ? resizePreview
                          : seg.start;
                      const effectiveEnd =
                        resizeTarget &&
                        resizeTarget.day === day &&
                        resizeTarget.index === i &&
                        resizeTarget.edge === "end"
                          ? resizePreview
                          : seg.end;

                      return (
                        <div
                          key={`${seg.start}-${seg.end}`}
                          className="group absolute top-0 bottom-0 bg-gold-dark/15 border border-gold-dark rounded-md"
                          style={{
                            left: effectiveStart * PX_PER_MINUTE,
                            width: Math.max(2, (effectiveEnd - effectiveStart) * PX_PER_MINUTE),
                          }}
                        >
                          <div
                            data-segment-resize="start"
                            onPointerDown={(e) => {
                              e.stopPropagation();
                              e.preventDefault();
                              startResize(day, i, "start");
                            }}
                            aria-label={`Resize ${WEEKDAY_LABEL[day]} segment start`}
                            className="hidden group-hover:block absolute inset-y-0 -left-1 w-2 cursor-ew-resize"
                          />
                          <div
                            data-segment-resize="end"
                            onPointerDown={(e) => {
                              e.stopPropagation();
                              e.preventDefault();
                              startResize(day, i, "end");
                            }}
                            aria-label={`Resize ${WEEKDAY_LABEL[day]} segment end`}
                            className="hidden group-hover:block absolute inset-y-0 -right-1 w-2 cursor-ew-resize"
                          />
                          <button
                            type="button"
                            data-segment-remove
                            onPointerDown={(e) => e.stopPropagation()}
                            onClick={() => removeSegment(day, i)}
                            aria-label={`Remove ${WEEKDAY_LABEL[day]} segment`}
                            className="hidden group-hover:flex absolute -top-2 -right-2 h-5 w-5 items-center justify-center rounded-full bg-gold-dark text-white text-xs leading-none focus:outline-none focus:ring-2 focus:ring-gold-dark"
                          >
                            ×
                          </button>
                        </div>
                      );
                    })}

                  {!isLoading && dragDay === day ? (
                    <div
                      className="absolute top-0 bottom-0 bg-gold-dark/25 border border-dashed border-gold-dark rounded-md pointer-events-none"
                      style={{
                        left: Math.min(previewRange.a, previewRange.b) * PX_PER_MINUTE,
                        width: Math.max(
                          2,
                          Math.abs(previewRange.b - previewRange.a) * PX_PER_MINUTE
                        ),
                      }}
                    />
                  ) : null}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
