"use client";

import type { AppointmentResponseDto } from "@/lib/useSchedule";
import type { ScheduleStatusAction } from "@/lib/scheduleStatus";
import {
  formatSalonTime,
  blockHeightPx,
} from "@/lib/scheduleTime";
import {
  AlertIcon,
  CheckIcon,
  XCircleIcon,
} from "@/components/icons";

export type ConfirmableAction = Extract<ScheduleStatusAction, "Cancelled" | "NoShow">;

type Props = {
  appointment: AppointmentResponseDto;
  topPx: number;
  heightPx: number;
  onOpenDetail: (appointment: AppointmentResponseDto) => void;
  onComplete: (appointment: AppointmentResponseDto) => void;
  onRequestConfirm: (
    appointment: AppointmentResponseDto,
    action: ConfirmableAction
  ) => void;
};

function clientName(a: AppointmentResponseDto): string {
  return [a.firstName, a.lastName].filter(Boolean).join(" ") || "Client";
}

function endTime(a: AppointmentResponseDto): string {
  if (!a.startsAt || a.durationMinutes == null) return "";
  const start = new Date(a.startsAt);
  const mins = Number(a.durationMinutes);
  const end = new Date(start.getTime() + mins * 60_000);
  return formatSalonTime(end);
}

function statusKey(status: string | undefined): string {
  return (status ?? "").replace(/[\s_-]/g, "").toLowerCase();
}

export function AppointmentBlock({
  appointment,
  topPx,
  heightPx,
  onOpenDetail,
  onComplete,
  onRequestConfirm,
}: Props) {
  const status = statusKey(appointment.status);
  const isCancelled = status === "cancelled";
  const isNoShow = status === "noshow";
  const isCompleted = status === "completed";
  const isTerminal = isCancelled || isNoShow;
  const showActions = !isTerminal && !isCompleted && heightPx >= 40;
  const showOverflow = !isTerminal && !isCompleted && heightPx < 40;

  const borderClass = isTerminal
    ? "border-l-4 border-border"
    : "border-l-4 border-gold-dark";
  const surfaceClass = isTerminal
    ? "bg-surface-alt/60"
    : "bg-surface-alt";
  const strikeClass = isTerminal ? "line-through text-muted" : "";

  const startsLabel = appointment.startsAt
    ? formatSalonTime(appointment.startsAt)
    : "";
  const rangeLabel =
    startsLabel && endTime(appointment)
      ? `${startsLabel} – ${endTime(appointment)}`
      : startsLabel;

  return (
    <div
      className={`absolute left-1 right-1 overflow-hidden rounded-md border border-border ${surfaceClass} ${borderClass} shadow-sm`}
      style={{ top: topPx, height: heightPx, minHeight: blockHeightPx(15) }}
    >
      <button
        type="button"
        onClick={() => onOpenDetail(appointment)}
        className="block w-full h-full text-left px-2 pt-1.5 pb-1"
      >
        <div className="relative pr-5">
          <p className={`text-sm text-ink truncate ${strikeClass}`}>
            {clientName(appointment)}
          </p>
          <p className={`text-sm text-muted truncate ${strikeClass}`}>
            {appointment.serviceName ?? "Service"}
          </p>
          <p className="text-xs uppercase tracking-wider text-muted truncate">
            {rangeLabel}
          </p>
          {isCancelled ? (
            <span className="text-xs uppercase tracking-wider text-muted">
              Cancelled
            </span>
          ) : null}
          {isNoShow ? (
            <span className="text-xs uppercase tracking-wider text-rose-600">
              No-show
            </span>
          ) : null}
          {isCompleted ? (
            <span
              className="absolute top-0 right-0 flex h-5 w-5 items-center justify-center rounded-full bg-gold-dark text-white"
              aria-label="Completed"
            >
              <CheckIcon className="h-3 w-3" />
            </span>
          ) : null}
        </div>
      </button>

      {showActions ? (
        <div className="absolute bottom-0 left-0 right-0 flex items-center justify-end gap-0.5 px-1 pb-0.5 bg-gradient-to-t from-surface-alt via-surface-alt/90 to-transparent">
          <button
            type="button"
            aria-label="Complete"
            title="Complete"
            onClick={(e) => {
              e.stopPropagation();
              onComplete(appointment);
            }}
            className="min-h-11 min-w-11 inline-flex items-center justify-center text-ink hover:text-gold-dark"
          >
            <CheckIcon className="h-5 w-5" />
          </button>
          <button
            type="button"
            aria-label="Cancel"
            title="Cancel"
            onClick={(e) => {
              e.stopPropagation();
              onRequestConfirm(appointment, "Cancelled");
            }}
            className="min-h-11 min-w-11 inline-flex items-center justify-center text-ink hover:text-ink"
          >
            <XCircleIcon className="h-5 w-5" />
          </button>
          <button
            type="button"
            aria-label="No-show"
            title="No-show"
            onClick={(e) => {
              e.stopPropagation();
              onRequestConfirm(appointment, "NoShow");
            }}
            className="min-h-11 min-w-11 inline-flex items-center justify-center text-ink hover:text-ink"
          >
            <AlertIcon className="h-5 w-5" />
          </button>
        </div>
      ) : null}

      {showOverflow ? (
        <button
          type="button"
          aria-label="Appointment actions"
          onClick={(e) => {
            e.stopPropagation();
            onOpenDetail(appointment);
          }}
          className="absolute bottom-0 right-0 min-h-11 min-w-11 inline-flex items-center justify-center text-ink text-lg leading-none"
        >
          ⋯
        </button>
      ) : null}
    </div>
  );
}
