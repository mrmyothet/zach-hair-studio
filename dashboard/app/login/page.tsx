"use client";

import { FormEvent, Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { api } from "@/lib/api/client";
import {
  ApiError,
  clearSession,
  extractErrorMessage,
  getSession,
  setSession,
} from "@/lib/auth";

const inputClass =
  "w-full bg-surface border border-border hover:border-gold-dark/40 focus:border-gold-dark rounded-xl px-4 py-3 text-ink placeholder:text-muted/60 text-sm outline-none transition-colors";

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="text-muted text-xs uppercase tracking-wider block mb-2">
        {label}
      </label>
      {children}
    </div>
  );
}

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const sessionReason = searchParams.get("reason");

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(sessionReason);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (getSession()) {
      router.replace("/schedule");
    }
  }, [router]);

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      // Login must not trip the global 401→redirect middleware — wrong
      // credentials stay on this page with an inline error (DASH-05).
      // OpenAPI documents the controller-token path as /api/Auth/login (case-insensitive at runtime).
      const { data, response, error: errorBody } = await api.POST("/api/Auth/login", {
        body: { email, password },
        headers: { "X-Skip-Auth-Redirect": "1" },
      });

      // role may be "" if AspNetUserRoles is missing — still accept a valid token so
      // the staff can get in; Owner-only screens check role separately.
      if (
        !response.ok ||
        !data?.token ||
        !data.expiresAt ||
        !data.displayName ||
        typeof data.role !== "string"
      ) {
        if (response.status === 401) {
          clearSession();
          setError("Invalid email or password.");
          return;
        }

        // A 2xx with a malformed payload has no server error to report — keep the
        // generic message rather than surfacing a misleading status line.
        const message = response.ok
          ? "Could not sign in. Please try again."
          : extractErrorMessage(errorBody, response.status);
        throw new ApiError(message, response.status || null);
      }

      setSession({
        token: data.token,
        expiresAt: data.expiresAt,
        displayName: data.displayName,
        role: data.role,
      });
      router.push("/schedule");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else if (err instanceof TypeError) {
        setError(
          "We couldn't reach the booking system. Check your connection and try again."
        );
      } else {
        setError("Could not sign in. Please try again.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="w-full max-w-md bg-surface-alt rounded-2xl p-8 border border-border shadow-sm">
      <div className="mb-8 text-center">
        <h1 className="font-serif text-2xl text-ink font-semibold tracking-tight">
          Zach Hair Studio
        </h1>
        <p className="text-muted text-sm mt-2">Staff sign-in</p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-5">
        <Field label="Email">
          <input
            type="email"
            name="email"
            autoComplete="username"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        <Field label="Password">
          <input
            type="password"
            name="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        {error ? (
          <p
            role="alert"
            className="text-sm text-destructive bg-destructive/5 border border-destructive/20 rounded-xl px-3 py-2"
          >
            {error}
          </p>
        ) : null}

        <button
          type="submit"
          disabled={submitting}
          className="w-full bg-gold-dark hover:bg-gold text-white font-medium text-sm rounded-xl px-4 py-3 transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
        >
          {submitting ? "Signing in…" : "Log In"}
        </button>
      </form>
    </div>
  );
}

export default function LoginPage() {
  return (
    <main className="min-h-screen bg-surface flex items-center justify-center px-4 py-12">
      <Suspense
        fallback={
          <p className="text-muted text-sm">Loading…</p>
        }
      >
        <LoginForm />
      </Suspense>
    </main>
  );
}
