"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AccountApiError,
  cancelBooking,
  rescheduleBooking,
  type AccountBooking,
} from "@/lib/account";
import { fetchOpenSlots, type OpenSlot } from "@/lib/appointments";

const slotTimeFormatter = new Intl.DateTimeFormat("en-US", {
  hour: "numeric",
  minute: "2-digit",
});

function toIsoDate(startsAt: string): string {
  const d = new Date(startsAt);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function isoDateOffsetFromToday(days: number): string {
  const d = new Date();
  d.setHours(12, 0, 0, 0);
  d.setDate(d.getDate() + days);
  return toIsoDate(d.toISOString());
}

type Panel = "none" | "cancel" | "reschedule";

type Props = {
  booking: AccountBooking;
  onCancelled: (updated: AccountBooking) => void;
  onRescheduled: (oldId: number, updated: AccountBooking) => void;
};

/**
 * Cancel confirm + Reschedule slot flow for upcoming Confirmed account bookings
 * (ACCT-04 / D-09–D-11, UI-SPEC). Past and terminal rows must not mount this.
 */
export default function AccountBookingActions({
  booking,
  onCancelled,
  onRescheduled,
}: Props) {
  const [panel, setPanel] = useState<Panel>("none");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const [selectedDate, setSelectedDate] = useState(() => toIsoDate(booking.startsAt));
  const [slots, setSlots] = useState<OpenSlot[] | null>(null);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsFailed, setSlotsFailed] = useState(false);
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);
  const [unavailableSlots, setUnavailableSlots] = useState<Set<string>>(
    () => new Set()
  );
  const [reloadKey, setReloadKey] = useState(0);

  const minDate = useMemo(() => isoDateOffsetFromToday(0), []);
  const maxDate = useMemo(() => isoDateOffsetFromToday(60), []);

  const loadSlots = useCallback(async () => {
    setSlotsLoading(true);
    setSlotsFailed(false);
    try {
      const result = await fetchOpenSlots(
        booking.serviceId,
        booking.stylistId,
        selectedDate
      );
      setSlots(result);
    } catch {
      setSlots(null);
      setSlotsFailed(true);
    } finally {
      setSlotsLoading(false);
    }
  }, [booking.serviceId, booking.stylistId, selectedDate, reloadKey]);

  useEffect(() => {
    if (panel !== "reschedule") return;
    void loadSlots();
  }, [panel, loadSlots]);

  function openCancel() {
    setPanel("cancel");
    setError(null);
    setSuccess(null);
    setConflict(false);
  }

  function openReschedule() {
    setPanel("reschedule");
    setError(null);
    setSuccess(null);
    setConflict(false);
    setSelectedSlot(null);
    setUnavailableSlots(new Set());
    setSelectedDate(toIsoDate(booking.startsAt));
  }

  function dismiss() {
    if (busy) return;
    setPanel("none");
    setError(null);
    setConflict(false);
  }

  async function handleConfirmCancel() {
    setBusy(true);
    setError(null);
    try {
      const updated = await cancelBooking(booking.id);
      setSuccess("Appointment cancelled.");
      setPanel("none");
      onCancelled(updated);
    } catch (err) {
      setError(
        err instanceof AccountApiError
          ? err.message
          : "Something went wrong. Please try again."
      );
    } finally {
      setBusy(false);
    }
  }

  async function handleConfirmReschedule() {
    if (!selectedSlot) return;
    setBusy(true);
    setError(null);
    setConflict(false);
    try {
      const updated = await rescheduleBooking(booking.id, {
        startsAt: selectedSlot,
        stylistId: booking.stylistId,
      });
      setSuccess("You're rescheduled.");
      setPanel("none");
      onRescheduled(booking.id, updated);
    } catch (err) {
      if (err instanceof AccountApiError && err.isConflict) {
        const taken = selectedSlot;
        setConflict(true);
        setUnavailableSlots((prev) => new Set(prev).add(taken));
        setSelectedSlot(null);
        setReloadKey((k) => k + 1);
      } else {
        setError(
          err instanceof AccountApiError
            ? err.message
            : "Something went wrong. Please try again."
        );
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-4 space-y-3">
      {success ? (
        <p className="text-sm text-gold" role="status">
          {success}
        </p>
      ) : null}

      {panel === "none" ? (
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            onClick={openReschedule}
            className="text-sm font-semibold text-gold hover:text-gold-dark min-h-11 px-2 transition-colors"
          >
            Reschedule
          </button>
          <button
            type="button"
            onClick={openCancel}
            className="text-sm text-gray-400 hover:text-white min-h-11 px-2 transition-colors"
          >
            Cancel
          </button>
        </div>
      ) : null}

      {panel === "cancel" ? (
        <div className="rounded-xl border border-white/10 bg-charcoal p-4 space-y-3">
          <h3 className="text-white font-semibold">Cancel this appointment?</h3>
          <p className="text-sm text-gray-400">
            This frees your time slot. You can book again anytime.
          </p>
          {error ? (
            <p
              role="alert"
              className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
            >
              {error}
            </p>
          ) : null}
          <div className="flex flex-wrap gap-3">
            <button
              type="button"
              disabled={busy}
              onClick={() => void handleConfirmCancel()}
              className="min-h-11 rounded-full px-5 text-sm font-semibold text-rose-400 bg-rose-500/10 border border-rose-500/20 hover:bg-rose-500/20 disabled:opacity-50 transition-colors"
            >
              {busy ? "Cancelling…" : "Yes, Cancel"}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={dismiss}
              className="min-h-11 rounded-full px-5 text-sm text-gray-400 hover:text-white disabled:opacity-50 transition-colors"
            >
              Keep Appointment
            </button>
          </div>
        </div>
      ) : null}

      {panel === "reschedule" ? (
        <div className="rounded-xl border border-white/10 bg-charcoal p-4 space-y-4">
          <div>
            <h3 className="text-white font-semibold">Reschedule Appointment</h3>
            <p className="text-sm text-gray-400 mt-1">
              Pick a new open time. Your current booking is held until you confirm.
            </p>
          </div>

          <label className="block space-y-2">
            <span className="text-xs uppercase tracking-wider text-gray-500">
              Date
            </span>
            <input
              type="date"
              min={minDate}
              max={maxDate}
              value={selectedDate}
              disabled={busy}
              onChange={(e) => {
                setSelectedDate(e.target.value);
                setSelectedSlot(null);
                setConflict(false);
                setUnavailableSlots(new Set());
              }}
              className="w-full max-w-xs bg-charcoal-light border border-white/10 focus:border-gold rounded-xl px-4 py-3 text-white text-sm outline-none transition-colors min-h-11"
            />
          </label>

          {conflict ? (
            <p
              role="alert"
              className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
            >
              That time was just taken. Please pick another open slot.
            </p>
          ) : null}

          {error ? (
            <p
              role="alert"
              className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
            >
              {error}
            </p>
          ) : null}

          {slotsLoading ? (
            <p className="text-sm text-gray-500">Loading open times…</p>
          ) : null}

          {slotsFailed ? (
            <p className="text-sm text-rose-400">
              Couldn&apos;t load times. Try another date.
            </p>
          ) : null}

          {!slotsLoading && !slotsFailed && slots && slots.length === 0 ? (
            <p className="text-sm text-gray-400">
              No openings this day. Try another date.
            </p>
          ) : null}

          {!slotsLoading && slots && slots.length > 0 ? (
            <div className="grid grid-cols-3 sm:grid-cols-4 gap-3">
              {slots.map((slot) => {
                const taken = unavailableSlots.has(slot.startsAt);
                const isSelected = selectedSlot === slot.startsAt;
                return (
                  <button
                    key={slot.startsAt}
                    type="button"
                    disabled={taken || busy}
                    onClick={() => {
                      setSelectedSlot(slot.startsAt);
                      setConflict(false);
                    }}
                    className={`min-h-11 rounded-lg border text-sm transition-colors px-2 ${
                      isSelected
                        ? "bg-gold text-charcoal font-semibold border-gold"
                        : taken
                          ? "border-white/10 text-gray-500 line-through opacity-40 cursor-not-allowed"
                          : "border-white/10 text-white hover:border-gold/30"
                    }`}
                  >
                    {slotTimeFormatter.format(new Date(slot.startsAt))}
                  </button>
                );
              })}
            </div>
          ) : null}

          <div className="flex flex-wrap gap-3">
            <button
              type="button"
              disabled={busy || !selectedSlot}
              onClick={() => void handleConfirmReschedule()}
              className="min-h-11 rounded-full px-6 text-sm font-semibold bg-gold hover:bg-gold-dark text-charcoal disabled:opacity-50 transition-colors"
            >
              {busy ? "Rescheduling…" : "Confirm New Time"}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={dismiss}
              className="min-h-11 rounded-full px-5 text-sm text-gray-400 hover:text-white disabled:opacity-50 transition-colors"
            >
              Keep Current Time
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

/** Upcoming Confirmed only — server still enforces until-start (D-11). */
export function isUpcomingConfirmed(booking: AccountBooking, now = new Date()): boolean {
  if (booking.status.toLowerCase() !== "confirmed") return false;
  return new Date(booking.startsAt).getTime() > now.getTime();
}
