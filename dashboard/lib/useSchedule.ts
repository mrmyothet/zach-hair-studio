"use client";

import { useEffect, useState } from "react";
import useSWR from "swr";
import { api } from "@/lib/api/client";
import {
  ApiError,
  extractErrorMessage,
  handleUnauthorized,
} from "@/lib/auth";
import type { components } from "@/lib/api/schema";

export type AppointmentResponseDto =
  components["schemas"]["AppointmentResponseDto"];

export type UseScheduleArgs = {
  from: string;
  to: string;
  /** Reserved for callers; the hook always fetches all statuses so D-08 can filter client-side. */
  includeCancelled?: boolean;
};

async function fetchSchedule(
  from: string,
  to: string
): Promise<AppointmentResponseDto[]> {
  const { data, response, error } = await api.GET("/api/Schedule", {
    params: { query: { from, to } },
  });

  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }

  if (!response.ok || error) {
    let message = "Couldn't load the schedule.";
    try {
      message = await extractErrorMessage(response.clone());
    } catch {
      // keep default
    }
    throw new ApiError(message, response.status || null);
  }

  return data ?? [];
}

/**
 * Schedule list with 60s polling + focus revalidation (D-14).
 * Does not pass a status filter — terminal statuses stay in the payload
 * so the "Show cancelled & no-shows" toggle can reveal them client-side.
 */
export function useSchedule({ from, to }: UseScheduleArgs) {
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  const { data, error, isLoading, isValidating, mutate } = useSWR(
    from && to ? (["schedule", from, to] as const) : null,
    ([, f, t]) => fetchSchedule(f, t),
    {
      refreshInterval: 60_000,
      revalidateOnFocus: true,
      shouldRetryOnError: false,
    }
  );

  useEffect(() => {
    if (data) {
      setLastUpdatedAt(new Date());
    }
  }, [data]);

  return {
    appointments: data ?? [],
    isLoading,
    isValidating,
    error: error instanceof Error ? error : error ? new Error(String(error)) : null,
    mutate,
    lastUpdatedAt,
  };
}
