"use client";

import { ChevronLeftIcon, ChevronRightIcon, RefreshIcon } from "@/components/icons";

export type ScheduleMode = "day" | "week";

type Props = {
  date: string;
  mode: ScheduleMode;
  includeCancelled: boolean;
  lastUpdatedAt: Date | null;
  onPrev: () => void;
  onNext: () => void;
  onToday: () => void;
  onDateChange: (dateOnly: string) => void;
  onModeChange: (mode: ScheduleMode) => void;
  onIncludeCancelledChange: (value: boolean) => void;
  onRefresh: () => void;
};

function freshnessCaption(lastUpdatedAt: Date | null): string {
  if (!lastUpdatedAt) return "Updated —";
  const mins = Math.floor((Date.now() - lastUpdatedAt.getTime()) / 60_000);
  if (mins < 1) return "Updated just now";
  if (mins === 1) return "Updated 1m ago";
  return `Updated ${mins}m ago`;
}

export function ScheduleToolbar({
  date,
  mode,
  includeCancelled,
  lastUpdatedAt,
  onPrev,
  onNext,
  onToday,
  onDateChange,
  onModeChange,
  onIncludeCancelledChange,
  onRefresh,
}: Props) {
  return (
    <div className="px-4 md:px-6 py-4 flex flex-wrap items-center gap-3 border-b border-border bg-surface">
      <div className="flex items-center gap-1">
        <button
          type="button"
          aria-label="Previous"
          onClick={onPrev}
          className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink hover:border-gold-dark/40"
        >
          <ChevronLeftIcon className="h-5 w-5" />
        </button>
        <button
          type="button"
          aria-label="Next"
          onClick={onNext}
          className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink hover:border-gold-dark/40"
        >
          <ChevronRightIcon className="h-5 w-5" />
        </button>
        <button
          type="button"
          onClick={onToday}
          className="min-h-11 px-4 rounded-xl bg-gold-dark text-white text-sm font-semibold hover:bg-gold"
        >
          Today
        </button>
      </div>

      <input
        type="date"
        value={date}
        onChange={(e) => {
          if (e.target.value) onDateChange(e.target.value);
        }}
        className="min-h-11 rounded-xl border border-border bg-surface px-3 text-sm text-ink [color-scheme:light] focus:border-gold-dark outline-none"
      />

      <div
        className="inline-flex rounded-xl border border-border overflow-hidden"
        role="group"
        aria-label="Day or week view"
      >
        <button
          type="button"
          onClick={() => onModeChange("day")}
          className={`min-h-11 min-w-11 px-4 text-sm ${
            mode === "day"
              ? "bg-gold-dark text-white font-semibold"
              : "bg-surface text-ink"
          }`}
        >
          Day
        </button>
        <button
          type="button"
          onClick={() => onModeChange("week")}
          className={`min-h-11 min-w-11 px-4 text-sm border-l border-border ${
            mode === "week"
              ? "bg-gold-dark text-white font-semibold"
              : "bg-surface text-ink"
          }`}
        >
          Week
        </button>
      </div>

      <label className="inline-flex items-center gap-2 min-h-11 text-sm text-ink cursor-pointer select-none">
        <input
          type="checkbox"
          checked={includeCancelled}
          onChange={(e) => onIncludeCancelledChange(e.target.checked)}
          className="h-4 w-4 accent-gold-dark"
        />
        Show cancelled &amp; no-shows
      </label>

      <div className="ml-auto flex items-center gap-2">
        <span className="text-xs text-muted whitespace-nowrap">
          {freshnessCaption(lastUpdatedAt)}
        </span>
        <button
          type="button"
          aria-label="Refresh schedule"
          onClick={onRefresh}
          className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink hover:border-gold-dark/40"
        >
          <RefreshIcon className="h-5 w-5" />
        </button>
      </div>
    </div>
  );
}
