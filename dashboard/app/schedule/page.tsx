"use client";

import { useEffect, useState } from "react";
import { getSession, requireAuth, type AuthSession } from "@/lib/auth";

/**
 * Protected schedule stub — real day/week UI lands in 03-05.
 * Visiting without a token redirects to /login via requireAuth().
 */
export default function SchedulePage() {
  const [session, setSessionState] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (!requireAuth()) return;
    setSessionState(getSession());
    setReady(true);
  }, []);

  if (!ready) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-surface text-muted text-sm">
        Loading…
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-surface text-ink">
      <header className="border-b border-border bg-surface-alt px-6 py-4 flex items-center justify-between">
        <h1 className="font-serif text-2xl font-semibold tracking-tight">
          Zach Hair Studio
        </h1>
        <p className="text-sm text-muted">
          {session?.displayName}
          {session?.role ? ` · ${session.role}` : ""}
        </p>
      </header>
      <div className="px-6 py-10 max-w-3xl">
        <h2 className="text-lg font-medium mb-2">Schedule</h2>
        <p className="text-muted text-sm">
          Schedule view coming next. You are signed in.
        </p>
      </div>
    </main>
  );
}
