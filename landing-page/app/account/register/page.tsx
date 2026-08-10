"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import ClaimHistoryPanel from "@/components/ClaimHistoryPanel";
import { AlertIcon } from "@/components/icons";
import {
  AuthApiError,
  getSession,
  registerClient,
  setSession,
} from "@/lib/auth";

const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-3 text-white placeholder-gray-600 text-sm outline-none transition-colors";

function Field({
  label,
  children,
  helper,
}: {
  label: string;
  children: React.ReactNode;
  helper?: string;
}) {
  return (
    <div>
      <label className="text-gray-400 text-xs uppercase tracking-wider block mb-2">
        {label}
      </label>
      {children}
      {helper ? <p className="text-xs text-gray-500 mt-2">{helper}</p> : null}
    </div>
  );
}

export default function AccountRegisterPage() {
  const router = useRouter();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [showClaim, setShowClaim] = useState(false);

  useEffect(() => {
    if (getSession() && !showClaim) {
      router.replace("/account");
    }
  }, [router, showClaim]);

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const session = await registerClient({
        email: email.trim(),
        password,
        confirmPassword,
      });
      setSession(session);
      setShowClaim(true);
    } catch (err) {
      if (err instanceof AuthApiError) {
        setError(err.message);
      } else {
        setError("Check your details and try again.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-5xl mx-auto px-6">
            <SectionHeading
              eyebrow="Client Account"
              title=""
              highlight="Account"
              subtitle="Save your history and manage upcoming visits."
            />

            {showClaim ? (
              <ClaimHistoryPanel />
            ) : (
              <div className="w-full max-w-md mx-auto bg-charcoal border border-white/5 rounded-2xl p-8">
                <form onSubmit={handleSubmit} className="space-y-5">
                  <Field
                    label="Email"
                    helper="We'll use this to match any guest bookings or orders."
                  >
                    <input
                      type="email"
                      name="email"
                      autoComplete="email"
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
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      className={inputClass}
                      disabled={submitting}
                    />
                  </Field>

                  <Field label="Confirm password">
                    <input
                      type="password"
                      name="confirmPassword"
                      autoComplete="new-password"
                      required
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      className={inputClass}
                      disabled={submitting}
                    />
                  </Field>

                  {error ? (
                    <div
                      role="alert"
                      className="flex items-start gap-2 text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
                    >
                      <AlertIcon className="w-5 h-5 flex-shrink-0 mt-0.5" />
                      <span>
                        <strong className="font-semibold">
                          {"Couldn't Create Account"}
                        </strong>
                        <span className="block mt-1">{error}</span>
                      </span>
                    </div>
                  ) : null}

                  <button
                    type="submit"
                    disabled={submitting}
                    className="w-full bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm rounded-full px-4 py-3 transition-colors disabled:opacity-60 disabled:cursor-not-allowed min-h-11"
                  >
                    {submitting ? "Creating account…" : "Create Account"}
                  </button>

                  <p className="text-center text-sm text-gray-400">
                    Already have an account?{" "}
                    <Link href="/account/login" className="text-gold hover:underline">
                      Log in
                    </Link>
                  </p>
                </form>
              </div>
            )}
          </div>
        </section>
      </main>
      <Footer />
    </>
  );
}
