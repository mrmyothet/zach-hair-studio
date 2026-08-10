"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { z } from "zod";
import SectionHeading from "@/components/SectionHeading";
import {
  CartApiError,
  createCheckout,
  fetchCart,
  type Cart,
} from "@/lib/cart";
import { AlertIcon } from "./icons";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-3 text-white placeholder-gray-600 text-sm outline-none transition-colors";

const EmailSchema = z.string().email();

type LoadState = "loading" | "ready" | "error" | "empty";

export default function CheckoutForm() {
  const [cart, setCart] = useState<Cart | null>(null);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [emailTouched, setEmailTouched] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState(false);

  const loadCart = useCallback(async () => {
    setLoadState("loading");
    setSubmitError(false);
    try {
      const next = await fetchCart();
      setCart(next);
      setLoadState(next.items.length === 0 ? "empty" : "ready");
    } catch {
      setLoadState("error");
    }
  }, []);

  useEffect(() => {
    void loadCart();
  }, [loadCart]);

  const emailValid = useMemo(
    () => EmailSchema.safeParse(email.trim()).success,
    [email]
  );

  const emailError =
    emailTouched && !emailValid
      ? "Enter a valid email address."
      : null;

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setEmailTouched(true);
    if (!emailValid || !cart || cart.items.length === 0 || submitting) return;

    setSubmitting(true);
    setSubmitError(false);

    try {
      const result = await createCheckout({
        email: email.trim(),
        name: name.trim() || undefined,
        items: cart.items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      });
      window.location.href = result.checkoutUrl;
    } catch (err) {
      setSubmitting(false);
      setSubmitError(true);
      if (!(err instanceof CartApiError)) {
        // Banner copy is fixed per UI-SPEC; keep cart intact.
      }
    }
  }

  return (
    <main className="min-h-screen bg-charcoal-light pt-32">
      <section className="py-16">
        <div className="max-w-5xl mx-auto px-6">
          <SectionHeading
            eyebrow="Guest Checkout"
            title=""
            highlight="Checkout"
            subtitle="Almost there — one last step before you pay."
          />

          {loadState === "error" ? (
            <div
              role="alert"
              className="mb-8 flex flex-col sm:flex-row sm:items-center gap-4 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
            >
              <div className="flex items-start gap-2 flex-1">
                <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
                <span>
                  <strong className="font-semibold">
                    {"Couldn't Load Your Cart"}
                  </strong>
                  <span className="block mt-1">
                    {"We had trouble reaching the server. Please try again."}
                  </span>
                </span>
              </div>
              <button
                type="button"
                onClick={() => void loadCart()}
                className="bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-5 py-2.5 rounded-full whitespace-nowrap"
              >
                Try Again
              </button>
            </div>
          ) : null}

          {loadState === "empty" ? (
            <div className="bg-charcoal border border-white/5 rounded-3xl p-10 md:p-14 text-center max-w-2xl mx-auto">
              <h3 className="font-serif text-3xl text-white mb-4">
                Your Cart Is Empty
              </h3>
              <p className="text-gray-400 mb-8 max-w-md mx-auto">
                Add a few stylist-recommended picks before checking out.
              </p>
              <Link
                href="/products"
                className="inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-6 py-3 rounded-full transition-all duration-300"
              >
                Browse Products
              </Link>
            </div>
          ) : null}

          {loadState === "ready" && cart ? (
            <div className="grid lg:grid-cols-[1fr_320px] gap-8 items-start">
              <form
                onSubmit={handleSubmit}
                className="bg-charcoal border border-white/5 rounded-3xl p-7 space-y-6"
                noValidate
              >
                <h2 className="text-white text-xl font-semibold">
                  Contact
                </h2>

                {submitError ? (
                  <div
                    role="alert"
                    className="flex flex-col sm:flex-row sm:items-center gap-4 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
                  >
                    <div className="flex items-start gap-2 flex-1">
                      <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
                      <span>
                        <strong className="font-semibold">
                          {"Couldn't Start Checkout"}
                        </strong>
                        <span className="block mt-1">
                          {
                            "We couldn't reach our payment provider. Your cart is still saved — please try again."
                          }
                        </span>
                      </span>
                    </div>
                    <button
                      type="submit"
                      disabled={submitting || !emailValid}
                      className="bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-5 py-2.5 rounded-full whitespace-nowrap disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      Try Again
                    </button>
                  </div>
                ) : null}

                <div>
                  <label
                    htmlFor="checkout-email"
                    className="block text-xs uppercase tracking-wider text-gray-400 mb-2"
                  >
                    Email
                  </label>
                  <input
                    id="checkout-email"
                    type="email"
                    autoComplete="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    onBlur={() => setEmailTouched(true)}
                    className={inputClass}
                    placeholder="you@example.com"
                    disabled={submitting}
                  />
                  <p className="text-gray-500 text-sm mt-2">
                    We&apos;ll send your order confirmation here.
                  </p>
                  {emailError ? (
                    <p className="text-rose-400 text-sm mt-2" role="alert">
                      {emailError}
                    </p>
                  ) : null}
                </div>

                <div>
                  <label
                    htmlFor="checkout-name"
                    className="block text-xs uppercase tracking-wider text-gray-400 mb-2"
                  >
                    Name{" "}
                    <span className="normal-case tracking-normal text-gray-600">
                      (optional)
                    </span>
                  </label>
                  <input
                    id="checkout-name"
                    type="text"
                    autoComplete="name"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    className={inputClass}
                    placeholder="Your name"
                    disabled={submitting}
                  />
                </div>

                <button
                  type="submit"
                  disabled={submitting || !emailValid}
                  className="w-full inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-6 py-3 rounded-full transition-all duration-300 disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  {submitting ? "Redirecting to payment…" : "Continue to Payment"}
                </button>

                <Link
                  href="/cart"
                  className="block text-center text-gray-400 hover:text-gold text-sm transition-colors"
                >
                  Return to Cart
                </Link>
              </form>

              <aside className="bg-charcoal border border-white/5 rounded-3xl p-7 lg:sticky lg:top-28">
                <h2 className="text-white text-xl font-semibold mb-6">
                  Order Summary
                </h2>
                <ul className="space-y-3 text-sm mb-6">
                  {cart.items.map((item) => (
                    <li
                      key={item.productId}
                      className="flex justify-between gap-4 text-gray-400"
                    >
                      <span className="line-clamp-2">
                        {item.productName} × {item.quantity}
                      </span>
                      <span className="text-gold font-bold whitespace-nowrap">
                        {priceFormatter.format(item.lineTotal)}
                      </span>
                    </li>
                  ))}
                </ul>
                <dl className="space-y-4 text-sm border-t border-white/5 pt-4">
                  <div className="flex items-center justify-between gap-4">
                    <dt className="text-gray-500">Total</dt>
                    <dd className="text-gold text-xl font-bold">
                      {priceFormatter.format(cart.subtotal)}
                    </dd>
                  </div>
                </dl>
              </aside>
            </div>
          ) : null}

          {loadState === "loading" ? (
            <div
              className="bg-charcoal border border-white/5 rounded-2xl p-7 animate-pulse h-64"
              aria-busy="true"
              aria-label="Loading checkout"
            />
          ) : null}
        </div>
      </section>
    </main>
  );
}
