"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import type { Service } from "@/lib/services";
import {
  AppointmentApiError,
  createAppointment,
  fetchOpenSlots,
  type AppointmentResponse,
  type OpenSlot,
  type Stylist,
} from "@/lib/appointments";
import { AlertIcon, CheckIcon, ClockIcon } from "./icons";

// Salon operating zone. Every appointment time is rendered in THIS zone using an
// explicit IANA timeZone — never the browser's local zone (D-16). The API hands us
// a DateTimeOffset carrying the salon offset; formatting with an explicit zone keeps
// the wall-clock correct for every viewer regardless of their machine timezone.
const SALON_TIME_ZONE = "America/New_York";
const SALON_ZONE_CAPTION = "All times shown in salon local time (Eastern)";

// Owner-reviewable booking window (mirrors AppointmentCreateDtoValidator): same-day
// through 60 days ahead, no minimum lead time. Flagged for owner review in the SUMMARY.
const BOOKING_HORIZON_DAYS = 60;

const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-3 text-white placeholder-gray-600 text-sm outline-none transition-colors";

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

const confirmTimeFormatter = new Intl.DateTimeFormat("en-US", {
  hour: "numeric",
  minute: "2-digit",
  timeZoneName: "short",
  timeZone: SALON_TIME_ZONE,
});

const confirmDateFormatter = new Intl.DateTimeFormat("en-US", {
  weekday: "long",
  month: "long",
  day: "numeric",
  year: "numeric",
  timeZone: SALON_TIME_ZONE,
});

/** Local YYYY-MM-DD for a date offset by `days` from today (for the date input bounds). */
function isoDateOffsetFromToday(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

type Props = {
  services: Service[];
  stylists: Stylist[];
  initialServiceSlug?: string;
};

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="text-gray-400 text-xs uppercase tracking-wider block mb-2">
        {label}
      </label>
      {children}
    </div>
  );
}

function StepPanel({
  heading,
  children,
  panelRef,
}: {
  heading: string;
  children: React.ReactNode;
  panelRef?: React.Ref<HTMLDivElement>;
}) {
  return (
    <div
      ref={panelRef}
      className="bg-charcoal-light border border-white/5 rounded-2xl p-6 transition-all duration-300"
    >
      <h2 className="text-white text-xl font-semibold mb-4">{heading}</h2>
      {children}
    </div>
  );
}

