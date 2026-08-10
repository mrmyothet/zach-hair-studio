"use client";

import Image from "next/image";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import SectionHeading from "@/components/SectionHeading";
import {
  CartApiError,
  fetchCart,
  removeCartItem,
  updateQuantity,
  type Cart,
  type CartItem,
} from "@/lib/cart";
import { AlertIcon, MinusIcon, PlusIcon } from "./icons";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

type LoadState = "loading" | "ready" | "error";

function QuantityStepper({
  quantity,
  stock,
  disabled,
  onChange,
}: {
  quantity: number;
  stock: number;
  disabled: boolean;
  onChange: (next: number) => void;
}) {
  return (
    <div className="inline-flex items-center rounded-full border border-white/10">
      <button
        type="button"
        aria-label="Decrease quantity"
        disabled={disabled || quantity <= 1}
        onClick={() => onChange(quantity - 1)}
        className="w-11 h-11 flex items-center justify-center text-gray-300 hover:text-gold disabled:opacity-40 disabled:cursor-not-allowed focus:outline-none focus:border-gold rounded-l-full"
      >
        <MinusIcon className="w-4 h-4" />
      </button>
      <span className="min-w-8 text-center font-semibold text-white tabular-nums">
        {quantity}
      </span>
      <button
        type="button"
        aria-label="Increase quantity"
        disabled={disabled || quantity >= stock}
        onClick={() => onChange(quantity + 1)}
        className="w-11 h-11 flex items-center justify-center text-gray-300 hover:text-gold disabled:opacity-40 disabled:cursor-not-allowed focus:outline-none focus:border-gold rounded-r-full"
      >
        <PlusIcon className="w-4 h-4" />
      </button>
    </div>
  );
}

function CartLineRow({
  item,
  busy,
  onQuantity,
  onRemove,
}: {
  item: CartItem;
  busy: boolean;
  onQuantity: (productId: number, quantity: number) => void;
  onRemove: (productId: number) => void;
}) {
  return (
    <article className="bg-charcoal-light border border-white/5 rounded-2xl p-5 md:p-7 flex flex-wrap items-center gap-5">
      {item.imageUrl ? (
        <Image
          src={item.imageUrl}
          alt={item.productName}
          width={80}
          height={80}
          className="w-20 h-20 rounded-xl object-cover flex-shrink-0"
        />
      ) : (
        <div className="w-14 h-14 bg-gold/10 rounded-xl flex items-center justify-center flex-shrink-0">
          <span className="text-gold font-serif text-xl font-bold">Z</span>
        </div>
      )}

      <div className="flex-1 min-w-[12rem]">
        <p className="text-lg font-semibold text-white line-clamp-2">
          {item.productName}
        </p>
        <p className="text-gold mt-1">{priceFormatter.format(item.unitPrice)}</p>
        <div className="mt-3">
          <QuantityStepper
            quantity={item.quantity}
            stock={item.stock}
            disabled={busy}
            onChange={(next) => onQuantity(item.productId, next)}
          />
        </div>
      </div>

      <div className="ml-auto flex flex-col items-end gap-2">
        <p className="text-gold text-xl font-bold">
          {priceFormatter.format(item.lineTotal)}
        </p>
        <button
          type="button"
          disabled={busy}
          onClick={() => onRemove(item.productId)}
          className="text-gray-400 hover:text-white text-xs uppercase tracking-wider disabled:opacity-40"
        >
          Remove
        </button>
      </div>
    </article>
  );
}

function CartSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading cart">
      {[0, 1].map((key) => (
        <div
          key={key}
          className="bg-charcoal border border-white/5 rounded-2xl p-7 animate-pulse"
        >
          <div className="flex gap-5 items-center">
            <div className="w-20 h-20 rounded-xl bg-white/5" />
            <div className="flex-1 space-y-3">
              <div className="h-5 w-2/3 rounded bg-white/5" />
              <div className="h-4 w-20 rounded bg-white/5" />
              <div className="h-11 w-32 rounded-full bg-white/5" />
            </div>
            <div className="h-6 w-16 rounded bg-white/5" />
          </div>
        </div>
      ))}
      <div className="bg-charcoal border border-white/5 rounded-2xl p-7 animate-pulse h-48 lg:hidden" />
    </div>
  );
}

function EmptyCart() {
  return (
    <div className="bg-charcoal border border-white/5 rounded-3xl p-10 md:p-14 text-center max-w-2xl mx-auto">
      <h3 className="font-serif text-3xl text-white mb-4">Your Cart Is Empty</h3>
      <p className="text-gray-400 mb-8 max-w-md mx-auto">
        You haven&apos;t added any products yet. Browse our stylist-recommended
        picks and add a few to get started.
      </p>
      <Link
        href="/products"
        className="inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-6 py-3 rounded-full transition-all duration-300"
      >
        Browse Products
      </Link>
    </div>
  );
}

