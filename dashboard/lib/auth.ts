/**
 * Client-side bearer-token session for the staff dashboard (D-03).
 *
 * Storage: localStorage under a single key. Acceptable for this internal tool —
 * the ~12h JWT lifetime bounds XSS-exfil risk (T-03-11). Not a cookie/NextAuth
 * session; refresh-token hardening is deferred to Phase 7/8.
 */

const STORAGE_KEY = "zhs.staff.auth";

export type AuthSession = {
  token: string;
  expiresAt: string;
  displayName: string;
  role: string;
};

export class ApiError extends Error {
  readonly status: number | null;

  constructor(message: string, status: number | null) {
    super(message);
    this.name = "ApiError";
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

/** Pulls a friendly message out of an ASP.NET ProblemDetails / ModelState response. */
export async function extractErrorMessage(res: Response): Promise<string> {
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

export function getSession(): AuthSession | null {
  const session = readRaw();
  if (!session) return null;
  if (isExpired(session)) {
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
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  if (typeof window === "undefined") return;
  window.localStorage.removeItem(STORAGE_KEY);
}

/** Used by the openapi-fetch onRequest middleware. */
export function attachToken(headers: Headers): void {
  const token = getToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
}

/**
 * Clears the stored token and sends the browser to /login.
 * Used on 401 responses and by the client-side route guard.
 */
export function handleUnauthorized(message?: string): void {
  clearSession();
  if (typeof window === "undefined") return;
  const params = message
    ? `?reason=${encodeURIComponent(message)}`
    : "";
  window.location.assign(`/login${params}`);
}

/**
 * Client-side route guard for protected pages. Call on mount;
 * returns true when a valid session is present, otherwise redirects to /login.
 */
export function requireAuth(): boolean {
  const session = getSession();
  if (!session) {
    handleUnauthorized();
    return false;
  }
  return true;
}
