import Link from "next/link";
import Image from "next/image";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import { fetchProducts, type Product } from "@/lib/products";

export const metadata = {
  title: "Products | Zach Hair Studio",
  description:
    "Browse hair care and styling products our stylists personally recommend.",
};

type CategoryGroup = {
  category: string;
  products: Product[];
};

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

function groupProductsByCategory(products: Product[]): CategoryGroup[] {
  const groups = new Map<string, Product[]>();

  for (const product of [...products].sort((a, b) =>
    a.name.localeCompare(b.name)
  )) {
    groups.set(product.category, [
      ...(groups.get(product.category) ?? []),
      product,
    ]);
  }

  return Array.from(groups, ([category, groupedProducts]) => ({
    category,
    products: groupedProducts,
  }));
}

function ProductCard({ product }: { product: Product }) {
  return (
    <Link
      href={`/products/${product.slug}`}
      className="card-hover bg-charcoal border border-white/5 hover:border-gold/30 rounded-2xl p-7 group flex flex-col"
    >
      {product.imageUrl ? (
        <Image
          src={product.imageUrl}
          alt=""
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

function toCategoryId(category: string): string {
  return `category-${category.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
}

export default async function ProductsPage() {
  const products = await fetchProducts();
  const categoryGroups = groupProductsByCategory(products);

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-7xl mx-auto px-6">
            <SectionHeading
              eyebrow="Stylist Picks"
              title="Recommended"
              highlight="Products"
              subtitle="Hair care and styling products our stylists personally recommend — the same tools and treatments we use in the chair."
            />

            {categoryGroups.length === 0 ? (
              <div className="bg-charcoal border border-white/5 rounded-2xl p-10 text-center">
                <h3 className="text-white text-xl font-semibold mb-3">
                  Products Are Being Curated
                </h3>
                <p className="text-gray-500 max-w-xl mx-auto">
                  Our product recommendations are temporarily unavailable.
                  Please check back soon, or ask your stylist during your next
                  visit.
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
                        {group.products.map((product) => (
                          <ProductCard key={product.id} product={product} />
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