export default function CartPageClient() {
  const [cart, setCart] = useState<Cart | null>(null);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyProductId, setBusyProductId] = useState<number | null>(null);

  const loadCart = useCallback(async () => {
    setLoadState("loading");
    setActionError(null);
    try {
      const next = await fetchCart();
      setCart(next);
      setLoadState("ready");
    } catch {
      setLoadState("error");
    }
  }, []);

  useEffect(() => {
    void loadCart();
  }, [loadCart]);

  async function handleQuantity(productId: number, quantity: number) {
    if (!cart) return;
    const previous = cart;
    const optimistic: Cart = {
      ...cart,
      items: cart.items.map((item) =>
        item.productId === productId
          ? {
              ...item,
              quantity,
              lineTotal: item.unitPrice * quantity,
            }
          : item
      ),
    };
    optimistic.subtotal = optimistic.items.reduce(
      (sum, item) => sum + item.lineTotal,
      0
    );
    setCart(optimistic);
    setBusyProductId(productId);
    setActionError(null);

    try {
      const next = await updateQuantity(productId, quantity);
      setCart(next);
    } catch (err) {
      setCart(previous);
      if (err instanceof CartApiError && (err.isConflict || err.isValidation)) {
        setActionError(
          err.message || "We couldn't update that quantity. Please try again."
        );
      } else {
        setActionError("We couldn't update your cart. Please try again.");
      }
    } finally {
      setBusyProductId(null);
    }
  }

  async function handleRemove(productId: number) {
    if (!cart) return;
    const previous = cart;
    setBusyProductId(productId);
    setActionError(null);
    setCart({
      ...cart,
      items: cart.items.filter((item) => item.productId !== productId),
      subtotal: cart.items
        .filter((item) => item.productId !== productId)
        .reduce((sum, item) => sum + item.lineTotal, 0),
    });

    try {
      const next = await removeCartItem(productId);
      setCart(next);
    } catch {
      setCart(previous);
      setActionError("We couldn't update your cart. Please try again.");
    } finally {
      setBusyProductId(null);
    }
  }

  const itemCount = cart?.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  const isEmpty = loadState === "ready" && (cart?.items.length ?? 0) === 0;

  return (
    <main className="min-h-screen bg-charcoal-light pt-32">
      <section className="py-16">
        <div className="max-w-5xl mx-auto px-6">
          <SectionHeading
            eyebrow="Your Selection"
            title="Your"
            highlight="Cart"
            subtitle="Review your picks before checking out."
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
                    {
                      "We had trouble reaching the server. Please try again."
                    }
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

          {actionError ? (
            <div
              role="alert"
              className="mb-8 flex items-start gap-2 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
            >
              <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
              <span>{actionError}</span>
            </div>
          ) : null}

          {loadState === "loading" ? <CartSkeleton /> : null}

          {isEmpty ? <EmptyCart /> : null}

          {loadState === "ready" && cart && cart.items.length > 0 ? (
            <div className="grid lg:grid-cols-[1fr_320px] gap-8 items-start">
              <div className="space-y-4">
                <p className="text-gray-500 text-sm">
                  {itemCount === 1 ? "1 item" : `${itemCount} items`}
                </p>
                {cart.items.map((item) => (
                  <CartLineRow
                    key={item.productId}
                    item={item}
                    busy={busyProductId === item.productId}
                    onQuantity={handleQuantity}
                    onRemove={handleRemove}
                  />
                ))}
              </div>

              <aside className="bg-charcoal border border-white/5 rounded-3xl p-7 lg:sticky lg:top-28">
                <h2 className="text-white text-xl font-semibold mb-6">
                  Order Summary
                </h2>
                <dl className="space-y-4 text-sm mb-8">
                  <div className="flex items-center justify-between gap-4">
                    <dt className="text-gray-500">Subtotal</dt>
                    <dd className="text-gold text-xl font-bold">
                      {priceFormatter.format(cart.subtotal)}
                    </dd>
                  </div>
                  <div className="flex items-center justify-between gap-4 border-t border-white/5 pt-4">
                    <dt className="text-gray-500">Total</dt>
                    <dd className="text-gold text-xl font-bold">
                      {priceFormatter.format(cart.subtotal)}
                    </dd>
                  </div>
                </dl>
                <Link
                  href="/checkout"
                  className="w-full inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-6 py-3 rounded-full transition-all duration-300"
                >
                  Proceed to Checkout
                </Link>
                <Link
                  href="/products"
                  className="mt-4 block text-center text-gray-400 hover:text-gold text-sm transition-colors"
                >
                  Continue Shopping
                </Link>
              </aside>
            </div>
          ) : null}

          {loadState === "error" && cart && cart.items.length > 0 ? (
            <div className="grid lg:grid-cols-[1fr_320px] gap-8 items-start opacity-80">
              <div className="space-y-4">
                {cart.items.map((item) => (
                  <CartLineRow
                    key={item.productId}
                    item={item}
                    busy
                    onQuantity={() => undefined}
                    onRemove={() => undefined}
                  />
                ))}
              </div>
            </div>
          ) : null}
        </div>
      </section>
    </main>
  );
}
