"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api/client";
import {
  clearSession,
  getSession,
  requireAuth,
  type AuthSession,
} from "@/lib/auth";
import {
  addDays,
  dayWindow,
  todayDateOnly,
  weekWindow,
} from "@/lib/scheduleTime";
import { useSchedule, type AppointmentResponseDto } from "@/lib/useSchedule";
import {
  updateAppointmentStatus,
  type ScheduleStatusAction,
} from "@/lib/scheduleStatus";
import type { ConfirmableAction } from "@/components/AppointmentBlock";
import { DayGrid, type StylistColumn } from "@/components/DayGrid";
import { WeekChips } from "@/components/WeekChips";
import { AppointmentDetailPanel } from "@/components/AppointmentDetailPanel";
import { ConfirmDialog, CONFIRM_COPY } from "@/components/ConfirmDialog";
import {
  ScheduleToolbar,
  type ScheduleMode,
} from "@/components/ScheduleToolbar";
import { LogOutIcon } from "@/components/icons";

type PendingConfirm = {
  appointment: AppointmentResponseDto;
  action: ConfirmableAction;
};

export default function SchedulePage() {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);
  const [date, setDate] = useState(todayDateOnly);
  const [mode, setMode] = useState<ScheduleMode>("day");
  const [includeCancelled, setIncludeCancelled] = useState(false);
  const [stylists, setStylists] = useState<StylistColumn[]>([]);
  const [detail, setDetail] = useState<AppointmentResponseDto | null>(null);
  const [pending, setPending] = useState<PendingConfirm | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!requireAuth()) return;
    setSession(getSession());
    setReady(true);
  }, []);

  useEffect(() => {
    if (!ready) return;
    let cancelled = false;
    (async () => {
      const { data, response } = await api.GET("/api/Stylists");
      if (cancelled || !response.ok || !data) return;
      setStylists(
        data
          .map((s) => ({
            id: Number(s.id),
            name: s.name ?? `Stylist ${s.id}`,
          }))
          .filter((s) => Number.isFinite(s.id))
          .sort((a, b) => a.name.localeCompare(b.name))
      );
    })();
    return () => {
      cancelled = true;
    };
  }, [ready]);

  const range = useMemo(
    () => (mode === "day" ? dayWindow(date) : weekWindow(date)),
    [date, mode]
  );

  const { appointments, isLoading, error, mutate, lastUpdatedAt } = useSchedule({
    from: range.from,
    to: range.to,
    includeCancelled,
  });

  const stepDays = mode === "day" ? 1 : 7;

  const handleComplete = useCallback(
    async (appointment: AppointmentResponseDto) => {
      if (appointment.id == null) return;
      setBusy(true);
      setActionError(null);
      try {
        await updateAppointmentStatus(appointment.id, "Completed");
        await mutate();
        setDetail(null);
      } catch (err) {
        setActionError(
          err instanceof Error ? err.message : "Could not update status."
        );
      } finally {
        setBusy(false);
      }
    },
    [mutate]
  );

  const handleRequestConfirm = useCallback(
    (appointment: AppointmentResponseDto, action: ConfirmableAction) => {
      setPending({ appointment, action });
    },
    []
  );

  const handleConfirm = useCallback(async () => {
    if (!pending || pending.appointment.id == null) return;
    const status: ScheduleStatusAction = pending.action;
    setBusy(true);
    setActionError(null);
    try {
      await updateAppointmentStatus(pending.appointment.id, status);
      await mutate();
      setPending(null);
      setDetail(null);
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : "Could not update status."
      );
    } finally {
      setBusy(false);
    }
  }, [pending, mutate]);

  function handleLogout() {
    clearSession();
    window.location.assign("/login");
  }

  if (!ready) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-surface text-muted text-sm">
        Loading…
      </main>
    );
  }

  const confirmCopy = pending ? CONFIRM_COPY[pending.action] : null;
  const isOwner = session?.role === "Owner";

  return (
    <main className="min-h-screen bg-surface text-ink">
      <header className="border-b border-border bg-surface-alt px-4 md:px-6 py-3 flex flex-wrap items-center gap-3 justify-between">
        <h1 className="font-serif text-2xl font-semibold tracking-tight">
          Zach Hair Studio
        </h1>
        <div className="flex items-center gap-3">
          <p className="text-sm text-muted">
            {session?.displayName}
            {session?.role ? ` · ${session.role}` : ""}
          </p>
          {isOwner ? (
            <Link
              href="/staff/new"
              className="min-h-11 inline-flex items-center px-3 rounded-xl border border-border text-sm text-ink hover:border-gold-dark/40"
            >
              Add staff
            </Link>
          ) : null}
          <button
            type="button"
            onClick={handleLogout}
            aria-label="Log out"
            className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink"
          >
            <LogOutIcon className="h-5 w-5" />
          </button>
        </div>
      </header>

      <ScheduleToolbar
        date={date}
        mode={mode}
        includeCancelled={includeCancelled}
        lastUpdatedAt={lastUpdatedAt}
        onPrev={() => setDate((d) => addDays(d, -stepDays))}
        onNext={() => setDate((d) => addDays(d, stepDays))}
        onToday={() => setDate(todayDateOnly())}
        onDateChange={setDate}
        onModeChange={setMode}
        onIncludeCancelledChange={setIncludeCancelled}
        onRefresh={() => {
          void mutate();
        }}
      />

      {actionError ? (
        <p
          role="alert"
          className="mx-4 md:mx-6 mt-4 text-sm text-rose-600 bg-rose-600/5 border border-rose-600/20 rounded-xl px-3 py-2"
        >
          {actionError}
        </p>
      ) : null}

      {isLoading && appointments.length === 0 ? (
        <p className="px-6 py-12 text-sm text-muted">Loading schedule…</p>
      ) : null}

      {error && appointments.length === 0 ? (
        <div className="px-6 py-12 max-w-lg">
          <h2 className="text-lg font-semibold text-ink">
            Couldn&apos;t Load the Schedule.
          </h2>
          <p className="text-sm text-muted mt-2">
            We couldn&apos;t reach the booking system. Try refreshing, or check
            your connection.
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
      ) : null}

      {!error || appointments.length > 0 ? (
        mode === "day" ? (
          <DayGrid
            appointments={appointments}
            stylists={stylists}
            includeCancelled={includeCancelled}
            onOpenDetail={setDetail}
            onComplete={handleComplete}
            onRequestConfirm={handleRequestConfirm}
          />
        ) : (
          <WeekChips
            appointments={appointments}
            weekOf={date}
            includeCancelled={includeCancelled}
            onOpenDay={(d) => {
              setDate(d);
              setMode("day");
            }}
            onOpenDetail={setDetail}
          />
        )
      ) : null}

      <AppointmentDetailPanel
        appointment={detail}
        onClose={() => setDetail(null)}
        onComplete={handleComplete}
        onRequestConfirm={handleRequestConfirm}
        busy={busy}
      />

      <ConfirmDialog
        open={Boolean(pending && confirmCopy)}
        title={confirmCopy?.title ?? ""}
        body={confirmCopy?.body ?? ""}
        confirmLabel={confirmCopy?.confirmLabel ?? "Confirm"}
        onConfirm={() => {
          void handleConfirm();
        }}
        onCancel={() => setPending(null)}
        busy={busy}
      />
    </main>
  );
}
