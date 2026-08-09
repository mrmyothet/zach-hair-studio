import Link from "next/link";
import Image from "next/image";
import { notFound } from "next/navigation";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import { formatDuration } from "@/lib/formatDuration";
import { fetchServiceBySlug } from "@/lib/services";
import type { Product } from "@/lib/products";

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
                    alt={service.name}
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

                <Link
                  href={`/book?service=${service.slug}`}
                  className="mt-7 inline-flex w-full items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-bold text-sm uppercase tracking-wider px-6 py-4 rounded-full transition-colors"
                >
                  Book This Service
                </Link>
                <p className="text-gray-500 text-xs leading-relaxed mt-4">
                  You will be taken to a dedicated booking request page with
                  this service selected.
                </p>
              </aside>
            </div>

            {service.recommendedProducts && service.recommendedProducts.length > 0 ? (
              <section className="mt-16">
                <SectionHeading
                  eyebrow="Stylist Picks for This Service"
                  title="Recommended"
                  highlight="Products"
                  subtitle=""
                />
                <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
                  {service.recommendedProducts.map((product) => (
                    <RecommendedProductCard key={product.id} product={product} />
                  ))}
                </div>
              </section>
            ) : null}
          </div>
        </section>
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}

function RecommendedProductCard({ product }: { product: Product }) {
  return (
    <Link
      href={`/products/${product.slug}`}
      className="card-hover bg-charcoal border border-white/5 hover:border-gold/30 rounded-2xl p-7 group flex flex-col"
    >
      {product.imageUrl ? (
        <Image
          src={product.imageUrl}
          alt={product.name}
          width={640}
          height={360}
          className="aspect-video w-full rounded-xl object-cover mb-5"
        />
      ) : (
        <div className="w-14 h-14 bg-gold/10 group-hover:bg-gold/20 rounded-xl flex items-center justify-center mb-5 transition-colors">
          <span className="text-gold font-serif text-2xl" aria-hidden="true">
            Z
          </span>
        </div>
      )}
      <p className="text-gold text-xs uppercase tracking-widest mb-2">
        {product.category}
      </p>
      <h3 className="text-white text-lg font-semibold mb-3">
        {product.name}
      </h3>
      <p className="text-gray-500 text-sm leading-relaxed mb-6 flex-1">
        {product.shortDescription}
      </p>
      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-white/5 pt-5">
        <span className="text-gold font-bold text-lg">
          {priceFormatter.format(product.price)}
        </span>
        {product.stock === 0 ? (
          <span className="bg-white/5 border border-white/10 text-gray-400 text-xs uppercase tracking-wider px-3 py-1 rounded-full">
            Out of Stock
          </span>
        ) : null}
      </div>
    </Link>
  );
}
