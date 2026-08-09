import Link from "next/link";
import Image from "next/image";
import { notFound } from "next/navigation";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import { fetchProductBySlug } from "@/lib/products";

type Props = {
  params: Promise<{ slug: string }>;
};

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

export default async function ProductDetailPage({ params }: Props) {
  const { slug } = await params;
  const product = await fetchProductBySlug(slug);

  if (!product) {
    notFound();
  }

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-5xl mx-auto px-6">
            <Link
              href="/products"
              className="text-gold text-xs uppercase tracking-wider hover:underline"
            >
              &larr; Back to products
            </Link>

            <div className="mt-8 grid lg:grid-cols-[1fr_320px] gap-8 items-start">
              <article className="bg-charcoal border border-white/5 rounded-3xl p-8 md:p-10">
                <p className="text-gold text-xs uppercase tracking-widest mb-4">
                  {product.category}
                </p>
                <h1 className="font-serif text-4xl md:text-6xl text-white mb-6">
                  {product.name}
                </h1>
                {product.imageUrl ? (
                  <Image
                    src={product.imageUrl}
                    alt=""
                    width={960}
                    height={480}
                    className="mb-8 h-72 w-full rounded-2xl object-cover"
                  />
                ) : null}
                <p className="text-gray-400 text-lg leading-8">
                  {product.longDescription}
                </p>
              </article>

              <aside className="bg-charcoal border border-gold/20 rounded-3xl p-7 lg:sticky lg:top-28">
                <h2 className="text-white text-xl font-semibold mb-6">
                  Product Details
                </h2>
                <dl className="space-y-5 text-sm">
                  <div className="flex items-center justify-between gap-4 border-b border-white/5 pb-5">
                    <dt className="text-gray-500">Price</dt>
                    <dd className="text-gold font-bold text-xl">
                      {priceFormatter.format(product.price)}
                    </dd>
                  </div>
                  <div className="flex items-center justify-between gap-4 border-b border-white/5 pb-5">
                    <dt className="text-gray-500">Stock</dt>
                    <dd>
                      {product.stock > 0 ? (
                        <span className="text-gray-400 font-medium">
                          In Stock
                        </span>
                      ) : (
                        <span className="bg-white/5 border border-white/10 text-gray-400 text-xs uppercase tracking-wider px-3 py-1 rounded-full">
                          Out of Stock
                        </span>
                      )}
                    </dd>
                  </div>
                </dl>
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
