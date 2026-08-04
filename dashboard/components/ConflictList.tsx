"use client";

import { AlertIcon } from "@/components/icons";
import { formatSalonDateTime } from "@/lib/scheduleTime";
import type { AvailabilityConflict } from "@/lib/useAvailability";

type Props = {
  conflicts: AvailabilityConflict[];
};

/**
 * The hard-block conflict panel (MGMT-03, D-09/D-11) — rendered inline below
 * Save Changes, never a modal, so staff can resolve conflicts elsewhere and
 * retry in place. Distinct rose treatment from the generic network/500 error
 * banner (E7). Internally scrolls past ~6 rows rather than growing the page.
 */
export function ConflictList({ conflicts }: Props) {
  if (conflicts.length === 0) return null;

  return (
    <div
      role="alert"
      className="border-2 border-rose-600 bg-rose-600/5 rounded-xl p-4 max-w-lg"
    >
      <div className="flex items-start gap-2">
        <AlertIcon className="h-5 w-5 text-rose-600 shrink-0 mt-0.5" />
        <div>
          <h3 className="text-lg font-semibold text-ink">
            Can&apos;t Save — Conflicting Appointments
          </h3>
          <p className="text-sm text-muted mt-1">
            These confirmed appointments fall outside the new hours or inside
            the new time off. Cancel or reschedule them first, then try
            again.
          </p>
        </div>
      </div>

      <ul className="mt-4 max-h-64 overflow-y-auto flex flex-col gap-2">
        {conflicts.map((conflict) => (
          <li
            key={conflict.appointmentId}
            className="text-sm text-ink bg-surface rounded-lg border border-border px-3 py-2"
          >
            {conflict.clientName} · {conflict.serviceName} ·{" "}
            {conflict.stylistName} · {formatSalonDateTime(conflict.salonLocalTime)} MMT
          </li>
        ))}
      </ul>
    </div>
  );
}
