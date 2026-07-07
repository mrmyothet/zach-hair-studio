// Typed client for the Zach Hair Studio .NET API.
// Base URL is overridable via NEXT_PUBLIC_API_URL; defaults to the local dev API
// (see the `dev` skill / README for ports).
const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

/** Payload for POST /api/bookings — mirrors the backend BookingCreateDto. */
export type BookingRequest = {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  service: string;
  /** ISO date string (YYYY-MM-DD) from the date input. */
  preferredDate: string;
  message?: string;
};

/** Shape returned by the API — mirrors the backend BookingResponseDto. */
export type BookingResponse = {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  service: string;
  preferredDate: string;
  message?: string;
  status: string | number;
  createdAt: string;
  customerName: string;
};

/**
 * Submits an appointment request to the API.
 * Throws an Error with a human-readable message when the request fails.
 */
export async function createBooking(
  data: BookingRequest
): Promise<BookingResponse> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE_URL}/api/bookings`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
  } catch {
    throw new Error(
      "We couldn't reach the booking service. Please check your connection and try again."
    );
  }

  if (!res.ok) {
    throw new Error(await extractErrorMessage(res));
  }

  return res.json();
}

/** Pulls a friendly message out of an ASP.NET ProblemDetails / ModelState response. */
async function extractErrorMessage(res: Response): Promise<string> {
  try {
    const body = await res.json();

    // ModelState validation errors: { errors: { Field: ["message"] } }
    if (body?.errors && typeof body.errors === "object") {
      const messages = Object.values(
        body.errors as Record<string, string[]>
      )
        .flat()
        .filter(Boolean);
      if (messages.length > 0) return messages.join(" ");
    }

    if (typeof body?.title === "string") return body.title;
  } catch {
    // Response wasn't JSON — fall through to the generic message.
  }

  return `Something went wrong (${res.status}). Please try again.`;
}
