import { api } from "@/lib/api/client";
import {
  ApiError,
  extractErrorMessage,
  handleUnauthorized,
} from "@/lib/auth";
import type { components } from "@/lib/api/schema";

export type AppointmentResponseDto =
  components["schemas"]["AppointmentResponseDto"];

/** String enum names — API registers JsonStringEnumConverter (03-03). */
export type ScheduleStatusAction = "Completed" | "Cancelled" | "NoShow";

/**
 * PATCH /api/schedule/{id}/status. On 401 clears the session; on 400 surfaces
 * the ProblemDetails message (invalid transition, D-10).
 */
export async function updateAppointmentStatus(
  id: number | string,
  newStatus: ScheduleStatusAction
): Promise<AppointmentResponseDto> {
  // OpenAPI schema types AppointmentStatus as number; runtime expects the
  // string enum name from the global JsonStringEnumConverter.
  const { data, response, error } = await api.PATCH("/api/Schedule/{id}/status", {
    params: { path: { id } },
    body: {
      newStatus: newStatus as unknown as components["schemas"]["AppointmentStatus"],
    },
  });

  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }

  if (!response.ok) {
    throw new ApiError(
      extractErrorMessage(error, response.status),
      response.status || null
    );
  }

  if (!data) {
    throw new ApiError("Could not update appointment status.", response.status || null);
  }

  return data;
}
