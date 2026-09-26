"use client";

import Link from "next/link";
import { useState } from "react";
import { Button } from "@/components/ui/button";

const NAV_LINKS = [
  { label: "Leads", href: "/fire-protection-leads" },
  { label: "Markets", href: "/locations" },
  { label: "How It Works", href: "/how-it-works" },
  { label: "Pricing", href: "/pricing" },
  { label: "Resources", href: "/blog" },
];

export function FlameMark({ className = "h-6 w-6 text-orange-500" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" className={className}>
      <path d="M12 2c.4 2.9-1.4 4.7-3 6.5C7.4 10.3 6 12.3 6 15a6 6 0 0 0 12 0c0-2.3-1-4.3-2.4-6C14.1 7.1 12.6 5.1 12 2Zm0 19a4 4 0 0 1-4-4c0-1.7.8-3 2-4.3.3 1.2 1 2.3 2 3.3 1-1 1.7-2.1 2-3.3 1.2 1.3 2 2.6 2 4.3a4 4 0 0 1-4 4Z" />
    </svg>
  );
}

export function SiteNav() {
  const [open, setOpen] = useState(false);
  return (
    <header className="sticky top-0 z-40 border-b border-neutral-200 bg-white/90 backdrop-blur">
      <nav className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
        <Link href="/" className="flex items-center gap-2 text-xl font-bold tracking-tight">
          <FlameMark />
          <span>Permit<span className="text-orange-500">Torch</span></span>
        </Link>

        <div className="hidden items-center gap-6 md:flex">
          {NAV_LINKS.map((l) => (
            <Link key={l.href} href={l.href}
              className="text-sm font-medium text-neutral-600 transition-colors hover:text-neutral-900">
              {l.label}
            </Link>
          ))}
        </div>

        <div className="hidden items-center gap-2 md:flex">
          <Button variant="ghost" render={<Link href="/login">Login</Link>} />
          <Button className="bg-orange-500 text-white hover:bg-orange-600"
            render={<Link href="/signup">Start Free</Link>} />
        </div>

        <button type="button" aria-label="Toggle menu" aria-expanded={open}
          onClick={() => setOpen((v) => !v)}
          className="rounded-md p-2 text-neutral-600 hover:bg-neutral-100 md:hidden">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-6 w-6">
            {open
              ? <path strokeLinecap="round" d="M6 6l12 12M18 6L6 18" />
              : <path strokeLinecap="round" d="M4 7h16M4 12h16M4 17h16" />}
          </svg>
        </button>
      </nav>

      {open && (
        <div className="border-t border-neutral-200 bg-white px-4 pb-4 md:hidden">
          {NAV_LINKS.map((l) => (
            <Link key={l.href} href={l.href} onClick={() => setOpen(false)}
              className="block py-2.5 text-sm font-medium text-neutral-700">
              {l.label}
            </Link>
          ))}
          <div className="mt-3 flex gap-2">
            <Button variant="outline" className="flex-1" render={<Link href="/login">Login</Link>} />
            <Button className="flex-1 bg-orange-500 text-white hover:bg-orange-600"
              render={<Link href="/signup">Start Free</Link>} />
          </div>
        </div>
      )}
    </header>
  );
}
