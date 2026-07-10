"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { branches, contactEmail } from "@/lib/data";
import type { Service } from "@/lib/services";
import { ArrowRightIcon, MapPinIcon } from "./icons";

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
      <label className="text-gray-400 text-xs uppercase tracking-wider block mb-2">{label}</label>
      {children}
    </div>
  );
}

function formatServiceOption(service: Service): string {
  return `${service.name} - ${priceFormatter.format(service.price)}`;
}

export default function Contact({ services, initialServiceSlug }: Props) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const servicesBySlug = useMemo(
    () => new Map(services.map((service) => [service.slug, service])),
    [services]
  );
  const requestedSlug = searchParams.get("service") ?? initialServiceSlug ?? "";
  const preselectedSlug = servicesBySlug.has(requestedSlug) ? requestedSlug : "";
  const [selectedSlug, setSelectedSlug] = useState(preselectedSlug);

  useEffect(() => {
    setSelectedSlug(preselectedSlug);
  }, [preselectedSlug]);

  // The homepage quick form no longer POSTs free text (D-14: the /api/bookings path
  // is retired). It routes into the real /book flow, preserving the chosen service so
  // that step 1 is pre-selected there.
  function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    router.push(selectedSlug ? `/book?service=${selectedSlug}` : "/book");
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
                Ready for your transformation? Choose a service and continue to
                our booking flow to pick a real open time. Walk-ins are also
                welcome during business hours.
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
            <form className="space-y-5" onSubmit={handleSubmit}>
              <div className="grid sm:grid-cols-2 gap-5">
                <Field label="First Name">
                  <input type="text" name="firstName" placeholder="Zach" className={inputClass} />
                </Field>
                <Field label="Last Name">
                  <input type="text" name="lastName" placeholder="Monroe" className={inputClass} />
                </Field>
              </div>

              <Field label="Email Address">
                <input type="email" name="email" placeholder="you@example.com" className={inputClass} />
              </Field>

              <Field label="Phone Number">
                <input type="tel" name="phone" placeholder="(212) 555-0000" className={inputClass} />
              </Field>

              <Field label="Service">
                <select
                  name="service"
                  value={selectedSlug}
                  onChange={(event) => setSelectedSlug(event.target.value)}
                  className={`${inputClass} appearance-none cursor-pointer`}
                >
                  <option value="" disabled className="bg-charcoal">
                    Select a service...
                  </option>
                  {services.map((service) => (
                    <option key={service.slug} value={service.slug} className="bg-charcoal">
                      {formatServiceOption(service)}
                    </option>
                  ))}
                </select>
              </Field>

              <button
                type="submit"
                className="w-full bg-gold hover:bg-gold-dark text-charcoal font-bold text-sm uppercase tracking-wider py-4 rounded-xl transition-all duration-300 hover:shadow-xl hover:shadow-gold/30 flex items-center justify-center gap-2"
              >
                <span>Continue to Booking</span>
                <ArrowRightIcon className="w-4 h-4" strokeWidth={2.5} />
              </button>
            </form>
          </div>
        </div>
      </div>
    </section>
  );
}
