"use client";

import type { AppointmentResponseDto } from "@/lib/useSchedule";
import {
  addDays,
  formatSalonDate,
  formatSalonTime,
  parseDateOnly,
  startOfWeekMonday,
  toDateOnly,
  weekdayShort,
} from "@/lib/scheduleTime";

type Props = {
  appointments: AppointmentResponseDto[];
  /** Any date in the week; Monday is derived via startOfWeekMonday. */
  weekOf: string;
  includeCancelled: boolean;
  onOpenDay: (dateOnly: string) => void;
  onOpenDetail: (appointment: AppointmentResponseDto) => void;
};

function statusKey(status: string | undefined): string {
  return (status ?? "").replace(/[\s_-]/g, "").toLowerCase();
}

function isTerminal(status: string | undefined): boolean {
  const key = statusKey(status);
  return key === "cancelled" || key === "noshow";
}

function chipBorderClass(status: string | undefined): string {
  const key = statusKey(status);
  if (key === "cancelled" || key === "noshow") return "border-l-4 border-border";
  return "border-l-4 border-gold-dark";
}

function clientName(a: AppointmentResponseDto): string {
  return [a.firstName, a.lastName].filter(Boolean).join(" ") || "Client";
}

export function WeekChips({
  appointments,
  weekOf,
  includeCancelled,
  onOpenDay,
  onOpenDetail,
}: Props) {
  const monday = startOfWeekMonday(weekOf);
  const days = Array.from({ length: 7 }, (_, i) => addDays(monday, i));

  const visible = includeCancelled
    ? appointments
    : appointments.filter((a) => !isTerminal(a.status));

  if (visible.length === 0) {
    return (
      <div className="px-6 py-12">
        <h2 className="text-lg font-semibold text-ink">No Appointments This Week</h2>
        <p className="text-sm text-muted mt-2 max-w-md">
          This week is wide open. New bookings from the site will appear here
          automatically.
        </p>
      </div>
    );
  }

  return (
    <div className="px-4 pb-8 md:px-6 overflow-x-auto">
      <div className="min-w-[720px] grid grid-cols-7 gap-2">
        {days.map((day) => {
          const dayAppts = visible
            .filter((a) => a.startsAt && toDateOnly(a.startsAt) === day)
            .sort((a, b) =>
              String(a.startsAt).localeCompare(String(b.startsAt))
            );

          return (
            <div key={day} className="min-w-0 flex flex-col gap-2">
              <button
                type="button"
                onClick={() => onOpenDay(day)}
                className="min-h-11 rounded-lg bg-surface-alt border border-border px-2 py-2 text-left hover:border-gold-dark/50 transition-colors"
              >
                <span className="block text-xs uppercase tracking-wider text-muted">
                  {weekdayShort(day)}
                </span>
                <span className="block text-sm text-ink">
                  {formatSalonDate(parseDateOnly(day))}
                </span>
              </button>

              <div className="flex flex-col gap-1.5">
                {dayAppts.map((a) => {
                  const key = statusKey(a.status);
                  const muted =
                    key === "cancelled" || key === "noshow"
                      ? "bg-surface-alt/60 line-through text-muted"
                      : "bg-surface-alt text-ink";
                  const id = a.id ?? `${a.startsAt}-${a.stylistId}`;
                  return (
                    <button
                      key={String(id)}
                      type="button"
                      onClick={() => onOpenDetail(a)}
                      className={`w-full text-left rounded-md border border-border ${chipBorderClass(a.status)} ${muted} px-2 py-1.5 min-h-11`}
                    >
                      <span className="block text-sm truncate text-ellipsis">
                        {a.startsAt ? formatSalonTime(a.startsAt) : ""}{" "}
                        {clientName(a)} · {a.serviceName ?? "Service"}
                      </span>
                      {key === "cancelled" ? (
                        <span className="text-xs uppercase tracking-wider text-muted">
                          Cancelled
                        </span>
                      ) : null}
                      {key === "noshow" ? (
                        <span className="text-xs uppercase tracking-wider text-rose-600">
                          No-show
                        </span>
                      ) : null}
                    </button>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
