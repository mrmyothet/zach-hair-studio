"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import AccountShell from "@/components/AccountShell";
import { AlertIcon } from "@/components/icons";
import {
  AccountApiError,
  fetchOrders,
  type AccountOrder,
} from "@/lib/account";
import { getSession, requireAuth, type AuthSession } from "@/lib/auth";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const dateFormatter = new Intl.DateTimeFormat("en-US", {
  dateStyle: "medium",
  timeStyle: "short",
});

type LoadState = "loading" | "ready" | "error";

export default function AccountOrdersPage() {
  const router = useRouter();
  const [session, setLocalSession] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [orders, setOrders] = useState<AccountOrder[]>([]);

  const load = useCallback(async () => {
    setLoadState("loading");
    try {
      const data = await fetchOrders();
      setOrders(data);
      setLoadState("ready");
    } catch (err) {
      if (err instanceof AccountApiError && err.isUnauthorized) return;
      setLoadState("error");
    }
  }, []);

  useEffect(() => {
    if (!requireAuth()) return;
    setLocalSession(getSession());
    setReady(true);
    void load();
  }, [load]);

  if (!ready || !session) {
    return (
      <>
        <Navbar />
        <main className="min-h-screen bg-charcoal-light pt-32">
          <p className="text-center text-gray-400 text-sm py-16">Loading…</p>
        </main>
      </>
    );
  }

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <AccountShell
          session={session}
          activeTab="orders"
          onLogout={() => router.push("/account/login")}
        >
          {loadState === "error" ? (
            <div
              role="alert"
              className="flex flex-col sm:flex-row sm:items-center gap-3 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3 mb-6"
            >
              <div className="flex items-start gap-2 flex-1">
                <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
                <span>
                  <strong className="font-semibold">
                    {"Couldn't Load Your Account"}
                  </strong>
                  <span className="block mt-1">
                    We had trouble reaching the server. Please try again.
                  </span>
                </span>
              </div>
              <button
                type="button"
                onClick={() => void load()}
                className="text-sm font-semibold text-rose-300 hover:text-white underline-offset-4 hover:underline min-h-11 px-2"
              >
                Try Again
              </button>
            </div>
          ) : null}

          {loadState === "loading" ? (
            <div className="space-y-4">
              {[0, 1].map((i) => (
                <div
                  key={i}
                  className="bg-charcoal border border-white/5 rounded-2xl p-7 animate-pulse h-28"
                />
              ))}
            </div>
          ) : null}

          {loadState === "ready" && orders.length === 0 ? (
            <div className="bg-charcoal-light border border-white/5 rounded-2xl p-8 md:p-10 text-center space-y-4">
              <h2 className="text-xl font-semibold text-white">No Orders Yet</h2>
              <p className="text-sm text-gray-400">
                Orders from the shop will appear here after checkout.
              </p>
              <Link
                href="/products"
                className="inline-flex items-center justify-center bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm rounded-full px-6 py-3 min-h-11 transition-colors"
              >
                Browse Products
              </Link>
            </div>
          ) : null}

          {loadState === "ready" && orders.length > 0 ? (
            <ul className="space-y-4">
              {orders.map((order) => {
                const itemCount = order.items?.length ?? 0;
                return (
                  <li key={order.id}>
                    <article className="bg-charcoal-light border border-white/5 rounded-2xl p-5 md:p-7">
                      <div className="flex flex-wrap items-start justify-between gap-4">
                        <div className="min-w-0 space-y-2">
                          <h2 className="text-xl font-semibold text-white">
                            Order #{order.id}
                          </h2>
                          <p className="text-sm text-gray-400">
                            {dateFormatter.format(new Date(order.placedAtUtc))} ·{" "}
                            {itemCount} {itemCount === 1 ? "item" : "items"}
                          </p>
                          <p className="text-xs uppercase tracking-wider text-gray-500">
                            {order.status === "Pending"
                              ? "Pending fulfillment"
                              : order.status}
                          </p>
                        </div>
                        <p className="text-gold font-bold text-lg">
                          {priceFormatter.format(order.totalAmount)}
                        </p>
                      </div>
                    </article>
                  </li>
                );
              })}
            </ul>
          ) : null}
        </AccountShell>
      </main>
      <Footer />
    </>
  );
}
