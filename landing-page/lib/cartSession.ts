const STORAGE_KEY = "zhs-cart-session";

/** UUID v4 for guest cart sessions (max 64 chars — API header limit). */
function createSessionId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  // Fallback for environments without crypto.randomUUID.
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

/**
 * Returns the guest cart session id from localStorage, creating one if missing.
 * Never uses cookies — CORS AllowAnyOrigin blocks credentialed cookie sessions.
 */
export function getCartSessionId(): string {
  if (typeof window === "undefined") {
    // Server Components must not invent a session — cart APIs are client-only.
    throw new Error("getCartSessionId() is only available in the browser");
  }

  try {
    const existing = window.localStorage.getItem(STORAGE_KEY);
    if (existing && existing.trim().length > 0 && existing.length <= 64) {
      return existing.trim();
    }
  } catch {
    // localStorage may be blocked (private mode) — fall through to ephemeral id.
  }

  const id = createSessionId();
  try {
    window.localStorage.setItem(STORAGE_KEY, id);
  } catch {
    // Persist best-effort; still return a usable id for this page session.
  }
  return id;
}

/** Alias used by callers that want the create-if-missing semantics named explicitly. */
export function getOrCreateCartSessionId(): string {
  return getCartSessionId();
}
