"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { z } from "zod";
import SectionHeading from "@/components/SectionHeading";
import {
  CartApiError,
  createCheckout,
  fetchCart,
  quoteCheckout,
  type Cart,
  type CheckoutQuote,
} from "@/lib/cart";
import { fetchLoyaltyBalance } from "@/lib/account";
import { getSession } from "@/lib/auth";
import { AlertIcon } from "./icons";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const pointsFormatter = new Intl.NumberFormat("en-US");

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
  const [sessionPresent, setSessionPresent] = useState(false);
  const [loyaltyBalance, setLoyaltyBalance] = useState(0);
  const [redeemPoints, setRedeemPoints] = useState(0);
  const [quote, setQuote] = useState<CheckoutQuote | null>(null);
  const [quoteError, setQuoteError] = useState(false);
  const [applyingPoints, setApplyingPoints] = useState(false);

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

  useEffect(() => {
    const session = getSession();
    setSessionPresent(!!session);
    if (!session) return;

    setName((current) => current || session.displayName || "");

    let cancelled = false;
    (async () => {
      try {
        const balance = await fetchLoyaltyBalance();
        if (!cancelled) setLoyaltyBalance(balance);
      } catch {
        if (!cancelled) setLoyaltyBalance(0);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const emailValid = useMemo(
    () => EmailSchema.safeParse(email.trim()).success,
    [email]
  );

  const emailError =
    emailTouched && !emailValid
      ? "Enter a valid email address."
      : null;

  const maxRedeemBlocks = useMemo(() => {
    if (!cart || loyaltyBalance < 10) return 0;
    const byBalance = Math.floor(loyaltyBalance / 10);
    // Cap by cart subtotal in $5 blocks (server still recomputes).
    const bySubtotal = Math.floor(cart.subtotal / 5);
    return Math.max(0, Math.min(byBalance, bySubtotal));
  }, [cart, loyaltyBalance]);

  const maxRedeemPoints = maxRedeemBlocks * 10;

  useEffect(() => {
    if (redeemPoints > maxRedeemPoints) {
      setRedeemPoints(maxRedeemPoints);
    }
  }, [maxRedeemPoints, redeemPoints]);

  async function handleApplyPoints() {
    if (!cart || !sessionPresent || !emailValid || applyingPoints) return;

    setApplyingPoints(true);
    setQuoteError(false);
    try {
      const next = await quoteCheckout({
        email: email.trim(),
        name: name.trim() || undefined,
        redeemPoints: redeemPoints > 0 ? redeemPoints : 0,
        items: cart.items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      });
      setQuote(next);
    } catch {
      setQuoteError(true);
      setQuote(null);
    } finally {
      setApplyingPoints(false);
    }
  }

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
        redeemPoints:
          sessionPresent && redeemPoints > 0 ? redeemPoints : undefined,
        items: cart.items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      });
      window.location.href = result.checkoutUrl;
    } catch {
      setSubmitting(false);
      setSubmitError(true);
    }
  }

  const summarySubtotal = quote?.subtotal ?? cart?.subtotal ?? 0;
  const summaryDiscount = quote?.loyaltyDiscount ?? 0;
  const summaryTotal = quote?.totalAmount ?? cart?.subtotal ?? 0;

  return (
    <main className="min-h-screen bg-charcoal-light pt-32">
      <section className="py-16">
        <div className="max-w-5xl mx-auto px-6">
          <SectionHeading
            eyebrow={sessionPresent ? "Checkout" : "Guest Checkout"}
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

                {sessionPresent ? (
                  <div className="border-t border-white/5 pt-6 space-y-4">
                    <div>
                      <p className="text-xs uppercase tracking-wider text-gray-400 mb-1">
                        Loyalty points
                      </p>
                      <p className="text-gold font-bold text-lg">
                        {pointsFormatter.format(loyaltyBalance)}{" "}
                        <span className="text-sm font-normal uppercase tracking-wider">
                          pts
                        </span>
                      </p>
                      <p className="text-gray-500 text-sm mt-1">
                        Use points (10 pts = $5 off)
                      </p>
                    </div>

                    {quoteError ? (
                      <div
                        role="alert"
                        className="flex items-start gap-2 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
                      >
                        <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
                        <span>
                          <strong className="font-semibold">
                            Couldn&apos;t Apply Points
                          </strong>
                          <span className="block mt-1">
                            We couldn&apos;t update your discount. Your points
                            were not spent — try again.
                          </span>
                        </span>
                      </div>
                    ) : null}

                    <div className="flex flex-wrap items-center gap-3">
                      <label
                        htmlFor="redeem-points"
                        className="text-xs uppercase tracking-wider text-gray-400"
                      >
                        Points to use
                      </label>
                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          aria-label="Decrease points by 10"
                          disabled={redeemPoints <= 0 || submitting}
                          onClick={() =>
                            setRedeemPoints((p) => Math.max(0, p - 10))
                          }
                          className="inline-flex items-center justify-center min-h-11 min-w-11 rounded-full border border-white/10 text-white hover:border-gold/40 disabled:opacity-40"
                        >
                          −
                        </button>
                        <input
                          id="redeem-points"
                          type="number"
                          min={0}
                          step={10}
                          max={maxRedeemPoints}
                          value={redeemPoints}
                          onChange={(e) => {
                            const raw = Number(e.target.value) || 0;
                            const stepped = Math.floor(raw / 10) * 10;
                            setRedeemPoints(
                              Math.max(0, Math.min(maxRedeemPoints, stepped))
                            );
                          }}
                          className={`${inputClass} w-24 text-center`}
                          disabled={submitting || maxRedeemPoints === 0}
                        />
                        <button
                          type="button"
                          aria-label="Increase points by 10"
                          disabled={
                            redeemPoints >= maxRedeemPoints || submitting
                          }
                          onClick={() =>
                            setRedeemPoints((p) =>
                              Math.min(maxRedeemPoints, p + 10)
                            )
                          }
                          className="inline-flex items-center justify-center min-h-11 min-w-11 rounded-full border border-white/10 text-white hover:border-gold/40 disabled:opacity-40"
                        >
                          +
                        </button>
                      </div>
                      <button
                        type="button"
                        onClick={() => void handleApplyPoints()}
                        disabled={
                          applyingPoints ||
                          !emailValid ||
                          submitting ||
                          maxRedeemPoints === 0
                        }
                        className="bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-5 py-2.5 min-h-11 rounded-full disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        {applyingPoints ? "Applying…" : "Apply Points"}
                      </button>
                    </div>
                  </div>
                ) : null}

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
                  {sessionPresent && quote ? (
                    <>
                      <div className="flex items-center justify-between gap-4">
                        <dt className="text-gray-500">Subtotal</dt>
                        <dd className="text-white">
                          {priceFormatter.format(summarySubtotal)}
                        </dd>
                      </div>
                      <div className="flex items-center justify-between gap-4">
                        <dt className="text-gray-500">Loyalty discount</dt>
                        <dd className="text-gold font-bold">
                          −{priceFormatter.format(summaryDiscount)}
                        </dd>
                      </div>
                    </>
                  ) : null}
                  <div className="flex items-center justify-between gap-4">
                    <dt className="text-gray-500">Total</dt>
                    <dd className="text-gold text-xl font-bold">
                      {priceFormatter.format(summaryTotal)}
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
