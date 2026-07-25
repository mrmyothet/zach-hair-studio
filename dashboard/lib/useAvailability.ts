"use client";

import useSWR from "swr";
import { api } from "@/lib/api/client";
import {
  ApiError,
  extractErrorMessage,
  handleUnauthorized,
} from "@/lib/auth";
import type { components } from "@/lib/api/schema";

/**
 * OpenAPI schema types DayOfWeek as `number` (Swashbuckle enum-doc quirk,
 * same class of mismatch as ScheduleStatusAction/AppointmentStatus); the
 * server's global JsonStringEnumConverter actually serializes DayOfWeek as
 * its .NET name ("Monday" … "Sunday") on the wire — confirmed against a live
 * GET /api/Availability/{id} response during this plan.
 */
export type DayOfWeekName =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export const WEEKDAYS: DayOfWeekName[] = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

/** One StylistWorkingHours segment, `startTime`/`endTime` as "HH:mm:ss". */
export type WorkingHoursSegment = {
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
};

/**
 * A StylistTimeOff range. `id` is present for a range already persisted on
 * the server; a range the staff has painted but not yet saved has no `id`
 * (D-12: the whole batch of adds/removes ships together on Save Changes).
 */
export type TimeOffRange = {
  id?: number;
  startsAt: string;
  endsAt: string;
  reason?: string | null;
};

type AvailabilityData = {
  hours: WorkingHoursSegment[];
  timeOff: TimeOffRange[];
};

/**
 * One Confirmed appointment the server refused to orphan (MGMT-03, D-09/D-11).
 * The 409 body's "conflicts" extension isn't representable in the generated
 * OpenAPI schema (ProblemDetails.Extensions has no static shape), so this type
 * is hand-defined to match AvailabilityConflictDto's fields exactly, per the
 * plan's documented escape hatch.
 */
export type AvailabilityConflict = {
  appointmentId: number;
  clientName: string;
  serviceName: string;
  stylistName: string;
  salonLocalTime: string;
};

/**
 * Thrown instead of the generic ApiError when a save is hard-blocked by the
 * conflict check, so the page can render the ConflictList panel instead of
 * the generic network/500 banner (E7).
 */
export class AvailabilityConflictError extends ApiError {
  readonly conflicts: AvailabilityConflict[];

  constructor(message: string, conflicts: AvailabilityConflict[]) {
    super(message, 409);
    this.name = "AvailabilityConflictError";
    this.conflicts = conflicts;
  }
}

async function extractConflicts(res: Response): Promise<AvailabilityConflict[]> {
  try {
    const body = await res.json();
    if (Array.isArray(body?.conflicts)) {
      return body.conflicts as AvailabilityConflict[];
    }
  } catch {
    // Not JSON, or no conflicts array — treat as no parseable conflicts.
  }
  return [];
}

async function fetchAvailability(stylistId: number): Promise<AvailabilityData> {
  const { data, response, error } = await api.GET("/api/Availability/{stylistId}", {
    params: { path: { stylistId } },
  });

  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }

  if (!response.ok || error) {
    let message = "Couldn't load availability.";
    try {
      message = await extractErrorMessage(response.clone());
    } catch {
      // keep default
    }
    throw new ApiError(message, response.status || null);
  }

  const hours = (data?.workingHours ?? []).map((segment) => ({
    dayOfWeek: segment.dayOfWeek as unknown as DayOfWeekName,
    startTime: String(segment.startTime),
    endTime: String(segment.endTime),
  }));

  const timeOff = (data?.timeOff ?? []).map((range) => ({
    id:
      range.id == null
        ? undefined
        : Number.isFinite(Number(range.id))
          ? Number(range.id)
          : undefined,
    startsAt: String(range.startsAt),
    endsAt: String(range.endsAt),
    reason: range.reason ?? null,
  }));

  return { hours, timeOff };
}

/**
 * One stylist's current availability (working hours + time off), keyed on
 * stylistId (mirrors useSchedule's compound-key SWR pattern).
 */
export function useAvailability(stylistId: number | null) {
  const { data, error, isLoading, mutate } = useSWR(
    stylistId != null ? (["availability", stylistId] as const) : null,
    ([, id]) => fetchAvailability(id),
    {
      revalidateOnFocus: false,
      shouldRetryOnError: false,
    }
  );

  return {
    hours: data?.hours ?? [],
    timeOff: data?.timeOff ?? [],
    isLoading,
    error:
      error instanceof Error ? error : error ? new Error(String(error)) : null,
    mutate,
  };
}

/**
 * Single Save Changes (D-12): whole-week hours replace, plus the time-off
 * diff against `originalTimeOffIds` (removed ranges DELETE, new ranges — no
 * `id` yet — POST). One authoritative save moment for the Plan 05 conflict
 * check to evaluate the whole new state against.
 */
export async function saveAvailability(
  stylistId: number,
  hours: WorkingHoursSegment[],
  timeOff: TimeOffRange[],
  originalTimeOffIds: number[]
): Promise<void> {
  const { response: hoursResponse, error: hoursError } = await api.PUT(
    "/api/Availability/{stylistId}/working-hours",
    {
      params: { path: { stylistId } },
      body: {
        segments: hours.map((segment) => ({
          dayOfWeek: segment.dayOfWeek as unknown as components["schemas"]["DayOfWeek"],
          startTime: segment.startTime,
          endTime: segment.endTime,
        })),
      },
    }
  );

  if (!hoursResponse.ok || hoursError) {
    if (hoursResponse.status === 409) {
      throw new AvailabilityConflictError(
        "Can't save — some confirmed appointments fall outside the new hours or inside time off.",
        await extractConflicts(hoursResponse.clone())
      );
    }
    let message = "Couldn't save availability. Try again.";
    try {
      message = await extractErrorMessage(hoursResponse.clone());
    } catch {
      // keep default
    }
    throw new ApiError(message, hoursResponse.status || null);
  }

  const keptIds = new Set(
    timeOff.map((range) => range.id).filter((id): id is number => id != null)
  );
  const removedIds = originalTimeOffIds.filter((id) => !keptIds.has(id));

  for (const timeOffId of removedIds) {
    const { response, error } = await api.DELETE(
      "/api/Availability/{stylistId}/time-off/{timeOffId}",
      { params: { path: { stylistId, timeOffId } } }
    );
    // A range already removed server-side (404) is not a save failure.
    if ((!response.ok && response.status !== 404) || error) {
      let message = "Couldn't save availability. Try again.";
      try {
        message = await extractErrorMessage(response.clone());
      } catch {
        // keep default
      }
      throw new ApiError(message, response.status || null);
    }
  }

  const added = timeOff.filter((range) => range.id == null);
  for (const range of added) {
    const { response, error } = await api.POST(
      "/api/Availability/{stylistId}/time-off",
      {
        params: { path: { stylistId } },
        body: {
          startsAt: range.startsAt,
          endsAt: range.endsAt,
          reason: range.reason ?? undefined,
        },
      }
    );
    if (!response.ok || error) {
      if (response.status === 409) {
        throw new AvailabilityConflictError(
          "Can't save — some confirmed appointments fall outside the new hours or inside time off.",
          await extractConflicts(response.clone())
        );
      }
      let message = "Couldn't save availability. Try again.";
      try {
        message = await extractErrorMessage(response.clone());
      } catch {
        // keep default
      }
      throw new ApiError(message, response.status || null);
    }
  }
}
