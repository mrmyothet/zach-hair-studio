import Link from "next/link";
import Image from "next/image";
import { notFound } from "next/navigation";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import { formatDuration } from "@/lib/formatDuration";
import { fetchServiceBySlug } from "@/lib/services";

type Props = {
  params: Promise<{ slug: string }>;
};

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

export default async function ServiceDetailPage({ params }: Props) {
  const { slug } = await params;
  const service = await fetchServiceBySlug(slug);

  if (!service) {
    notFound();
  }

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-5xl mx-auto px-6">
            <Link
              href="/services"
              className="text-gold text-xs uppercase tracking-wider hover:underline"
            >
              &larr; Back to services
            </Link>

            <div className="mt-8 grid lg:grid-cols-[1fr_320px] gap-8 items-start">
              <article className="bg-charcoal border border-white/5 rounded-3xl p-8 md:p-10">
                <p className="text-gold text-xs uppercase tracking-widest mb-4">
                  {service.category}
                </p>
                <h1 className="font-serif text-4xl md:text-6xl text-white mb-6">
                  {service.name}
                </h1>
                {service.imageUrl ? (
                  <Image
                    src={service.imageUrl}
                    alt=""
                    width={960}
                    height={480}
                    className="mb-8 h-72 w-full rounded-2xl object-cover"
                  />
                ) : null}
                <p className="text-gray-400 text-lg leading-8">
                  {service.longDescription}
                </p>
              </article>

              <aside className="bg-charcoal border border-gold/20 rounded-3xl p-7 lg:sticky lg:top-28">
                <h2 className="text-white text-xl font-semibold mb-6">
                  Service Details
                </h2>
                <dl className="space-y-5 text-sm">
                  <div className="flex items-center justify-between gap-4 border-b border-white/5 pb-5">
                    <dt className="text-gray-500">Duration</dt>
                    <dd className="text-white font-medium">
                      {formatDuration(service.durationMinutes)}
                    </dd>
                  </div>
                  <div className="flex items-center justify-between gap-4 border-b border-white/5 pb-5">
                    <dt className="text-gray-500">Price</dt>
                    <dd className="text-gold font-bold text-xl">
                      {priceFormatter.format(service.price)}
                    </dd>
                  </div>
                </dl>

                <a
                  href={`/#contact?service=${service.slug}`}
                  className="mt-7 inline-flex w-full items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-bold text-sm uppercase tracking-wider px-6 py-4 rounded-full transition-colors"
                >
                  Book This Service
                </a>
                <p className="text-gray-500 text-xs leading-relaxed mt-4">
                  You will be taken to the contact form with this service
                  selected.
                </p>
              </aside>
            </div>
          </div>
        </section>
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
