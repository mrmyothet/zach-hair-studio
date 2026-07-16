"use client";

import { useEffect, useId, useRef } from "react";

type Props = {
  open: boolean;
  title: string;
  body: string;
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
};

export function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  onConfirm,
  onCancel,
  busy = false,
}: Props) {
  const titleId = useId();
  const confirmRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;
    confirmRef.current?.focus();

    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onCancel();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onCancel]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center px-4"
      role="presentation"
    >
      <button
        type="button"
        aria-label="Dismiss"
        className="absolute inset-0 bg-ink/40"
        onClick={onCancel}
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="relative z-10 w-full max-w-md rounded-2xl bg-surface-alt border border-border p-6 shadow-lg"
      >
        <h2 id={titleId} className="text-lg font-semibold text-ink">
          {title}
        </h2>
        <p className="text-sm text-muted mt-3">{body}</p>
        <div className="mt-6 flex flex-wrap justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={busy}
            className="min-h-11 px-4 rounded-xl border border-border text-sm text-ink bg-surface hover:bg-surface/80 disabled:opacity-60"
          >
            Never mind
          </button>
          <button
            ref={confirmRef}
            type="button"
            onClick={onConfirm}
            disabled={busy}
            className="min-h-11 px-4 rounded-xl text-sm text-white bg-rose-600 hover:bg-rose-700 disabled:opacity-60"
          >
            {busy ? "Working…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

/** Copywriting contract helpers for Cancel / No-show (D-11). */
export const CONFIRM_COPY = {
  Cancelled: {
    title: "Cancel Appointment",
    body: "Cancel this appointment? This frees the time slot immediately and can't be undone.",
    confirmLabel: "Cancel Appointment",
  },
  NoShow: {
    title: "Mark as No-Show",
    body: "Mark this appointment as a no-show? This frees the time slot immediately and can't be undone.",
    confirmLabel: "Mark as No-Show",
  },
} as const;
