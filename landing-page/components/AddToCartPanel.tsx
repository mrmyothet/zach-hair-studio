"use client";

import { useEffect, useState } from "react";
import {
  CartApiError,
  fetchCart,
  upsertCartItem,
} from "@/lib/cart";
import { AlertIcon, CheckIcon, MinusIcon, PlusIcon } from "./icons";

type Props = {
  productId: number;
  stock: number;
};

export default function AddToCartPanel({ productId, stock }: Props) {
  const [quantity, setQuantity] = useState(1);
  const [submitting, setSubmitting] = useState(false);
  const [added, setAdded] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const outOfStock = stock <= 0;
  const maxQty = Math.max(1, stock);

  useEffect(() => {
    if (quantity > maxQty) {
      setQuantity(maxQty);
    }
  }, [maxQty, quantity]);

  useEffect(() => {
    if (!added) return;
    const timer = window.setTimeout(() => setAdded(false), 2000);
    return () => window.clearTimeout(timer);
  }, [added]);

  async function handleAdd() {
    if (outOfStock || submitting) return;

    setSubmitting(true);
    setError(null);

    try {
      let nextQty = quantity;
      try {
        const cart = await fetchCart();
        const existing = cart.items.find((item) => item.productId === productId);
        nextQty = Math.min(stock, (existing?.quantity ?? 0) + quantity);
      } catch {
        // If we can't read the cart, still attempt an absolute upsert of the selected qty.
        nextQty = Math.min(stock, quantity);
      }

      await upsertCartItem({ productId, quantity: nextQty });
      setAdded(true);
    } catch (err) {
      const message =
        err instanceof CartApiError
          ? err.message
          : "We couldn't update your cart. Please try again.";
      setError(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mt-6 space-y-4">
      {!outOfStock ? (
        <div className="inline-flex items-center rounded-full border border-white/10">
          <button
            type="button"
            aria-label="Decrease quantity"
            disabled={quantity <= 1 || submitting}
            onClick={() => setQuantity((q) => Math.max(1, q - 1))}
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
            disabled={quantity >= maxQty || submitting}
            onClick={() => setQuantity((q) => Math.min(maxQty, q + 1))}
            className="w-11 h-11 flex items-center justify-center text-gray-300 hover:text-gold disabled:opacity-40 disabled:cursor-not-allowed focus:outline-none focus:border-gold rounded-r-full"
          >
            <PlusIcon className="w-4 h-4" />
          </button>
        </div>
      ) : null}

      <button
        type="button"
        onClick={handleAdd}
        disabled={outOfStock || submitting}
        className={`w-full bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-6 py-3 rounded-full transition-all duration-300 inline-flex items-center justify-center gap-2 ${
          outOfStock || submitting
            ? "opacity-40 cursor-not-allowed hover:bg-gold"
            : ""
        }`}
      >
        {added ? (
          <>
            Added
            <CheckIcon className="w-4 h-4 text-charcoal" />
          </>
        ) : submitting ? (
          "Adding…"
        ) : (
          "Add to Cart"
        )}
      </button>

      {error ? (
        <div
          role="alert"
          className="flex items-start gap-2 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
        >
          <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
          <span>
            <strong className="font-semibold">Couldn&apos;t Add to Cart</strong>
            <span className="block mt-1">
              We couldn&apos;t update your cart. Please try again.
            </span>
          </span>
        </div>
      ) : null}
    </div>
  );
}
