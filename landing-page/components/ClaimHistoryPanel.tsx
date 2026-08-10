"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  AccountApiError,
  fetchClaimPreview,
  postClaim,
  type ClaimPreview,
} from "@/lib/account";
import { AlertIcon } from "@/components/icons";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const dateFormatter = new Intl.DateTimeFormat("en-US", {
  dateStyle: "medium",
  timeStyle: "short",
});

type ClaimHistoryPanelProps = {
  /**
   * `register` (default): empty/error/finish navigate to /account (07-02 UX).
   * `embedded`: stay on parent page — empty preview renders null; finish calls onFinished only.
   */
  variant?: "register" | "embedded";
  /** Called after Confirm or Skip — parent reloads list in embedded mode. */
  onFinished?: () => void;
};

/**
 * D-04 claim-by-email confirm UI. Shows only when claim-preview has matches;
 * Skip leaves FKs null; Confirm posts claim then continues.
 */
export default function ClaimHistoryPanel({
  variant = "register",
  onFinished,
}: ClaimHistoryPanelProps) {
  const router = useRouter();
  const embedded = variant === "embedded";
  const [preview, setPreview] = useState<ClaimPreview | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const data = await fetchClaimPreview();
        if (cancelled) return;

        const hasMatches =
          data.appointments.length > 0 || data.orders.length > 0;
        if (!hasMatches) {
          if (!embedded) {
            router.replace("/account");
            onFinished?.();
          }
          return;
        }

        setPreview(data);
      } catch (err) {
        if (cancelled) return;
        if (err instanceof AccountApiError && err.isUnauthorized) return;
        // Preview failure shouldn't trap the user.
        if (!embedded) {
          router.replace("/account");
          onFinished?.();
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [router, onFinished, embedded]);

  async function finish(confirm: boolean) {
    setError(null);
    setSubmitting(true);
    try {
      if (confirm) {
        await postClaim(true);
      } else {
        await postClaim(false);
      }
      if (!embedded) {
        router.replace("/account");
      }
      onFinished?.();
    } catch (err) {
      if (err instanceof AccountApiError && err.isUnauthorized) return;
      setError(
        err instanceof AccountApiError
          ? err.message
          : "We couldn't update your account. Please try again."
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    if (embedded) {
      return null;
    }
    return (
      <div className="w-full max-w-md mx-auto bg-charcoal border border-white/5 rounded-2xl p-8 animate-pulse">
        <div className="h-6 bg-charcoal-light rounded w-2/3 mb-4" />
        <div className="h-4 bg-charcoal-light rounded w-full mb-2" />
        <div className="h-4 bg-charcoal-light rounded w-5/6" />
      </div>
    );
  }

  if (!preview) {
    return null;
  }

  return (
    <div
      className={
        embedded
          ? "w-full bg-charcoal border border-white/5 rounded-2xl p-7 md:p-8 space-y-6 mb-6"
          : "w-full max-w-2xl mx-auto bg-charcoal border border-white/5 rounded-2xl p-7 md:p-10 space-y-6"
      }
    >
      <div>
        <h2 className="text-xl font-semibold text-white">We Found Past Visits</h2>
        <p className="text-sm text-gray-400 mt-2">
          Guest bookings or orders used this email. Add them to your account?
        </p>
      </div>

      <ul className="space-y-3">
        {preview.appointments.map((a) => (
          <li
            key={`a-${a.id}`}
            className="bg-charcoal-light border border-white/5 rounded-2xl px-5 py-4"
          >
            <p className="text-white font-semibold line-clamp-2">{a.serviceName}</p>
            <p className="text-sm text-gray-400 mt-1">
              {dateFormatter.format(new Date(a.startsAt))} · {a.status}
            </p>
          </li>
        ))}
        {preview.orders.map((o) => (
          <li
            key={`o-${o.id}`}
            className="bg-charcoal-light border border-white/5 rounded-2xl px-5 py-4"
          >
            <p className="text-white font-semibold">
              Order #{o.id} · {o.itemCount} {o.itemCount === 1 ? "item" : "items"}
            </p>
            <p className="text-sm text-gray-400 mt-1">
              {dateFormatter.format(new Date(o.placedAtUtc))} ·{" "}
              <span className="text-gold font-bold">
                {priceFormatter.format(o.totalAmount)}
              </span>
            </p>
          </li>
        ))}
      </ul>

      {error ? (
        <div
          role="alert"
          className="flex items-start gap-2 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
        >
          <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      ) : null}

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          disabled={submitting}
          onClick={() => finish(true)}
          className="bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm rounded-full px-6 py-3 min-h-11 transition-colors disabled:opacity-60"
        >
          {submitting ? "Adding…" : "Add to My Account"}
        </button>
        <button
          type="button"
          disabled={submitting}
          onClick={() => finish(false)}
          className="text-sm text-gray-400 hover:text-gold px-4 py-3 min-h-11 transition-colors disabled:opacity-60"
        >
          Skip for Now
        </button>
      </div>

      {!embedded ? (
        <p className="text-xs text-gray-500">
          Prefer to review later?{" "}
          <Link
            href="/account"
            className="text-gray-400 hover:text-gold underline-offset-2 hover:underline"
          >
            Go to account
          </Link>
        </p>
      ) : null}
    </div>
  );
}
