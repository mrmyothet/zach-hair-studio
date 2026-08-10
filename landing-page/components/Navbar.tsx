"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { cartItemCount, CART_UPDATED_EVENT, fetchCart } from "@/lib/cart";
import { cartNavLink, navLinks } from "@/lib/data";
import { ArrowRightIcon, CartIcon } from "./icons";

function Logo() {
  return (
    <Link href="/#home" className="flex items-center gap-3">
      <div className="w-10 h-10 bg-gold rounded-full flex items-center justify-center">
        <span className="text-charcoal font-bold text-lg">Z</span>
      </div>
      <div>
        <span className="text-gold font-serif text-xl font-bold tracking-wide">ZACH</span>
        <span className="text-white text-xl ml-1 tracking-widest font-light">HAIR STUDIO</span>
      </div>
    </Link>
  );
}

function CartBadge({ count }: { count: number }) {
  if (count < 1) return null;
  return (
    <span className="absolute -top-1.5 -right-2 bg-gold text-charcoal text-xs font-semibold w-5 h-5 rounded-full flex items-center justify-center">
      {count > 99 ? "99+" : count}
    </span>
  );
}

export default function Navbar() {
  const [open, setOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const [cartCount, setCartCount] = useState(0);

  const refreshCartCount = useCallback(async () => {
    try {
      const cart = await fetchCart();
      setCartCount(cartItemCount(cart));
    } catch {
      // Keep last known count on transient failures — link still navigates to /cart.
    }
  }, []);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 80);
    window.addEventListener("scroll", onScroll);
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    void refreshCartCount();
    const onCartUpdated = () => {
      void refreshCartCount();
    };
    window.addEventListener(CART_UPDATED_EVENT, onCartUpdated);
    return () => window.removeEventListener(CART_UPDATED_EVENT, onCartUpdated);
  }, [refreshCartCount]);

  return (
    <nav
      className={`fixed top-0 left-0 right-0 z-50 bg-charcoal/90 backdrop-blur-md border-b border-gold/20 transition-all duration-300 ${
        scrolled ? "py-2" : "py-4"
      }`}
    >
      <div className="max-w-7xl mx-auto px-6 flex items-center justify-between">
        <Logo />

        <ul className="hidden md:flex items-center gap-8 text-sm tracking-wider uppercase">
          {navLinks.map((link) => (
            <li key={link.href}>
              <Link
                href={link.href}
                className="nav-link text-gray-300 hover:text-gold transition-colors"
              >
                {link.label}
              </Link>
            </li>
          ))}
        </ul>

        <div className="hidden md:flex items-center gap-4">
          <Link
            href={cartNavLink.href}
            className="relative inline-flex items-center gap-2 text-gray-300 hover:text-gold transition-colors text-sm tracking-wider uppercase"
          >
            <span className="relative">
              <CartIcon className="w-5 h-5 text-gold" />
              <CartBadge count={cartCount} />
            </span>
            {cartNavLink.label}
          </Link>

          <Link
            href="/book"
            className="inline-flex items-center gap-2 bg-gold hover:bg-gold-dark text-charcoal font-semibold text-sm px-5 py-2.5 rounded-full transition-all duration-300 hover:shadow-lg hover:shadow-gold/30"
          >
            Book Now
            <ArrowRightIcon className="w-4 h-4" />
          </Link>
        </div>

        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          className="md:hidden text-gold focus:outline-none"
          aria-label="Toggle menu"
          aria-expanded={open}
        >
          <svg className="w-7 h-7" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
      </div>

      {open && (
        <div className="md:hidden bg-charcoal-light border-t border-gold/20">
          <ul className="flex flex-col px-6 py-4 gap-4 text-sm uppercase tracking-wider">
            {navLinks.map((link) => (
              <li key={link.href}>
                <Link
                  href={link.href}
                  className="text-gray-300 hover:text-gold block transition-colors"
                  onClick={() => setOpen(false)}
                >
                  {link.label}
                </Link>
              </li>
            ))}
            <li>
              <Link
                href={cartNavLink.href}
                className="relative inline-flex items-center gap-2 text-gray-300 hover:text-gold transition-colors"
                onClick={() => setOpen(false)}
              >
                <span className="relative">
                  <CartIcon className="w-5 h-5 text-gold" />
                  <CartBadge count={cartCount} />
                </span>
                {cartNavLink.label}
              </Link>
            </li>
            <li>
              <Link
                href="/book"
                className="bg-gold text-charcoal font-semibold text-center py-2 rounded-full block"
                onClick={() => setOpen(false)}
              >
                Book Now
              </Link>
            </li>
          </ul>
        </div>
      )}
    </nav>
  );
}
