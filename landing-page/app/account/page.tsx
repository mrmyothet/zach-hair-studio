"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import {
  clearSession,
  getSession,
  requireAuth,
  type AuthSession,
} from "@/lib/auth";

export default function AccountPage() {
  const router = useRouter();
  const [session, setLocalSession] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (!requireAuth()) return;
    setLocalSession(getSession());
    setReady(true);
  }, []);

  function handleLogout() {
    clearSession();
    router.push("/account/login");
  }

  if (!ready || !session) {
    return (
      <>
        <Navbar />
        <main className="min-h-screen bg-charcoal-light pt-32">
          <p className="text-center text-gray-400 text-sm py-16">Loading…</p>
        </main>
      </>
    );
  }

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-5xl mx-auto px-6">
            <SectionHeading
              eyebrow="Your Studio"
              title="My"
              highlight="Account"
              subtitle="Bookings, orders, and loyalty in one place."
            />

            <div className="max-w-md mx-auto text-center space-y-6">
              <p className="text-gray-300 text-sm">
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
        </section>
      </main>
      <Footer />
    </>
  );
}
