import Link from "next/link";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import { formatDuration } from "@/lib/formatDuration";
import { fetchServices, type Service } from "@/lib/services";

export const metadata = {
  title: "Services | Zach Hair Studio",
  description:
    "Browse Zach Hair Studio services, including cuts, color, styling, treatments, and full glam packages.",
};

type CategoryGroup = {
  category: string;
  services: Service[];
};

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

function groupServicesByCategory(services: Service[]): CategoryGroup[] {
  const groups = new Map<string, Service[]>();

  for (const service of [...services].sort(
    (a, b) => a.displayOrder - b.displayOrder
  )) {
    groups.set(service.category, [
      ...(groups.get(service.category) ?? []),
      service,
    ]);
  }

  return Array.from(groups, ([category, groupedServices]) => ({
    category,
    services: groupedServices,
  }));
}

function ServiceCard({ service }: { service: Service }) {
  return (
    <Link
      href={`/services/${service.slug}`}
      className="card-hover bg-charcoal border border-white/5 hover:border-gold/30 rounded-2xl p-7 group flex flex-col"
    >
      <div className="w-14 h-14 bg-gold/10 group-hover:bg-gold/20 rounded-xl flex items-center justify-center mb-5 transition-colors">
        <span className="text-gold font-serif text-2xl" aria-hidden="true">
          Z
        </span>
      </div>
      <h3 className="text-white text-xl font-semibold mb-3">
        {service.name}
      </h3>
      <p className="text-gray-500 text-sm leading-relaxed mb-6 flex-1">
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
  );
}

function toCategoryId(category: string): string {
  return `category-${category.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
}

export default async function ServicesPage() {
  const services = await fetchServices();
  const categoryGroups = groupServicesByCategory(services);

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-7xl mx-auto px-6">
            <SectionHeading
              eyebrow="Service Menu"
              title="Browse"
              highlight="Services"
              subtitle="Explore cuts, color, styling, and treatments before choosing the service that fits your next visit."
            />

            {categoryGroups.length === 0 ? (
              <div className="bg-charcoal border border-white/5 rounded-2xl p-10 text-center">
                <h3 className="text-white text-xl font-semibold mb-3">
                  Services are being refreshed
                </h3>
                <p className="text-gray-500 max-w-xl mx-auto">
                  The catalog is temporarily unavailable. Please check back
                  soon or contact the studio for current service options.
                </p>
              </div>
            ) : (
              <div className="space-y-14">
                {categoryGroups.map((group) => {
                  const categoryId = toCategoryId(group.category);

                  return (
                    <section key={group.category} aria-labelledby={categoryId}>
                      <div className="flex items-center gap-4 mb-6">
                        <h2
                          id={categoryId}
                          className="font-serif text-3xl text-white"
                        >
                          {group.category}
                        </h2>
                        <div className="h-px flex-1 bg-white/10" />
                      </div>
                      <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
                        {group.services.map((service) => (
                          <ServiceCard key={service.id} service={service} />
                        ))}
                      </div>
                    </section>
                  );
                })}
              </div>
            )}
          </div>
        </section>
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
