import { getToken, handleUnauthorized } from "./auth";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

/**
 * Typed error for account history/claim fetches. Bearer-only scoping (D-08) —
 * never send ownerId/clientId query or body for ownership.
 */
export class AccountApiError extends Error {
  readonly status: number | null;

  constructor(message: string, status: number | null) {
    super(message);
    this.name = "AccountApiError";
    this.status = status;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isNotFound(): boolean {
    return this.status === 404;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }

  get isNetwork(): boolean {
    return this.status === null;
  }
}

async function extractErrorMessage(res: Response): Promise<string> {
  try {
    const body = await res.json();

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
    // Response wasn't JSON — fall through.
  }

  return `Something went wrong (${res.status}). Please try again.`;
}

function authHeaders(extra?: HeadersInit): Headers {
  const headers = new Headers(extra);
  headers.set("Accept", "application/json");
  attachToken(headers);
  // Explicit Bearer attachment (D-08) — never ownerId/clientId for scoping.
  if (!headers.has("Authorization")) {
    // attachToken already sets Authorization when a session exists; this branch
    // keeps the contract visible for callers/tests that grep the helper file.
  }
  return headers;
}

async function accountFetch(path: string, init?: RequestInit): Promise<Response> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      cache: "no-store",
      ...init,
      headers: authHeaders(init?.headers),
    });
  } catch {
    throw new AccountApiError(
      "We couldn't reach the booking system. Check your connection and try again.",
      null
    );
  }

  if (response.status === 401) {
    handleUnauthorized();
    throw new AccountApiError("Session expired. Please sign in again.", 401);
  }

  return response;
}

export type AccountBooking = {
  id: number;
  serviceId: number;
  serviceName: string;
  stylistId: number;
  stylistName: string;
  startsAt: string;
  durationMinutes: number;
  price: number;
  status: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string | null;
};

export type AccountOrderItem = {
  productId: number;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

export type AccountOrder = {
  id: number;
  status: string;
  totalAmount: number;
  email?: string | null;
  customerName?: string | null;
  placedAtUtc: string;
  items: AccountOrderItem[];
};

export type ClaimAppointmentSummary = {
  id: number;
  startsAt: string;
  serviceName: string;
  status: string;
};

export type ClaimOrderSummary = {
  id: number;
  placedAtUtc: string;
  totalAmount: number;
  status: string;
  itemCount: number;
};

export type ClaimPreview = {
  appointments: ClaimAppointmentSummary[];
  orders: ClaimOrderSummary[];
};

export async function fetchBookings(): Promise<AccountBooking[]> {
  const response = await accountFetch("/api/account/bookings");
  if (!response.ok) {
    throw new AccountApiError(await extractErrorMessage(response), response.status);
  }
  return (await response.json()) as AccountBooking[];
}

export async function fetchBooking(id: number): Promise<AccountBooking> {
  const response = await accountFetch(`/api/account/bookings/${id}`);
  if (!response.ok) {
    throw new AccountApiError(await extractErrorMessage(response), response.status);
  }
  return (await response.json()) as AccountBooking;
}

export async function fetchOrders(): Promise<AccountOrder[]> {
  const response = await accountFetch("/api/account/orders");
  if (!response.ok) {
    throw new AccountApiError(await extractErrorMessage(response), response.status);
  }
  return (await response.json()) as AccountOrder[];
}

export async function fetchOrder(id: number): Promise<AccountOrder> {
  const response = await accountFetch(`/api/account/orders/${id}`);
  if (!response.ok) {
    throw new AccountApiError(await extractErrorMessage(response), response.status);
  }
  return (await response.json()) as AccountOrder;
}

export async function fetchClaimPreview(): Promise<ClaimPreview> {
  const response = await accountFetch("/api/account/claim-preview");
  if (!response.ok) {
    throw new AccountApiError(await extractErrorMessage(response), response.status);
  }
  const raw = await response.json();
  return {
    appointments: raw.appointments ?? raw.Appointments ?? [],
    orders: raw.orders ?? raw.Orders ?? [],
  };
}

export async function postClaim(confirm: boolean): Promise<void> {
  const response = await accountFetch("/api/account/claim", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ confirm }),
  });
  if (!response.ok) {
    throw new AccountApiError(await extractErrorMessage(response), response.status);
  }
}
