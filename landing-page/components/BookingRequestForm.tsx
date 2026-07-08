"use client";

import { useMemo, useState } from "react";
import { createBooking } from "@/lib/api";
import type { Service } from "@/lib/services";
import { ArrowRightIcon } from "./icons";

const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-3 text-white placeholder-gray-600 text-sm outline-none transition-colors";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

type Props = {
  services: Service[];
  initialServiceSlug?: string;
};

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="text-gray-400 text-xs uppercase tracking-wider block mb-2">
        {label}
      </label>
      {children}
    </div>
  );
}

export default function BookingRequestForm({
  services,
  initialServiceSlug,
}: Props) {
  const serviceSlugs = useMemo(
    () => new Set(services.map((service) => service.slug)),
    [services]
  );
  const [selectedSlug, setSelectedSlug] = useState(
    initialServiceSlug && serviceSlugs.has(initialServiceSlug)
      ? initialServiceSlug
      : ""
  );
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    const form = new FormData(e.currentTarget);
    const serviceSlug = String(form.get("service") ?? "");
    const selectedService = services.find(
      (service) => service.slug === serviceSlug
    );
    const serviceLabel = selectedService
      ? `${selectedService.name} - ${priceFormatter.format(selectedService.price)}`
      : serviceSlug;

    try {
      await createBooking({
        firstName: String(form.get("firstName") ?? "").trim(),
        lastName: String(form.get("lastName") ?? "").trim(),
        email: String(form.get("email") ?? "").trim(),
        phone: String(form.get("phone") ?? "").trim() || undefined,
        service: serviceLabel,
        preferredDate: String(form.get("preferredDate") ?? ""),
        message: String(form.get("message") ?? "").trim() || undefined,
      });
      setSubmitted(true);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Something went wrong. Please try again."
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <div className="bg-charcoal border border-white/5 rounded-3xl p-8 text-center">
        <div className="w-16 h-16 bg-gold/20 rounded-full flex items-center justify-center mx-auto mb-4">
          <svg
            className="w-8 h-8 text-gold"
            fill="none"
            stroke="currentColor"
            strokeWidth={2}
            viewBox="0 0 24 24"
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
        <h2 className="text-white text-2xl font-serif mb-2">You&apos;re All Set!</h2>
        <p className="text-gray-400 text-sm">
          We&apos;ve received your request and will confirm your appointment
          within 24 hours.
        </p>
      </div>
    );
  }

  return (
    <form
      className="bg-charcoal border border-white/5 rounded-3xl p-8 space-y-5"
      onSubmit={handleSubmit}
    >
      <div className="grid sm:grid-cols-2 gap-5">
        <Field label="First Name">
          <input
            type="text"
            name="firstName"
            placeholder="Zach"
            required
            className={inputClass}
          />
        </Field>
        <Field label="Last Name">
          <input
            type="text"
            name="lastName"
            placeholder="Monroe"
            required
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
          className={inputClass}
        />
      </Field>

      <Field label="Phone Number">
        <input
          type="tel"
          name="phone"
          placeholder="(212) 555-0000"
          className={inputClass}
        />
      </Field>

      <Field label="Service">
        <select
          name="service"
          required
          value={selectedSlug}
          onChange={(event) => setSelectedSlug(event.target.value)}
          className={`${inputClass} appearance-none cursor-pointer`}
        >
          <option value="" disabled className="bg-charcoal">
            Select a service...
          </option>
          {services.map((service) => (
            <option
              key={service.slug}
              value={service.slug}
              className="bg-charcoal"
            >
              {service.name} - {priceFormatter.format(service.price)}
            </option>
          ))}
        </select>
      </Field>

      <Field label="Preferred Date">
        <input
          type="date"
          name="preferredDate"
          required
          className={`${inputClass} [color-scheme:dark]`}
        />
      </Field>

      <Field label="Message (Optional)">
        <textarea
          name="message"
          rows={4}
          placeholder="Tell us about your desired style or any special requests..."
          className={`${inputClass} resize-none`}
        />
      </Field>

      {error && (
        <p
          role="alert"
          className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
        >
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={submitting || services.length === 0}
        className="w-full bg-gold hover:bg-gold-dark text-charcoal font-bold text-sm uppercase tracking-wider py-4 rounded-xl transition-all duration-300 hover:shadow-xl hover:shadow-gold/30 flex items-center justify-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:shadow-none"
      >
        <span>{submitting ? "Sending..." : "Request Appointment"}</span>
        {!submitting && <ArrowRightIcon className="w-4 h-4" strokeWidth={2.5} />}
      </button>
    </form>
  );
}
