"use client";

import type { AppointmentResponseDto } from "@/lib/useSchedule";
import type { ConfirmableAction } from "@/components/AppointmentBlock";
import {
  formatSalonDateTime,
  SALON_ZONE_LABEL,
} from "@/lib/scheduleTime";
import { AlertIcon, CheckIcon, XCircleIcon } from "@/components/icons";

type Props = {
  appointment: AppointmentResponseDto | null;
  onClose: () => void;
  onComplete: (appointment: AppointmentResponseDto) => void;
  onRequestConfirm: (
    appointment: AppointmentResponseDto,
    action: ConfirmableAction
  ) => void;
  busy?: boolean;
};

function statusKey(status: string | undefined): string {
  return (status ?? "").replace(/[\s_-]/g, "").toLowerCase();
}

function statusLabel(status: string | undefined): string {
  const key = statusKey(status);
  if (key === "noshow") return "No-show";
  if (!status) return "—";
  return status;
}

function clientName(a: AppointmentResponseDto): string {
  return [a.firstName, a.lastName].filter(Boolean).join(" ") || "Client";
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wider text-muted">{label}</dt>
      <dd className="text-sm text-ink mt-1">{children}</dd>
    </div>
  );
}

export function AppointmentDetailPanel({
  appointment,
  onClose,
  onComplete,
  onRequestConfirm,
  busy = false,
}: Props) {
  if (!appointment) return null;

  const key = statusKey(appointment.status);
  const canAct = key === "confirmed";
  const hasAudit =
    Boolean(appointment.statusChangedAt) && Boolean(appointment.statusChangedBy);

  return (
    <div className="fixed inset-0 z-40 flex justify-end" role="presentation">
      <button
        type="button"
        aria-label="Close detail"
        className="absolute inset-0 bg-ink/30 md:bg-ink/20"
        onClick={onClose}
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Appointment details"
        className="relative z-10 h-full w-full md:max-w-md bg-surface-alt border-l border-border shadow-xl overflow-y-auto"
      >
        <div className="flex items-start justify-between gap-4 p-6 border-b border-border">
          <div>
            <h2 className="text-lg font-semibold text-ink">
              {clientName(appointment)}
            </h2>
            <p className="text-xs uppercase tracking-wider text-muted mt-1">
              {statusLabel(appointment.status)}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink text-sm"
          >
            Close
          </button>
        </div>

        <dl className="p-6 space-y-4">
          <Field label="Phone">{appointment.phone || "—"}</Field>
          <Field label="Email">{appointment.email || "—"}</Field>
          <Field label="Service">{appointment.serviceName || "—"}</Field>
          <Field label="Stylist">{appointment.stylistName || "—"}</Field>
          <Field label="Price">
            {appointment.price != null ? `$${Number(appointment.price).toFixed(2)}` : "—"}
          </Field>
          <Field label="Duration">
            {appointment.durationMinutes != null
              ? `${appointment.durationMinutes} min`
              : "—"}
          </Field>
          <Field label="Starts">
            {appointment.startsAt
              ? `${formatSalonDateTime(appointment.startsAt)} (${SALON_ZONE_LABEL})`
              : "—"}
          </Field>
          {hasAudit ? (
            <Field label="Status history">
              <span className="text-sm text-muted">
                {statusLabel(appointment.status)} by {appointment.statusChangedBy} ·{" "}
                {formatSalonDateTime(appointment.statusChangedAt!)} MMT
              </span>
            </Field>
          ) : null}
        </dl>

        {canAct ? (
          <div className="p-6 pt-0 flex flex-col gap-2">
            <button
              type="button"
              disabled={busy}
              onClick={() => onComplete(appointment)}
              className="min-h-11 w-full inline-flex items-center justify-center gap-2 rounded-xl border border-border bg-surface text-sm text-ink hover:border-gold-dark/50 disabled:opacity-60"
            >
              <CheckIcon className="h-5 w-5" />
              Complete
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => onRequestConfirm(appointment, "Cancelled")}
              className="min-h-11 w-full inline-flex items-center justify-center gap-2 rounded-xl border border-border bg-surface text-sm text-ink disabled:opacity-60"
            >
              <XCircleIcon className="h-5 w-5" />
              Cancel
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => onRequestConfirm(appointment, "NoShow")}
              className="min-h-11 w-full inline-flex items-center justify-center gap-2 rounded-xl border border-border bg-surface text-sm text-ink disabled:opacity-60"
            >
              <AlertIcon className="h-5 w-5" />
              No-show
            </button>
          </div>
        ) : null}
      </aside>
    </div>
  );
}
