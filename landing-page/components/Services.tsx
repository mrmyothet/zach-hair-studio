import Link from "next/link";
import SectionHeading from "./SectionHeading";
import { formatDuration } from "@/lib/formatDuration";
import type { Service } from "@/lib/services";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

type Props = {
  services: Service[];
};

export default function Services({ services }: Props) {
  return (
    <section id="services" className="py-24 bg-charcoal-light">
      <div className="max-w-7xl mx-auto px-6">
        <SectionHeading
          eyebrow="What We Offer"
          title="Our"
          highlight="Services"
          subtitle="From everyday cuts to full transformations, our expert stylists deliver results that speak for themselves."
        />

        {services.length === 0 ? (
          <div className="bg-charcoal border border-white/5 rounded-2xl p-10 text-center">
            <h3 className="text-white text-xl font-semibold mb-3">
              Services are being refreshed
            </h3>
            <p className="text-gray-500 max-w-xl mx-auto">
              The catalog is temporarily unavailable. Please check back soon or
              contact the studio for current service options.
            </p>
          </div>
        ) : (
          <>
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {services.map((service) => (
                <Link
                  key={service.id}
                  href={`/services/${service.slug}`}
                  className="card-hover bg-charcoal border border-white/5 hover:border-gold/30 rounded-2xl p-7 group flex flex-col"
                >
                  <div className="w-14 h-14 bg-gold/10 group-hover:bg-gold/20 rounded-xl flex items-center justify-center mb-5 transition-colors">
                    <span className="text-gold font-serif text-2xl" aria-hidden="true">
                      Z
                    </span>
                  </div>
                  <h3 className="text-white text-lg font-semibold mb-2">
                    {service.name}
                  </h3>
                  <p className="text-gray-500 text-sm leading-relaxed mb-5 flex-1">
                    {service.shortDescription}
                  </p>
                  <div className="flex flex-wrap items-center justify-between gap-3 border-t border-white/5 pt-5">
                    <span className="text-gray-400 text-sm">
                      {formatDuration(service.durationMinutes)}
                    </span>
                    <span className="text-gold font-bold text-lg">
                      {priceFormatter.format(service.price)}
                    </span>
                  </div>
                </Link>
              ))}
            </div>

            <div className="mt-10 text-center">
              <Link
                href="/services"
                className="inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal text-xs font-bold uppercase tracking-wider px-6 py-3 rounded-full transition-colors"
              >
                View Full Service Menu &rarr;
              </Link>
            </div>
          </>
        )}
      </div>
    </section>
  );
}
