/**
 * Client-side bearer-token session for the landing-page account surface (D-02).
 *
 * Storage: localStorage under zhs.client.auth — never the staff dashboard key.
 * Mirrors dashboard/lib/auth.ts shape; requireAuth redirects to /account/login.
 */

const STORAGE_KEY = "zhs.client.auth";
const CLIENT_ROLE = "Client";

/** Notify Navbar (and other listeners) that the client auth session changed. */
export const AUTH_UPDATED_EVENT = "zhs:client-auth-updated";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

function notifyAuthUpdated(): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(AUTH_UPDATED_EVENT));
}

export type AuthSession = {
  token: string;
  expiresAt: string;
  displayName: string;
  role: string;
};

export type AuthResponse = AuthSession;

export class AuthApiError extends Error {
  readonly status: number | null;

  constructor(message: string, status: number | null) {
    super(message);
    this.name = "AuthApiError";
    this.status = status;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isValidation(): boolean {
    return this.status === 400;
  }

  get isNetwork(): boolean {
    return this.status === null;
  }
}

export function extractErrorMessage(body: unknown, status: number): string {
  if (body && typeof body === "object") {
    const problem = body as {
      errors?: Record<string, string[]>;
      detail?: unknown;
      title?: unknown;
    };

    if (problem.errors && typeof problem.errors === "object") {
      const messages = Object.values(problem.errors).flat().filter(Boolean);
      if (messages.length > 0) return messages.join(" ");
    }

    if (typeof problem.detail === "string" && problem.detail.length > 0) {
      return problem.detail;
    }
    if (typeof problem.title === "string" && problem.title.length > 0) {
      return problem.title;
    }
  }

  if (typeof body === "string" && body.trim().length > 0) return body;

  return `Something went wrong (${status}). Please try again.`;
}

function readRaw(): AuthSession | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as AuthSession;
    if (!parsed?.token || typeof parsed.token !== "string") return null;
    return parsed;
  } catch {
    return null;
  }
}

function isExpired(session: AuthSession): boolean {
  // Fail closed: a missing or unparsable expiresAt means the stored session
  // is malformed/tampered, so treat it as expired rather than trusting it.
  if (!session.expiresAt) return true;
  const expires = Date.parse(session.expiresAt);
  if (Number.isNaN(expires)) return true;
  return Date.now() >= expires;
}

function isClientSession(session: AuthSession): boolean {
  return session.role === CLIENT_ROLE;
}

export function getSession(): AuthSession | null {
  const session = readRaw();
  if (!session) return null;
  if (isExpired(session) || !isClientSession(session)) {
    clearSession();
    return null;
  }
  return session;
}

export function getToken(): string | null {
  return getSession()?.token ?? null;
}

export function setSession(session: AuthSession): void {
  if (typeof window === "undefined") return;
  if (session.role !== CLIENT_ROLE) {
    clearSession();
    return;
  }
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  notifyAuthUpdated();
}

export function clearSession(): void {
  if (typeof window === "undefined") return;
  window.localStorage.removeItem(STORAGE_KEY);
  notifyAuthUpdated();
}

/** Attach Authorization Bearer for subsequent account API calls. */
export function attachToken(headers: Headers): void {
  const token = getToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
}

/**
 * Clears the stored token and sends the browser to /account/login.
 * Used on 401 responses and by the client-side route guard.
 */
export function handleUnauthorized(message?: string): void {
  clearSession();
  if (typeof window === "undefined") return;
  const params = message
    ? `?reason=${encodeURIComponent(message)}`
    : "";
  window.location.assign(`/account/login${params}`);
}

/**
 * Client-side route guard for protected account pages. Call on mount;
 * returns true when a valid Client session is present, otherwise redirects.
 */
export function requireAuth(): boolean {
  const session = getSession();
  if (!session) {
    handleUnauthorized();
    return false;
  }
  return true;
}

type LoginResponseJson = {
  token?: string;
  expiresAt?: string;
  displayName?: string;
  role?: string;
};

async function parseAuthResponse(response: Response): Promise<AuthResponse> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    body = null;
  }

  if (!response.ok) {
    throw new AuthApiError(
      extractErrorMessage(body, response.status),
      response.status
    );
  }

  const data = body as LoginResponseJson;
  if (
    !data?.token ||
    !data.expiresAt ||
    !data.displayName ||
    typeof data.role !== "string"
  ) {
    throw new AuthApiError("Could not sign in. Please try again.", response.status);
  }

  if (data.role !== CLIENT_ROLE) {
    throw new AuthApiError(
      "This account isn't a client login. Use the staff dashboard instead.",
      response.status
    );
  }

  return {
    token: data.token,
    expiresAt: data.expiresAt,
    displayName: data.displayName,
    role: data.role,
  };
}

export async function loginClient(
  email: string,
  password: string
): Promise<AuthResponse> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ email, password }),
    });
  } catch {
    throw new AuthApiError(
      "We couldn't reach the booking system. Check your connection and try again.",
      null
    );
  }

  return parseAuthResponse(response);
}

export async function registerClient(input: {
  email: string;
  password: string;
  confirmPassword: string;
  displayName?: string;
}): Promise<AuthResponse> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/auth/register`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({
        email: input.email,
        password: input.password,
        confirmPassword: input.confirmPassword,
        displayName: input.displayName,
      }),
    });
  } catch {
    throw new AuthApiError(
      "We couldn't reach the booking system. Check your connection and try again.",
      null
    );
  }

  return parseAuthResponse(response);
}
