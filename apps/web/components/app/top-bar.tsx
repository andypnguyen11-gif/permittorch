"use client";
import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import type { AccountMe, Market, PlanTier } from "@permittorch/types";
import { AccountMenu } from "@/components/app/account-menu";
import { MobileNav } from "@/components/app/mobile-nav";
import { MarketSelect, leadsHrefWith, useLeadsLocation } from "@/components/app/market-select";

export function TopBar({ markets, email, role = "MEMBER", plan }: {
  markets: Market[];
  email: string;
  role?: AccountMe["role"];
  plan?: PlanTier | null;
}) {
  const router = useRouter();
  const { pathname, searchParams, currentQ } = useLeadsLocation();
  const inputRef = useRef<HTMLInputElement>(null);

  // ⌘K / Ctrl+K focuses the search input (command-palette stub).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-3 border-b border-border bg-white/90 px-4 backdrop-blur supports-backdrop-filter:bg-white/75 sm:px-6">
      <MobileNav role={role} plan={plan} markets={markets} />
      <form
        role="search"
        className="relative min-w-0 flex-1 max-w-xl"
        onSubmit={(e) => {
          e.preventDefault();
          const q = inputRef.current?.value.trim() ?? "";
          // Keep the other leads filters (market, category, …); reset the page.
          router.push(leadsHrefWith(pathname, searchParams, { q: q || undefined }));
        }}
      >
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-stone-400" aria-hidden />
        <input
          key={currentQ}
          ref={inputRef}
          type="search"
          name="q"
          defaultValue={currentQ}
          aria-label="Search leads"
          placeholder="Search permits, projects, addresses…"
          className="h-9 w-full rounded-lg border border-border bg-stone-50 pr-12 pl-9 text-sm outline-none transition-colors placeholder:text-stone-400 focus:border-orange-400 focus:bg-white focus:ring-3 focus:ring-orange-500/15"
        />
        <kbd className="pointer-events-none absolute top-1/2 right-2.5 hidden -translate-y-1/2 rounded border border-border bg-white px-1.5 font-sans text-[11px] text-stone-400 sm:block">
          ⌘K
        </kbd>
      </form>
      <div className="ml-auto flex items-center gap-3">
        <MarketSelect markets={markets} className="hidden md:flex" />
        <AccountMenu email={email} />
      </div>
    </header>
  );
}
