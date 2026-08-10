import { z } from "zod";
import { getToken } from "./auth";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

const STYLIST_REVALIDATE_SECONDS = 300;

/**
 * A single open appointment start time from GET /api/appointments/slots.
 * `startsAt` is an ISO 8601 string carrying the salon's true offset (e.g.
 * "2026-08-14T10:00:00-04:00"). StylistId/StylistName are populated only when the
 * query is filtered to a specific stylist; the "Any stylist" union leaves them null.
 */
export const OpenSlotSchema = z.object({
  startsAt: z.string(),
  stylistId: z.number().nullable().optional(),
  stylistName: z.string().nullable().optional(),
});

export const OpenSlotListSchema = z.array(OpenSlotSchema);

export type OpenSlot = z.infer<typeof OpenSlotSchema>;

/** Mirrors the backend AppointmentResponseDto — every field the confirmation needs. */
export const AppointmentResponseSchema = z.object({
  id: z.number(),
  serviceId: z.number(),
  serviceName: z.string(),
  stylistId: z.number(),
  stylistName: z.string(),
  startsAt: z.string(),
  durationMinutes: z.number(),
  price: z.number(),
  status: z.string(),
  firstName: z.string(),
  lastName: z.string(),
  email: z.string(),
  phone: z.string().nullable().optional(),
});

export type AppointmentResponse = z.infer<typeof AppointmentResponseSchema>;

/** Mirrors the backend StylistResponseDto. */
export const StylistSchema = z.object({
  id: z.number(),
  slug: z.string(),
  name: z.string(),
  displayOrder: z.number(),
});

export const StylistListSchema = z.array(StylistSchema);

export type Stylist = z.infer<typeof StylistSchema>;

/** Incoming payload for POST /api/appointments — mirrors AppointmentCreateDto. */
export type AppointmentCreateRequest = {
  serviceId: number;
  stylistId?: number | null;
  /** ISO 8601 instant carrying the salon offset, taken verbatim from an OpenSlot. */
  startsAt: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
};

/**
 * Typed error for the appointments client. `status` is the HTTP status code, or
 * `null` for a network/transport failure. Callers branch on it (409 slot-taken vs
 * 400 validation vs null network) — this client never silently swallows failures.
 */
export class AppointmentApiError extends Error {
  readonly status: number | null;

  constructor(message: string, status: number | null) {
    super(message);
    this.name = "AppointmentApiError";
    this.status = status;
  }

  get isConflict(): boolean {
    return this.status === 409;
  }

  get isValidation(): boolean {
    return this.status === 400;
  }

  get isNetwork(): boolean {
    return this.status === null;
  }
}

/** Pulls a friendly message out of an ASP.NET ProblemDetails / ModelState response. */
async function extractErrorMessage(res: Response): Promise<string> {
  try {
    const body = await res.json();

    // ModelState validation errors: { errors: { Field: ["message"] } }
    if (body?.errors && typeof body.errors === "object") {
      const messages = Object.values(body.errors as Record<string, string[]>)
        .flat()
        .filter(Boolean);
      if (messages.length > 0) return messages.join(" ");
    }

    if (typeof body?.detail === "string" && body.detail.length > 0) {
      return body.detail;
    }
    if (typeof body?.title === "string") return body.title;
  } catch {
    // Response wasn't JSON — fall through to the generic message.
  }

  return `Something went wrong (${res.status}). Please try again.`;
}

/**
 * Fetches the open appointment start times for a service/stylist/date.
 *
 * Deliberately NOT wrapped in Next `revalidate` caching — slot availability must
 * always be fresh (D-15). On a transport or non-2xx failure this THROWS an
 * {@link AppointmentApiError} so the UI can distinguish a load-failure ("Couldn't
 * Load Times") from a successful empty day (a returned `[]` = fully booked). It does
 * not swallow to `[]` the way fetchServices does — those two states are visually
 * different in the booking flow.
 *
 * @param serviceId selected service id
 * @param stylistId concrete stylist id, or null for "Any Available Stylist"
 * @param date the target day as "YYYY-MM-DD"
 */
export async function fetchOpenSlots(
  serviceId: number,
  stylistId: number | null,
  date: string
): Promise<OpenSlot[]> {
  const params = new URLSearchParams({
    serviceId: String(serviceId),
    date,
  });
  if (stylistId != null) {
    params.set("stylistId", String(stylistId));
  }

  let response: Response;
  try {
    response = await fetch(
      `${API_BASE_URL}/api/appointments/slots?${params.toString()}`,
      { cache: "no-store" }
    );
  } catch {
    throw new AppointmentApiError(
      "We couldn't reach the booking system.",
      null
    );
  }

  if (!response.ok) {
    throw new AppointmentApiError(
      `Slots request failed with ${response.status}`,
      response.status
    );
  }

  try {
    return OpenSlotListSchema.parse(await response.json());
  } catch {
    // A malformed payload is a load failure, not an empty day (T-02-12).
    throw new AppointmentApiError(
      "The booking system returned an unexpected response.",
      response.status
    );
  }
}

/**
 * Creates an appointment via POST /api/appointments.
 * When a Client session token is present, attaches Authorization Bearer so the API
 * can set Appointment.ClientUserId (register→book owns the row). Guest book unchanged.
 *
 * On 201 returns the parsed {@link AppointmentResponse}. On any failure it THROWS an
 * {@link AppointmentApiError} exposing the HTTP `status`, so the caller can branch:
 *   - 409 → the slot was just taken → run the 409 recovery UX
 *   - 400 → validation problem → show the message inline
 *   - null → network/transport failure
 * It never collapses these into a single default (contrast fetchServices).
 */
export async function createAppointment(
  request: AppointmentCreateRequest
): Promise<AppointmentResponse> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  const token = getToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/appointments`, {
      method: "POST",
      headers,
      body: JSON.stringify(request),
    });
  } catch {
    throw new AppointmentApiError(
      "We couldn't reach the booking system. Please check your connection and try again.",
      null
    );
  }

  if (!response.ok) {
    throw new AppointmentApiError(
      await extractErrorMessage(response),
      response.status
    );
  }

  return AppointmentResponseSchema.parse(await response.json());
}

/**
 * Fetches the active stylists for the picker. Degrades gracefully to `[]` on
 * failure — the "Any Available Stylist" default always works without this list, so
 * a stylist-fetch failure must not break the booking flow.
 */
export async function fetchStylists(): Promise<Stylist[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/stylists`, {
      next: { revalidate: STYLIST_REVALIDATE_SECONDS },
    });

    if (!response.ok) {
      return [];
    }

    return StylistListSchema.parse(await response.json());
  } catch {
    return [];
  }
}
