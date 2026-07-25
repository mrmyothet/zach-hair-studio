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

async function fetchServices(): Promise<ServiceResponseDto[]> {
  const { data, response, error } = await api.GET("/api/Services");

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
 * mid-session, unlike the schedule. GET /api/Services only returns
 * Active rows (server-side filter); the /services page layers a
 * session-local "retired this session" override on top so Retire/
 * Reactivate stay reachable without a new API filter param.
 */
export function useServices() {
  const { data, error, isLoading, mutate } = useSWR(
    "services",
    fetchServices,
    { shouldRetryOnError: false }
  );

  return {
    services: data ?? [],
    isLoading,
    error: error instanceof Error ? error : error ? new Error(String(error)) : null,
    mutate,
  };
}
