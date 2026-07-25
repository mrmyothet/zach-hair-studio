"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { clearSession, getSession, type AuthSession } from "@/lib/auth";
import { LogOutIcon } from "@/components/icons";

const NAV_LINKS = [
  { href: "/schedule", label: "Schedule", ownerOnly: false },
  { href: "/services", label: "Services", ownerOnly: true },
  { href: "/availability", label: "Availability", ownerOnly: false },
] as const;

/**
 * Shared header across every dashboard page (D-16): wordmark, nav links
 * (Services hidden — not disabled — for Staff sessions), session/Add-staff/
 * logout cluster. The server [Authorize(Roles=Owner)] gate is the real
 * boundary; hiding the link here is UX only.
 */
export function DashboardNav() {
  const pathname = usePathname();
  const [session, setSession] = useState<AuthSession | null>(null);

  useEffect(() => {
    setSession(getSession());
  }, []);

  const isOwner = session?.role === "Owner";

  function handleLogout() {
    clearSession();
    window.location.assign("/login");
  }

  return (
    <header className="border-b border-border bg-surface-alt px-4 md:px-6 py-3 flex flex-wrap items-center gap-x-6 gap-y-3 justify-between">
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <h1 className="font-serif text-2xl font-semibold tracking-tight">
          Zach Hair Studio
        </h1>
        <nav className="flex flex-wrap items-center gap-4">
          {NAV_LINKS.filter((link) => !link.ownerOnly || isOwner).map(
            (link) => {
              const active =
                pathname === link.href || pathname?.startsWith(`${link.href}/`);
              return (
                <Link
                  key={link.href}
                  href={link.href}
                  className={
                    active
                      ? "text-sm text-gold-dark font-semibold"
                      : "text-sm text-ink hover:text-gold-dark"
                  }
                >
                  {link.label}
                </Link>
              );
            }
          )}
        </nav>
      </div>
      <div className="flex items-center gap-3">
        <p className="text-sm text-muted">
          {session?.displayName}
          {session?.role ? ` · ${session.role}` : ""}
        </p>
        {isOwner ? (
          <Link
            href="/staff/new"
            className="min-h-11 inline-flex items-center px-3 rounded-xl border border-border text-sm text-ink hover:border-gold-dark/40"
          >
            Add staff
          </Link>
        ) : null}
        <button
          type="button"
          onClick={handleLogout}
          aria-label="Log out"
          className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink"
        >
          <LogOutIcon className="h-5 w-5" />
        </button>
      </div>
    </header>
  );
}
