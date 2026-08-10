"use client";

import { useEffect, useState } from "react";
import { AccountApiError, fetchLoyaltyBalance } from "@/lib/account";

const pointsFormatter = new Intl.NumberFormat("en-US");

type StripState = "loading" | "ready" | "error";

/**
 * Account loyalty balance strip (UI-SPEC). Shows 0 pts when empty — not an error.
 */
export default function LoyaltyBalanceStrip() {
  const [state, setState] = useState<StripState>("loading");
  const [balance, setBalance] = useState(0);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const next = await fetchLoyaltyBalance();
        if (cancelled) return;
        setBalance(next);
        setState("ready");
      } catch (err) {
        if (cancelled) return;
        if (err instanceof AccountApiError && err.isUnauthorized) return;
        setState("error");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (state === "error") {
    return (
      <div
        role="status"
        className="bg-charcoal-light border border-white/5 rounded-2xl px-5 py-4 mb-8 text-sm text-gray-400"
      >
        Couldn&apos;t load loyalty balance. Refresh to try again.
      </div>
    );
  }

  const display = state === "ready" ? balance : 0;

  return (
    <div className="bg-charcoal-light border border-white/5 rounded-2xl px-5 py-4 mb-8 flex flex-wrap items-center justify-between gap-4">
      <div>
        <p className="text-xs uppercase tracking-wider text-gray-400 mb-1">
          Loyalty balance
        </p>
        <p className="text-gold font-bold text-2xl">
          {state === "loading" ? (
            <span className="text-gray-500 font-normal text-sm">Loading…</span>
          ) : (
            <>
              {pointsFormatter.format(display)}{" "}
              <span className="text-sm font-normal uppercase tracking-wider">
                pts
              </span>
            </>
          )}
        </p>
      </div>
      <p className="text-gray-400 text-sm max-w-md">
        Earn 1 pt per completed visit · 10 pts = $5 off at checkout.
      </p>
    </div>
  );
}
