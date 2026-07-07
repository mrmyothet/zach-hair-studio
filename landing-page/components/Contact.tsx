"use client";

import { useState } from "react";
import { branches, contactEmail, serviceOptions } from "@/lib/data";
import { createBooking } from "@/lib/api";
import { ArrowRightIcon, MapPinIcon } from "./icons";

const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-3 text-white placeholder-gray-600 text-sm outline-none transition-colors";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="text-gray-400 text-xs uppercase tracking-wider block mb-2">{label}</label>
      {children}
    </div>
  );
}

export default function Contact() {
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    const form = new FormData(e.currentTarget);
    const serviceValue = String(form.get("service") ?? "");
    // Store the readable service label (with price) rather than the option key.
    const serviceLabel =
      serviceOptions.find((o) => o.value === serviceValue)?.label ?? serviceValue;

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

  return (
    <section id="contact" className="py-24 bg-charcoal-light">
      <div className="max-w-7xl mx-auto px-6">
        <div className="grid lg:grid-cols-2 gap-16 items-start">
          <div className="space-y-8">
            <div>
              <p className="text-gold text-xs uppercase tracking-widest mb-3">Get In Touch</p>
              <h2 className="font-serif text-4xl md:text-5xl text-white mb-4">
                Book Your <span className="gold-gradient">Appointment</span>
              </h2>
              <p className="text-gray-400 leading-relaxed">
                Ready for your transformation? Fill in the form and we&apos;ll reach out to confirm
                your appointment. Walk-ins are also welcome during business hours.
              </p>
            </div>

            <div className="space-y-5">
              {branches.map((branch) => (
                <div key={branch.name} className="flex items-start gap-4">
                  <div className="w-11 h-11 bg-gold/10 rounded-xl flex items-center justify-center flex-shrink-0 mt-0.5">
                    <MapPinIcon className="w-5 h-5 text-gold" />
                  </div>
                  <div>
                    <p className="text-white font-medium">{branch.name}</p>
                    <p className="text-gray-500 text-sm mt-1">
                      {branch.address.map((line, i) => (
                        <span key={i}>
                          {line}
                          {i < branch.address.length - 1 && <br />}
                        </span>
                      ))}
                    </p>
                    <p className="text-gold text-sm mt-2">
                      <a href={`tel:${branch.phone.tel}`} className="hover:underline">
                        {branch.phone.display}
                      </a>
                    </p>
                  </div>
                </div>
              ))}

              <div className="flex items-start gap-4">
                <div className="w-11 h-11 bg-gold/10 rounded-xl flex items-center justify-center flex-shrink-0 mt-0.5">
                  <svg className="w-5 h-5 text-gold" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
                <div>
                  <p className="text-white font-medium">Hours</p>
                  <p className="text-gray-500 text-sm mt-1">Open Daily: 9:00 AM – 7:30 PM</p>
                </div>
              </div>

              <div className="flex items-start gap-4">
                <div className="w-11 h-11 bg-gold/10 rounded-xl flex items-center justify-center flex-shrink-0 mt-0.5">
                  <svg className="w-5 h-5 text-gold" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                  </svg>
                </div>
                <div>
                  <p className="text-white font-medium">Email</p>
                  <p className="text-gray-500 text-sm mt-1">{contactEmail}</p>
                </div>
              </div>
            </div>
          </div>

          <div className="bg-charcoal border border-white/5 rounded-2xl p-8">
            {submitted ? (
              <div className="text-center py-10">
                <div className="w-16 h-16 bg-gold/20 rounded-full flex items-center justify-center mx-auto mb-4">
                  <svg className="w-8 h-8 text-gold" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                  </svg>
                </div>
                <h3 className="text-white text-xl font-serif mb-2">You&apos;re All Set!</h3>
                <p className="text-gray-400 text-sm">
                  We&apos;ve received your request and will confirm your appointment within 24 hours.
                </p>
              </div>
            ) : (
              <form className="space-y-5" onSubmit={handleSubmit}>
                <div className="grid sm:grid-cols-2 gap-5">
                  <Field label="First Name">
                    <input type="text" name="firstName" placeholder="Zach" required className={inputClass} />
                  </Field>
                  <Field label="Last Name">
                    <input type="text" name="lastName" placeholder="Monroe" required className={inputClass} />
                  </Field>
                </div>

                <Field label="Email Address">
                  <input type="email" name="email" placeholder="you@example.com" required className={inputClass} />
                </Field>

                <Field label="Phone Number">
                  <input type="tel" name="phone" placeholder="(212) 555-0000" className={inputClass} />
                </Field>

                <Field label="Service">
                  <select
                    name="service"
                    required
                    defaultValue=""
                    className={`${inputClass} appearance-none cursor-pointer`}
                  >
                    <option value="" disabled className="bg-charcoal">
                      Select a service...
                    </option>
                    {serviceOptions.map((opt) => (
                      <option key={opt.value} value={opt.value} className="bg-charcoal">
                        {opt.label}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Preferred Date">
                  <input type="date" name="preferredDate" required className={`${inputClass} [color-scheme:dark]`} />
                </Field>

                <Field label="Message (Optional)">
                  <textarea
                    name="message"
                    rows={3}
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
                  disabled={submitting}
                  className="w-full bg-gold hover:bg-gold-dark text-charcoal font-bold text-sm uppercase tracking-wider py-4 rounded-xl transition-all duration-300 hover:shadow-xl hover:shadow-gold/30 flex items-center justify-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:shadow-none"
                >
                  <span>{submitting ? "Sending..." : "Request Appointment"}</span>
                  {!submitting && <ArrowRightIcon className="w-4 h-4" strokeWidth={2.5} />}
                </button>
              </form>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