export default function AppointmentBookingForm({
  services,
  stylists,
  initialServiceSlug,
}: Props) {
  const servicesBySlug = useMemo(
    () => new Map(services.map((service) => [service.slug, service])),
    [services]
  );

  const [selectedSlug, setSelectedSlug] = useState(
    initialServiceSlug && servicesBySlug.has(initialServiceSlug)
      ? initialServiceSlug
      : ""
  );
  // null = "Any Available Stylist" (default, D-07).
  const [selectedStylistId, setSelectedStylistId] = useState<number | null>(
    null
  );
  const [selectedDate, setSelectedDate] = useState("");
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);

  const [slots, setSlots] = useState<OpenSlot[] | null>(null);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsFailed, setSlotsFailed] = useState(false);
  const [unavailableSlots, setUnavailableSlots] = useState<Set<string>>(
    () => new Set()
  );
  // Re-fetch trigger, bumped on 409 recovery and the "Try Again" retry.
  const [reloadKey, setReloadKey] = useState(0);

  // Contact details are controlled state so a 409 recovery never clears them.
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [confirmation, setConfirmation] = useState<AppointmentResponse | null>(
    null
  );

  const dateStepRef = useRef<HTMLDivElement>(null);

  const selectedService = selectedSlug
    ? servicesBySlug.get(selectedSlug) ?? null
    : null;
  const serviceId = selectedService?.id ?? null;

  const minDate = useMemo(() => isoDateOffsetFromToday(0), []);
  const maxDate = useMemo(
    () => isoDateOffsetFromToday(BOOKING_HORIZON_DAYS),
    []
  );

  // Fetch fresh slots whenever the service / stylist / date changes. Distinguishes a
  // successful empty day ([]) from a load failure (slotsFailed) — the UI has two
  // different states for these (D-15).
  useEffect(() => {
    if (serviceId == null || !selectedDate) {
      setSlots(null);
      setSlotsFailed(false);
      setSlotsLoading(false);
      return;
    }

    let cancelled = false;
    setSlotsLoading(true);
    setSlotsFailed(false);

    fetchOpenSlots(serviceId, selectedStylistId, selectedDate)
      .then((result) => {
        if (cancelled) return;
        setSlots(result);
        setSlotsFailed(false);
      })
      .catch(() => {
        if (cancelled) return;
        setSlots(null);
        setSlotsFailed(true);
      })
      .finally(() => {
        if (!cancelled) setSlotsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [serviceId, selectedStylistId, selectedDate, reloadKey]);

  function handleServiceChange(slug: string) {
    setSelectedSlug(slug);
    // Changing an earlier step resets later selections.
    setSelectedStylistId(null);
    setSelectedSlot(null);
    setConflict(false);
    setSubmitError(null);
    setUnavailableSlots(new Set());
  }

  function handleStylistChange(stylistId: number | null) {
    setSelectedStylistId(stylistId);
    setSelectedSlot(null);
    setConflict(false);
    setUnavailableSlots(new Set());
  }

  function handleDateChange(date: string) {
    setSelectedDate(date);
    setSelectedSlot(null);
    setConflict(false);
    setUnavailableSlots(new Set());
  }

  const canSubmit =
    serviceId != null &&
    selectedSlot != null &&
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    email.trim().length > 0 &&
    !submitting;

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (serviceId == null || selectedSlot == null) return;

    setSubmitting(true);
    setSubmitError(null);
    setConflict(false);

    try {
      const result = await createAppointment({
        serviceId,
        stylistId: selectedStylistId,
        startsAt: selectedSlot,
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        phone: phone.trim() || undefined,
      });
      setConfirmation(result);
    } catch (err) {
      if (err instanceof AppointmentApiError && err.isConflict) {
        // 409 recovery: keep the client's details, return focus to the slot step,
        // mark the taken slot unavailable, and re-fetch the grid for this date.
        const taken = selectedSlot;
        setConflict(true);
        setUnavailableSlots((prev) => new Set(prev).add(taken));
        setSelectedSlot(null);
        setReloadKey((k) => k + 1);
        requestAnimationFrame(() => {
          dateStepRef.current?.scrollIntoView({
            behavior: "smooth",
            block: "start",
          });
        });
      } else {
        setSubmitError(
          err instanceof Error
            ? err.message
            : "Something went wrong. Please try again."
        );
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (confirmation) {
    const stylistLabel = confirmation.stylistName;
    return (
      <div className="bg-charcoal-light border border-white/5 rounded-2xl p-8 text-center">
        <div className="w-16 h-16 bg-gold/20 rounded-full flex items-center justify-center mx-auto mb-4">
          <CheckIcon className="w-8 h-8 text-gold" />
        </div>
        <h2 className="text-white text-xl font-semibold mb-2">
          You&apos;re All Set!
        </h2>
        <p className="text-gray-400 text-sm mb-6 max-w-md mx-auto">
          Your appointment is confirmed. A confirmation email is on its way — but
          everything you need is right here.
        </p>

        <dl className="text-left max-w-md mx-auto space-y-3 bg-charcoal border border-white/5 rounded-xl p-6">
          <div className="flex justify-between gap-4">
            <dt className="text-gray-400 text-sm">Service</dt>
            <dd className="text-white text-sm text-right">
              {confirmation.serviceName}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-gray-400 text-sm">Stylist</dt>
            <dd className="text-white text-sm text-right">{stylistLabel}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-gray-400 text-sm">Date</dt>
            <dd className="text-white text-sm text-right">
              {confirmDateFormatter.format(new Date(confirmation.startsAt))}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-gray-400 text-sm">Time</dt>
            <dd className="text-white text-sm text-right">
              {confirmTimeFormatter.format(new Date(confirmation.startsAt))}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-gray-400 text-sm">Duration</dt>
            <dd className="text-white text-sm text-right">
              {confirmation.durationMinutes} min
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-gray-400 text-sm">Price</dt>
            <dd className="text-gold font-bold text-sm text-right">
              {priceFormatter.format(confirmation.price)}
            </dd>
          </div>
        </dl>

        <p className="text-gray-500 text-xs mt-4">
          Save or screenshot this confirmation — email delivery isn&apos;t
          guaranteed.
        </p>
      </div>
    );
  }

  const serviceChosen = serviceId != null;
  const slotChosen = selectedSlot != null;

  return (
    <form className="space-y-6" onSubmit={handleSubmit}>
      {/* Step 1 — Choose a Service */}
      <StepPanel heading="1. Choose a Service">
        <select
          name="service"
          required
          value={selectedSlug}
          onChange={(event) => handleServiceChange(event.target.value)}
          className={`${inputClass} appearance-none cursor-pointer`}
        >
          <option value="" disabled className="bg-charcoal">
            Select a service...
          </option>
          {services.map((service) => (
            <option key={service.slug} value={service.slug} className="bg-charcoal">
              {service.name} - {priceFormatter.format(service.price)}
            </option>
          ))}
        </select>
      </StepPanel>

      {/* Step 2 — Choose a Stylist */}
      {serviceChosen && (
        <StepPanel heading="2. Choose a Stylist">
          <div className="flex flex-wrap gap-3">
            <button
              type="button"
              onClick={() => handleStylistChange(null)}
              className={`rounded-full border px-5 py-2.5 text-sm transition-colors min-h-11 flex items-center gap-2 ${
                selectedStylistId === null
                  ? "border-gold text-gold bg-gold/10"
                  : "border-white/10 text-gray-300 hover:border-gold/30"
              }`}
            >
              {selectedStylistId === null && <CheckIcon className="w-4 h-4" />}
              Any Available Stylist
            </button>
            {stylists.map((stylist) => (
              <button
                key={stylist.id}
                type="button"
                onClick={() => handleStylistChange(stylist.id)}
                className={`rounded-full border px-5 py-2.5 text-sm transition-colors min-h-11 flex items-center gap-2 ${
                  selectedStylistId === stylist.id
                    ? "border-gold text-gold bg-gold/10"
                    : "border-white/10 text-gray-300 hover:border-gold/30"
                }`}
              >
                {selectedStylistId === stylist.id && (
                  <CheckIcon className="w-4 h-4" />
                )}
                {stylist.name}
              </button>
            ))}
          </div>
        </StepPanel>
      )}

      {/* Step 3 — Pick a Date & Time */}
      {serviceChosen && (
        <StepPanel heading="3. Pick a Date & Time" panelRef={dateStepRef}>
          <Field label="Date">
            <input
              type="date"
              name="date"
              value={selectedDate}
              min={minDate}
              max={maxDate}
              onChange={(event) => handleDateChange(event.target.value)}
              className={`${inputClass} [color-scheme:dark]`}
            />
          </Field>

          {conflict && (
            <div
              role="alert"
              className="mt-4 flex items-start gap-2 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
            >
              <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
              <span>
                <strong className="font-semibold">
                  Slot taken — pick another time.
                </strong>{" "}
                Someone booked this exact time moments ago. Your other details are
                still filled in — just choose a new slot below.
              </span>
            </div>
          )}

          <div className="mt-4">
            {!selectedDate ? (
              <p className="text-gray-500 text-sm">
                Pick a date above to see open times.
              </p>
            ) : slotsLoading ? (
              <div>
                <p className="text-gray-400 text-sm mb-4">Finding open times…</p>
                <div className="grid grid-cols-3 sm:grid-cols-4 gap-4">
                  {Array.from({ length: 8 }).map((_, i) => (
                    <div
                      key={i}
                      className="min-h-11 rounded-lg bg-white/5 animate-pulse"
                    />
                  ))}
                </div>
              </div>
            ) : slotsFailed ? (
              <div className="text-center py-6">
                <h3 className="text-white text-xl font-semibold mb-2">
                  Couldn&apos;t Load Times.
                </h3>
                <p className="text-gray-400 text-sm mb-4 max-w-sm mx-auto">
                  We couldn&apos;t reach the booking system. Please try again, or
                  call the studio to book directly.
                </p>
                <button
                  type="button"
                  onClick={() => setReloadKey((k) => k + 1)}
                  className="border border-white/10 hover:border-gold/30 text-white text-sm rounded-xl px-5 py-2.5 transition-colors"
                >
                  Try Again
                </button>
              </div>
            ) : slots && slots.length === 0 ? (
              <div className="text-center py-6">
                <h3 className="text-white text-xl font-semibold mb-2">
                  No Openings This Day
                </h3>
                <p className="text-gray-400 text-sm max-w-sm mx-auto">
                  This date is fully booked for the service and stylist you picked.
                  Try another date, or switch to{" "}
                  <strong className="font-semibold">Any Available Stylist</strong>{" "}
                  to see more openings.
                </p>
              </div>
            ) : slots ? (
              <div>
                <div className="grid grid-cols-3 sm:grid-cols-4 gap-4">
                  {slots.map((slot) => {
                    const taken = unavailableSlots.has(slot.startsAt);
                    const isSelected = selectedSlot === slot.startsAt;
                    return (
                      <button
                        key={slot.startsAt}
                        type="button"
                        disabled={taken}
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
                <p className="text-gray-500 text-xs mt-3">
                  {SALON_ZONE_CAPTION}
                </p>
              </div>
            ) : null}
          </div>
        </StepPanel>
      )}

      {/* Step 4 — Your Details */}
      {slotChosen && (
        <StepPanel heading="4. Your Details">
          <div className="space-y-5">
            <div className="grid sm:grid-cols-2 gap-5">
              <Field label="First Name">
                <input
                  type="text"
                  name="firstName"
                  placeholder="Zach"
                  required
                  value={firstName}
                  onChange={(e) => setFirstName(e.target.value)}
                  className={inputClass}
                />
              </Field>
              <Field label="Last Name">
                <input
                  type="text"
                  name="lastName"
                  placeholder="Monroe"
                  required
                  value={lastName}
                  onChange={(e) => setLastName(e.target.value)}
                  className={inputClass}
                />
              </Field>
            </div>

            <Field label="Email Address">
              <input
                type="email"
                name="email"
                placeholder="you@example.com"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className={inputClass}
              />
            </Field>

            <Field label="Phone Number">
              <input
                type="tel"
                name="phone"
                placeholder="(212) 555-0000"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                className={inputClass}
              />
            </Field>

            {submitError && (
              <p
                role="alert"
                className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
              >
                {submitError}
              </p>
            )}

            <button
              type="submit"
              disabled={!canSubmit}
              className="w-full bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm uppercase tracking-wider py-4 rounded-xl transition-all duration-300 hover:shadow-xl hover:shadow-gold/30 flex items-center justify-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:shadow-none"
            >
              <ClockIcon className="w-4 h-4" />
              <span>{submitting ? "Confirming…" : "Confirm Appointment"}</span>
            </button>
          </div>
        </StepPanel>
      )}
    </form>
  );
}
