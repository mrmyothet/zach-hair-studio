"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { getSession } from "@/lib/auth";

/** Entry: send authenticated staff to /schedule, everyone else to /login. */
export default function HomePage() {
  const router = useRouter();

  useEffect(() => {
    router.replace(getSession() ? "/schedule" : "/login");
  }, [router]);

  return (
    <main className="min-h-screen flex items-center justify-center bg-surface text-muted text-sm">
      Loading…
    </main>
  );
}
