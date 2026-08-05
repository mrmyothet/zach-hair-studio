"use client";

import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api/client";
import {
  ApiError,
  extractErrorMessage,
  getSession,
  handleUnauthorized,
  requireAuth,
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

export default function AddStaffPage() {
  const router = useRouter();
  const [ready, setReady] = useState(false);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<"Staff" | "Owner">("Staff");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!requireAuth()) return;
    const session = getSession();
    if (!session || session.role !== "Owner") {
      router.replace("/schedule");
      return;
    }
    setReady(true);
  }, [router]);

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    // API always assigns Staff (D-04); Owner accounts are seeded at startup.
    if (role === "Owner") {
      setError(
        "Owner accounts are seeded at startup and can't be created here. Choose Staff."
      );
      return;
    }

    setSubmitting(true);
    try {
      const { response, error: errorBody } = await api.POST("/api/staff-users", {
        body: { displayName, email, password },
      });

      if (response.status === 401) {
        handleUnauthorized("Your session has ended. Log in again to continue.");
        return;
      }

      // API returns 201 Created. Older OpenAPI docs only listed 200, so openapi-fetch
      // can leave `data` undefined on a successful create — treat any 2xx as success.
      if (!response.ok) {
        throw new ApiError(
          extractErrorMessage(errorBody, response.status),
          response.status || null
        );
      }

      setSuccess(
        "Staff member added. They can log in with the email and password you set."
      );
      setDisplayName("");
      setEmail("");
      setPassword("");
      setRole("Staff");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else if (err instanceof TypeError) {
        setError(
          "We couldn't reach the booking system. Check your connection and try again."
        );
      } else {
        setError("Could not add staff member.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (!ready) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-surface text-muted text-sm">
        Loading…
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-surface flex items-center justify-center px-4 py-12">
      <div className="w-full max-w-md bg-surface-alt rounded-2xl p-8 border border-border shadow-sm">
        <div className="mb-6 flex items-start justify-between gap-4">
          <div>
            <h1 className="text-lg font-semibold text-ink">Add Staff Member</h1>
            <p className="text-sm text-muted mt-1">
              Creates a Staff login for the dashboard.
            </p>
          </div>
          <Link
            href="/schedule"
            className="min-h-11 inline-flex items-center text-sm text-muted hover:text-ink"
          >
            Back
          </Link>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          <Field label="Display name">
            <input
              type="text"
              name="displayName"
              required
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className={inputClass}
              disabled={submitting}
            />
          </Field>

          <Field label="Email">
            <input
              type="email"
              name="email"
              autoComplete="off"
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
              autoComplete="new-password"
              required
              minLength={8}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className={inputClass}
              disabled={submitting}
            />
          </Field>

          <Field label="Role">
            <select
              name="role"
              value={role}
              onChange={(e) => setRole(e.target.value as "Staff" | "Owner")}
              className={inputClass}
              disabled={submitting}
            >
              <option value="Staff">Staff</option>
              <option value="Owner">Owner</option>
            </select>
          </Field>

          {error ? (
            <p
              role="alert"
              className="text-sm text-rose-600 bg-rose-600/5 border border-rose-600/20 rounded-xl px-3 py-2"
            >
              {error}
            </p>
          ) : null}

          {success ? (
            <p
              role="status"
              className="text-sm text-ink bg-surface border border-border rounded-xl px-3 py-2"
            >
              {success}
            </p>
          ) : null}

          <button
            type="submit"
            disabled={submitting}
            className="w-full bg-gold-dark hover:bg-gold text-white font-medium text-sm rounded-xl px-4 py-3 transition-colors disabled:opacity-60 disabled:cursor-not-allowed min-h-11"
          >
            {submitting ? "Adding…" : "Add Staff Member"}
          </button>
        </form>
      </div>
    </main>
  );
}
