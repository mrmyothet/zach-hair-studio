"use client";

import useSWR from "swr";
import { api } from "@/lib/api/client";
import {
  ApiError,
  extractErrorMessage,
  handleUnauthorized,
} from "@/lib/auth";
import type { components } from "@/lib/api/schema";

export type ServiceResponseDto = components["schemas"]["ServiceResponseDto"];

async function fetchServices(includeInactive: boolean): Promise<ServiceResponseDto[]> {
  const { data, response, error } = await api.GET("/api/Services", {
    params: { query: { includeInactive } },
  });

  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }

  if (!response.ok || error) {
    let message = "Couldn't load services.";
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
 * Owner-facing services catalog. No polling — services rarely change
 * mid-session, unlike the schedule. `includeInactive` is honored server-side
 * only for an authenticated Owner (silently ignored otherwise); the two
 * states get distinct SWR cache keys so an Owner listing never shares a
 * cache entry with an active-only fetch.
 */
export function useServices(options?: { includeInactive?: boolean }) {
  const includeInactive = options?.includeInactive ?? false;
  const { data, error, isLoading, mutate } = useSWR(
    includeInactive ? "services:all" : "services:active",
    () => fetchServices(includeInactive),
    { shouldRetryOnError: false }
  );

  return {
    services: data ?? [],
    isLoading,
    error: error instanceof Error ? error : error ? new Error(String(error)) : null,
    mutate,
  };
}
