"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Footer from "@/components/Footer";
import LoyaltyBalanceStrip from "@/components/LoyaltyBalanceStrip";
import Navbar from "@/components/Navbar";
import { requireAuth } from "@/lib/auth";

/** /account defaults to Bookings tab (D-06). Bookings | Orders deep links: /account/bookings, /account/orders. */
export default function AccountPage() {
  const router = useRouter();

  useEffect(() => {
    if (!requireAuth()) return;
    router.replace("/account/bookings");
  }, [router]);

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <div className="max-w-5xl mx-auto px-6 py-16">
          <LoyaltyBalanceStrip />
          <p className="text-center text-gray-400 text-sm">Loading…</p>
        </div>
      </main>
      <Footer />
    </>
  );
}
