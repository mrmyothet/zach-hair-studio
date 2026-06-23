import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Zach Hair Studio",
  description:
    "At Zach Hair Studio, we blend artistry with expertise to craft looks that are uniquely yours — from precision cuts to bold transformations.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className="bg-charcoal text-white font-sans antialiased">
        {children}
      </body>
    </html>
  );
}
