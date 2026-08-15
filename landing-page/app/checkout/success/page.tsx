import { notFound } from "next/navigation";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import { CheckIcon } from "@/components/icons";
import { fetchOrderById } from "@/lib/cart";

export const metadata = {
  title: "Order Received | Zach Hair Studio",
  description: "Your payment went through. We'll email your order confirmation when it's ready.",
};

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

type Props = {
  searchParams: Promise<{
    orderId?: string;
    order?: string;
    session_id?: string;
  }>;
};

// SHOP-02 — the order id must arrive as an explicit param. Never derive it from
// session_id: a Stripe id is random, so digits found in it belong to no order.
function parseOrderId(params: { orderId?: string; order?: string }): number | null {
  const raw = params.orderId ?? params.order;
  return raw && /^\d+$/.test(raw) ? Number(raw) : null;
}

export default async function CheckoutSuccessPage({ searchParams }: Props) {
  const params = await searchParams;
  const orderId = parseOrderId(params);
  if (orderId === null) {
    notFound();
  }

  // SHOP-05 / D-06 — display/poll GET only; never mutate order status from this page.
  const order = await fetchOrderById(orderId);
  if (!order) {
    notFound();
  }

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-2xl mx-auto px-6">
            <div className="bg-charcoal border border-white/5 rounded-3xl p-10 md:p-14 text-center">
              <div className="w-16 h-16 bg-gold/20 rounded-full flex items-center justify-center mx-auto mb-4">
                <CheckIcon className="w-8 h-8 text-gold" />
              </div>
              <h1 className="font-serif text-4xl md:text-5xl text-white mb-4">
                Order Received
              </h1>
              <p className="text-gray-400 text-sm mb-8 max-w-md mx-auto">
                Thanks! Your payment went through — we&apos;ll email your order
                confirmation once it&apos;s ready.
              </p>

              <dl className="text-left max-w-md mx-auto space-y-3 bg-charcoal-light border border-white/5 rounded-xl p-6 mb-6">
                <div className="flex justify-between gap-4">
                  <dt className="text-gray-400 text-sm">Order number</dt>
                  <dd className="text-white text-sm text-right">#{order.id}</dd>
                </div>
                {order.items.map((item) => (
                  <div
                    key={`${item.productId}-${item.productName}`}
                    className="flex justify-between gap-4"
                  >
                    <dt className="text-gray-400 text-sm">
                      {item.productName} × {item.quantity}
                    </dt>
                    <dd className="text-gold font-bold text-sm text-right">
                      {priceFormatter.format(item.lineTotal)}
                    </dd>
                  </div>
                ))}
                <div className="flex justify-between gap-4 border-t border-white/5 pt-3">
                  <dt className="text-gray-400 text-sm">Total</dt>
                  <dd className="text-gold text-xl font-bold text-right">
                    {priceFormatter.format(order.totalAmount)}
                  </dd>
                </div>
              </dl>

              <p className="text-gray-500 text-xs max-w-md mx-auto">
                Payment confirmed. You&apos;ll receive a confirmation email when
                your order is ready.
              </p>
            </div>
          </div>
        </section>
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
