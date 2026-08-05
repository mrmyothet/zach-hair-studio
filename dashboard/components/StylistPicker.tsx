"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api/client";
import { ApiError, extractErrorMessage, handleUnauthorized } from "@/lib/auth";

export type StylistOption = { id: number; name: string };

type Props = {
  value: number | null;
  onChange: (id: number) => void;
};

async function fetchStylists(): Promise<StylistOption[]> {
  const { data, response, error } = await api.GET("/api/Stylists");

  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }

  if (!response.ok || error) {
    throw new ApiError(
      extractErrorMessage(error, response.status),
      response.status || null
    );
  }

  return (data ?? [])
    .map((s) => ({ id: Number(s.id), name: s.name ?? `Stylist ${s.id}` }))
    .filter((s) => Number.isFinite(s.id))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * Picker-only stylist selection (D-14 — no stylist CRUD here). Fetches active
 * stylists itself and owns all E4 states; the parent page only reacts to
 * `value` (a null value naturally hides the editors below, no separate
 * "hide editors" plumbing needed).
 */
export function StylistPicker({ value, onChange }: Props) {
  const [stylists, setStylists] = useState<StylistOption[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    fetchStylists()
      .then((list) => {
        setStylists(list);
        setLoading(false);
        if (list.length === 1) {
          onChange(list[0].id);
        }
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : "Couldn't load stylists.");
        setLoading(false);
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  if (loading) {
    return (
      <div>
        <p className="text-xs uppercase tracking-wider text-muted mb-2">Stylist</p>
        <div className="h-11 w-64 max-w-full rounded-xl bg-surface-alt animate-pulse" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-lg">
        <h2 className="text-lg font-semibold text-ink">
          Couldn&apos;t Load Availability.
        </h2>
        <p className="text-sm text-muted mt-2">
          We couldn&apos;t reach the booking system. Try refreshing, or check
          your connection.
        </p>
        <button
          type="button"
          onClick={load}
          className="mt-4 min-h-11 px-4 rounded-xl bg-gold-dark text-white text-sm font-semibold"
        >
          Refresh
        </button>
      </div>
    );
  }

  if (!stylists || stylists.length === 0) {
    return (
      <div>
        <p className="text-xs uppercase tracking-wider text-muted mb-2">Stylist</p>
        <p className="text-sm text-muted">No stylists yet.</p>
      </div>
    );
  }

  return (
    <div>
      <p className="text-xs uppercase tracking-wider text-muted mb-2">Stylist</p>

      {/* Under 768px: collapse to a select (E4 overflow). */}
      <select
        aria-label="Stylist"
        className="md:hidden min-h-11 w-full rounded-xl border border-border bg-surface px-3 text-sm text-ink"
        value={value ?? ""}
        onChange={(e) => onChange(Number(e.target.value))}
      >
        {stylists.map((s) => (
          <option key={s.id} value={s.id}>
            {s.name}
          </option>
        ))}
      </select>

      {/* 768px+: chip row, wraps for many stylists, no page horizontal scroll. */}
      <div className="hidden md:flex flex-wrap gap-2">
        {stylists.map((s) => {
          const selected = s.id === value;
          return (
            <button
              key={s.id}
              type="button"
              onClick={() => onChange(s.id)}
              title={s.name}
              className={
                selected
                  ? "min-h-11 max-w-[220px] px-4 rounded-xl border border-gold-dark bg-gold-dark/10 text-sm text-gold-dark font-semibold truncate"
                  : "min-h-11 max-w-[220px] px-4 rounded-xl border border-border text-sm text-ink hover:border-gold-dark/40 truncate"
              }
            >
              {s.name}
            </button>
          );
        })}
      </div>
    </div>
  );
}
