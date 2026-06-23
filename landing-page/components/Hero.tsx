"use client";

import { useEffect, useState } from "react";
import Image from "next/image";

const slides = [
  { src: "/hair_color_7.jpg", alt: "Premium Styling 1" },
  { src: "/hair_color_8.jpg", alt: "Premium Styling 2" },
  { src: "/hair_color_9.jpg", alt: "Premium Styling 3" },
];

const stats = [
  { value: "18+", label: "Years Experience" },
  { value: "5K+", label: "Happy Clients" },
  { value: "20+", label: "Style Experts" },
];

function Slideshow() {
  const [current, setCurrent] = useState(0);

  useEffect(() => {
    const timer = setInterval(
      () => setCurrent((c) => (c + 1) % slides.length),
      4000,
    );
    return () => clearInterval(timer);
  }, []);

  return (
    <div className="relative w-full h-full">
      {slides.map((slide, i) => (
        <Image
          key={slide.src}
          src={slide.src}
          alt={slide.alt}
          fill
          priority={i === 0}
          sizes="(max-width: 768px) 80vw, 384px"
          className={`object-cover transition-opacity duration-1000 ${
            i === current ? "opacity-100" : "opacity-0"
          }`}
        />
      ))}

      <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-charcoal via-charcoal/70 to-transparent p-6 text-center space-y-2">
        <p className="text-gold font-serif text-2xl">Premium Styling</p>
        <p className="text-gray-300 text-sm">Your look, perfected</p>
        <div className="flex justify-center gap-2 pt-1">
          {slides.map((slide, i) => (
            <button
              key={slide.src}
              type="button"
              onClick={() => setCurrent(i)}
              aria-label={`Slide ${i + 1}`}
              className={`w-2.5 h-2.5 rounded-full transition-all ${
                i === current ? "bg-gold" : "bg-gold/40"
              }`}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

export default function Hero() {
  return (
    <section
      id="home"
      className="hero-bg min-h-screen flex items-center relative overflow-hidden pt-20"
    >
      <div className="absolute top-1/4 right-10 w-72 h-72 bg-gold/10 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute bottom-10 left-0 w-96 h-96 bg-gold/5 rounded-full blur-3xl pointer-events-none" />

      <div className="max-w-7xl mx-auto px-6 py-24 grid md:grid-cols-2 gap-12 items-center">
        <div className="space-y-8">
          <div className="inline-flex items-center gap-2 bg-gold/10 border border-gold/30 rounded-full px-4 py-1.5 text-gold text-xs uppercase tracking-widest">
            <span className="w-1.5 h-1.5 bg-gold rounded-full animate-pulse" />
            Premium Hair Experience
          </div>
          <h1 className="font-serif text-5xl md:text-6xl lg:text-7xl leading-tight">
            <span className="gold-gradient font-bold">Elevate</span>
            <br />
            <span className="text-white">Your Style,</span>
            <br />
            <span className="text-gray-400 text-4xl md:text-5xl font-light">
              Define Your Story.
            </span>
          </h1>
          <p className="text-gray-400 text-lg leading-relaxed max-w-md">
            At Zach Hair Studio, we blend artistry with expertise to craft looks that are
            uniquely yours — from precision cuts to bold transformations.
          </p>
          <div className="flex flex-wrap gap-4">
            <a
              href="#contact"
              className="bg-gold hover:bg-gold-dark text-charcoal font-semibold px-8 py-3.5 rounded-full transition-all duration-300 hover:shadow-xl hover:shadow-gold/30 text-sm uppercase tracking-wider"
            >
              Book Appointment
            </a>
            <a
              href="#services"
              className="border border-gold/40 hover:border-gold text-gold hover:bg-gold/10 font-semibold px-8 py-3.5 rounded-full transition-all duration-300 text-sm uppercase tracking-wider"
            >
              Our Services
            </a>
          </div>

          <div className="flex gap-8 pt-4 border-t border-white/10">
            {stats.map((stat, i) => (
              <div key={stat.label} className={i > 0 ? "border-l border-white/10 pl-8" : ""}>
                <p className="text-gold text-3xl font-bold font-serif">{stat.value}</p>
                <p className="text-gray-500 text-xs uppercase tracking-wider mt-1">
                  {stat.label}
                </p>
              </div>
            ))}
          </div>
        </div>

        <div className="relative flex justify-center">
          <div className="relative w-80 h-96 md:w-96 md:h-[480px]">
            <div className="absolute inset-0 bg-gradient-to-br from-gold/30 to-transparent rounded-3xl border border-gold/20 overflow-hidden">
              <Slideshow />
            </div>

            <div className="absolute -bottom-4 -left-4 bg-charcoal-light border border-gold/30 rounded-2xl px-4 py-3 shadow-xl">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 bg-gold rounded-full flex items-center justify-center">
                  <svg className="w-4 h-4 text-charcoal" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                  </svg>
                </div>
                <div>
                  <p className="text-white text-sm font-semibold">4.9 Rating</p>
                  <p className="text-gray-500 text-xs">500+ Reviews</p>
                </div>
              </div>
            </div>

            <div className="absolute -top-4 -right-4 bg-gold text-charcoal rounded-2xl px-4 py-2 shadow-xl">
              <p className="text-xs font-bold uppercase tracking-wider">Open Today</p>
              <p className="text-xs font-medium">9AM – 7:30PM</p>
            </div>
          </div>
        </div>
      </div>

      <div className="absolute bottom-8 left-1/2 -translate-x-1/2 flex flex-col items-center gap-2 animate-bounce">
        <p className="text-gray-500 text-xs uppercase tracking-widest">Scroll</p>
        <svg className="w-5 h-5 text-gold" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </div>
    </section>
  );
}
