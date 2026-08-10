"use client";

import Link from "next/link";
import { clearSession, type AuthSession } from "@/lib/auth";
import SectionHeading from "@/components/SectionHeading";

export type AccountTab = "bookings" | "orders";

const tabClass = (active: boolean) =>
  [
    "inline-flex items-center justify-center rounded-full border px-5 py-2.5 text-sm min-h-11 uppercase tracking-wider transition-colors",
    active
      ? "border-gold text-gold font-semibold"
      : "border-white/10 text-gray-300 hover:border-gold/40 hover:text-gold",
  ].join(" ");

type AccountShellProps = {
  session: AuthSession;
  activeTab: AccountTab;
  children: React.ReactNode;
  onLogout?: () => void;
};

export default function AccountShell({
  session,
  activeTab,
  children,
  onLogout,
}: AccountShellProps) {
  function handleLogout() {
    clearSession();
    if (onLogout) {
      onLogout();
    } else if (typeof window !== "undefined") {
      window.location.assign("/account/login");
    }
  }

  return (
    <section className="py-16">
      <div className="max-w-5xl mx-auto px-6">
        <div className="flex flex-wrap items-start justify-between gap-4 mb-2">
          <SectionHeading
            eyebrow="Your Studio"
            title="My"
            highlight="Account"
            subtitle="Bookings, orders, and loyalty in one place."
          />
          <div className="text-right space-y-2 pt-2">
            <p className="text-gray-400 text-sm">
              Signed in as{" "}
              <span className="text-white font-medium">{session.displayName}</span>
            </p>
            <button
              type="button"
              onClick={handleLogout}
              className="text-sm text-gray-400 hover:text-gold transition-colors underline-offset-4 hover:underline"
            >
              Log out
            </button>
          </div>
        </div>

        <nav
          aria-label="Account sections"
          className="flex flex-wrap gap-3 mb-10"
        >
          <Link
            href="/account/bookings"
            className={tabClass(activeTab === "bookings")}
            aria-current={activeTab === "bookings" ? "page" : undefined}
          >
            Bookings
          </Link>
          <Link
            href="/account/orders"
            className={tabClass(activeTab === "orders")}
            aria-current={activeTab === "orders" ? "page" : undefined}
          >
            Orders
          </Link>
        </nav>

        {children}
      </div>
    </section>
  );
}
