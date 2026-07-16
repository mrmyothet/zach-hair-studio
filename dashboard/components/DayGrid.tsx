"use client";

import type { AppointmentResponseDto } from "@/lib/useSchedule";
import type { ConfirmableAction } from "@/components/AppointmentBlock";
import { AppointmentBlock } from "@/components/AppointmentBlock";
import {
  OPEN_HOUR,
  CLOSE_HOUR,
  PX_PER_15MIN,
  SALON_ZONE_LABEL,
  blockHeightPx,
  blockTopPx,
  minutesSinceOpen,
} from "@/lib/scheduleTime";

export type StylistColumn = { id: number; name: string };

type Props = {
  appointments: AppointmentResponseDto[];
  stylists: StylistColumn[];
  includeCancelled: boolean;
  onOpenDetail: (appointment: AppointmentResponseDto) => void;
  onComplete: (appointment: AppointmentResponseDto) => void;
  onRequestConfirm: (
    appointment: AppointmentResponseDto,
    action: ConfirmableAction
  ) => void;
};

function statusKey(status: string | undefined): string {
  return (status ?? "").replace(/[\s_-]/g, "").toLowerCase();
}

function isTerminal(status: string | undefined): boolean {
  const key = statusKey(status);
  return key === "cancelled" || key === "noshow";
}

function stylistIdOf(a: AppointmentResponseDto): number {
  return Number(a.stylistId);
}

function resolveColumns(
  stylists: StylistColumn[],
  appointments: AppointmentResponseDto[]
): StylistColumn[] {
  if (stylists.length > 0) return stylists;
  const map = new Map<number, string>();
  for (const a of appointments) {
    const id = stylistIdOf(a);
    if (!Number.isFinite(id)) continue;
    if (!map.has(id)) map.set(id, a.stylistName ?? `Stylist ${id}`);
  }
  return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
}

function workingHours(
  appointments: AppointmentResponseDto[]
): { open: number; close: number } {
  let open = OPEN_HOUR;
  let close = CLOSE_HOUR;
  for (const a of appointments) {
    if (!a.startsAt) continue;
    const mins = minutesSinceOpen(a.startsAt, 0);
    const startH = Math.floor(mins / 60);
    const endH = Math.ceil(
      (mins + Number(a.durationMinutes ?? 0)) / 60
    );
    if (startH < open) open = Math.max(0, startH);
    if (endH > close) close = Math.min(24, endH);
  }
  return { open, close };
}

export function DayGrid({
  appointments,
  stylists,
  includeCancelled,
  onOpenDetail,
  onComplete,
  onRequestConfirm,
}: Props) {
  const visible = includeCancelled
    ? appointments
    : appointments.filter((a) => !isTerminal(a.status));

  const columns = resolveColumns(stylists, visible);
  const { open, close } = workingHours(visible);
  const totalMinutes = (close - open) * 60;
  const gridHeight = (totalMinutes / 15) * PX_PER_15MIN;

  const hourMarks: number[] = [];
  for (let h = open; h <= close; h += 1) hourMarks.push(h);

  if (visible.length === 0) {
    return (
      <div className="px-6 py-12">
        <p className="text-xs uppercase tracking-wider text-muted mb-4">
          All times shown in salon local time ({SALON_ZONE_LABEL})
        </p>
        <h2 className="text-lg font-semibold text-ink">No Appointments Today</h2>
        <p className="text-sm text-muted mt-2 max-w-md">
          Nothing on the books for this day yet. New bookings from the site will
          appear here automatically.
        </p>
      </div>
    );
  }

  return (
    <div className="px-4 pb-8 md:px-6">
      <p className="text-xs uppercase tracking-wider text-muted mb-3">
        All times shown in salon local time ({SALON_ZONE_LABEL})
      </p>

      <div className="overflow-x-auto border border-border rounded-xl bg-surface">
        <div
          className="min-w-[640px] grid"
          style={{
            gridTemplateColumns: `64px repeat(${Math.max(columns.length, 1)}, minmax(140px, 1fr))`,
          }}
        >
          {/* Sticky header row */}
          <div className="sticky top-0 z-20 bg-surface-alt border-b border-border h-10" />
          {columns.map((col) => (
            <div
              key={col.id}
              className="sticky top-0 z-20 bg-surface-alt border-b border-l border-border h-10 flex items-center px-2"
            >
              <span className="text-xs uppercase tracking-wider text-ink truncate">
                {col.name}
              </span>
            </div>
          ))}

          {/* Time axis + columns */}
          <div className="relative border-r border-border" style={{ height: gridHeight }}>
            {hourMarks.map((h) => {
              const top = ((h - open) * 60) / 15 * PX_PER_15MIN;
              const label =
                h === 0
                  ? "12 AM"
                  : h < 12
                    ? `${h} AM`
                    : h === 12
                      ? "12 PM"
                      : `${h - 12} PM`;
              return (
                <div
                  key={h}
                  className="absolute right-2 text-xs uppercase tracking-wider text-muted -translate-y-1/2"
                  style={{ top }}
                >
                  {label}
                </div>
              );
            })}
          </div>

          {columns.map((col) => {
            const colAppts = visible.filter((a) => stylistIdOf(a) === col.id);
            return (
              <div
                key={col.id}
                className="relative border-l border-border"
                style={{ height: gridHeight }}
              >
                {hourMarks.map((h) => {
                  const top = ((h - open) * 60) / 15 * PX_PER_15MIN;
                  return (
                    <div
                      key={h}
                      className="absolute left-0 right-0 border-t border-border/60"
                      style={{ top }}
                    />
                  );
                })}
                {colAppts.map((a) => {
                  const id = a.id ?? `${a.startsAt}-${a.stylistId}`;
                  const top = Math.max(0, blockTopPx(a.startsAt!, open));
                  const height = blockHeightPx(Number(a.durationMinutes ?? 30));
                  return (
                    <AppointmentBlock
                      key={String(id)}
                      appointment={a}
                      topPx={top}
                      heightPx={height}
                      onOpenDetail={onOpenDetail}
                      onComplete={onComplete}
                      onRequestConfirm={onRequestConfirm}
                    />
                  );
                })}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
