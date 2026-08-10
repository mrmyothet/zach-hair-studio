import Link from "next/link";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";

export const metadata = {
  title: "Checkout Cancelled | Zach Hair Studio",
  description: "Your checkout was cancelled. Your cart is still saved.",
};

export default function CheckoutCancelPage() {
  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-2xl mx-auto px-6">
            <div className="bg-charcoal border border-white/5 rounded-3xl p-10 md:p-14 text-center">
              <h1 className="font-serif text-4xl md:text-5xl text-white mb-4">
                Checkout Cancelled
              </h1>
              <p className="text-gray-400 text-sm mb-8 max-w-md mx-auto">
                No charge was made and your cart is still saved. You can try
                again whenever you&apos;re ready.
              </p>
              <Link
                href="/cart"
                className="inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-6 py-3 rounded-full transition-all duration-300"
              >
                Return to Cart
              </Link>
            </div>
          </div>
        </section>
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
