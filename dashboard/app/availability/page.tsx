"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { requireAuth } from "@/lib/auth";
import { DashboardNav } from "@/components/DashboardNav";
import { StylistPicker } from "@/components/StylistPicker";
import { WeekStripEditor } from "@/components/WeekStripEditor";
import { TimeOffCalendar } from "@/components/TimeOffCalendar";
import { ConflictList } from "@/components/ConflictList";
import {
  AvailabilityConflictError,
  saveAvailability,
  useAvailability,
  type AvailabilityConflict,
  type TimeOffRange,
  type WorkingHoursSegment,
} from "@/lib/useAvailability";

function timeOffIds(ranges: TimeOffRange[]): number[] {
  return ranges
    .map((r) => r.id)
    .filter((id): id is number => id != null);
}

export default function AvailabilityPage() {
  const [ready, setReady] = useState(false);
  const [stylistId, setStylistId] = useState<number | null>(null);

  const { hours, timeOff, isLoading, error, mutate } = useAvailability(stylistId);

  const [localHours, setLocalHours] = useState<WorkingHoursSegment[]>([]);
  const [localTimeOff, setLocalTimeOff] = useState<TimeOffRange[]>([]);
  const [originalTimeOffIds, setOriginalTimeOffIds] = useState<number[]>([]);
  const hydratedKeyRef = useRef<string | null>(null);

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [conflicts, setConflicts] = useState<AvailabilityConflict[]>([]);

  useEffect(() => {
    if (!requireAuth()) return;
    setReady(true);
  }, []);

  // Hydrate local editable state from the fetched availability once per
  // stylist selection — subsequent hook revalidations (e.g. background
  // refocus) never clobber in-progress edits; only an explicit Save Changes
  // re-hydrates (via the mutate() return value below).
  useEffect(() => {
    if (stylistId == null || isLoading) return;
    const key = String(stylistId);
    if (hydratedKeyRef.current === key) return;
    hydratedKeyRef.current = key;
    setLocalHours(hours);
    setLocalTimeOff(timeOff);
    setOriginalTimeOffIds(timeOffIds(timeOff));
  }, [stylistId, isLoading, hours, timeOff]);

  const handleStylistChange = useCallback((id: number) => {
    setStylistId(id);
    setSaveError(null);
    setSaveSuccess(false);
    setConflicts([]);
  }, []);

  const handleHoursChange = useCallback((segments: WorkingHoursSegment[]) => {
    setLocalHours(segments);
    setSaveSuccess(false);
    setConflicts([]);
  }, []);

  const handleTimeOffChange = useCallback((ranges: TimeOffRange[]) => {
    setLocalTimeOff(ranges);
    setSaveSuccess(false);
    setConflicts([]);
  }, []);

  const handleSave = useCallback(async () => {
    if (stylistId == null) return;
    setSaving(true);
    setSaveError(null);
    setSaveSuccess(false);
    setConflicts([]);
    try {
      await saveAvailability(stylistId, localHours, localTimeOff, originalTimeOffIds);
      const fresh = await mutate();
      if (fresh) {
        setLocalHours(fresh.hours);
        setLocalTimeOff(fresh.timeOff);
        setOriginalTimeOffIds(timeOffIds(fresh.timeOff));
      }
      setSaveSuccess(true);
    } catch (err) {
      if (err instanceof AvailabilityConflictError) {
        // Distinct from the generic network/500 banner (E7) — the rose
        // ConflictList panel below renders instead. Save Changes stays
        // enabled so staff can retry once conflicts are resolved elsewhere.
        setConflicts(err.conflicts);
      } else {
        setSaveError(
          err instanceof Error ? err.message : "Couldn't save availability. Try again."
        );
      }
    } finally {
      setSaving(false);
    }
  }, [stylistId, localHours, localTimeOff, originalTimeOffIds, mutate]);

  if (!ready) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-surface text-muted text-sm">
        Loading…
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-surface text-ink">
      <DashboardNav />

      <div className="px-4 md:px-6 py-6 max-w-4xl mx-auto flex flex-col gap-6">
        <h1 className="text-lg font-semibold text-ink">Availability</h1>

        <StylistPicker value={stylistId} onChange={handleStylistChange} />

        {stylistId != null ? (
          <>
            {error ? (
              <div className="max-w-lg">
                <h2 className="text-lg font-semibold text-ink">
                  Couldn&apos;t Load Availability.
                </h2>
                <p className="text-sm text-muted mt-2">
                  We couldn&apos;t reach the booking system. Try refreshing, or
                  check your connection.
                </p>
                <button
                  type="button"
                  onClick={() => {
                    void mutate();
                  }}
                  className="mt-4 min-h-11 px-4 rounded-xl bg-gold-dark text-white text-sm font-semibold"
                >
                  Refresh
                </button>
              </div>
            ) : (
              <>
                <section className="bg-surface-alt rounded-2xl p-6 border border-border">
                  <h2 className="text-lg font-semibold text-ink mb-4">
                    Weekly Hours
                  </h2>
                  <WeekStripEditor
                    value={localHours}
                    onChange={handleHoursChange}
                    isLoading={isLoading}
                  />
                </section>

                <section className="bg-surface-alt rounded-2xl p-6 border border-border">
                  <h2 className="text-lg font-semibold text-ink mb-4">Time Off</h2>
                  <TimeOffCalendar
                    value={localTimeOff}
                    onChange={handleTimeOffChange}
                    isLoading={isLoading}
                  />
                </section>

                <div className="flex flex-col gap-3">
                  <button
                    type="button"
                    disabled={saving || isLoading}
                    onClick={() => {
                      void handleSave();
                    }}
                    className="self-start min-h-11 px-6 rounded-xl bg-gold-dark text-white text-sm font-semibold disabled:opacity-60"
                  >
                    {saving ? "Saving…" : "Save Changes"}
                  </button>

                  {saveSuccess ? (
                    <p role="status" className="text-sm text-ink">
                      Availability saved.
                    </p>
                  ) : null}

                  <ConflictList conflicts={conflicts} />

                  {saveError ? (
                    <p
                      role="alert"
                      className="text-sm text-rose-600 bg-rose-600/5 border border-rose-600/20 rounded-xl px-3 py-2 max-w-md"
                    >
                      {saveError}
                    </p>
                  ) : null}
                </div>
              </>
            )}
          </>
        ) : null}
      </div>
    </main>
  );
}
