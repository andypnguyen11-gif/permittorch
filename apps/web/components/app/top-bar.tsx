"use client";
import { useEffect, useRef } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { MapPin, Search } from "lucide-react";
import type { AccountMe, Market, PlanTier } from "@permittorch/types";
import { AccountMenu } from "@/components/app/account-menu";
import { MobileNav } from "@/components/app/mobile-nav";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";

const ALL = "all";

export function TopBar({ markets, email, role = "MEMBER", plan }: {
  markets: Market[];
  email: string;
  role?: AccountMe["role"];
  plan?: PlanTier | null;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const inputRef = useRef<HTMLInputElement>(null);
  const onLeads = pathname === "/app/leads";
  const currentMarket = onLeads ? searchParams.get("market") ?? ALL : ALL;
  const currentQ = onLeads ? searchParams.get("q") ?? "" : "";

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

  const items = [
    { value: ALL, label: "All my markets" },
    ...markets.map((m) => ({ value: m.slug, label: m.name })),
  ];

  return (
    <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-3 border-b border-border bg-white/90 px-4 backdrop-blur supports-backdrop-filter:bg-white/75 sm:px-6">
      <MobileNav role={role} plan={plan} />
      <form
        role="search"
        className="relative min-w-0 flex-1 max-w-xl"
        onSubmit={(e) => {
          e.preventDefault();
          const q = inputRef.current?.value.trim() ?? "";
          router.push(q ? `/app/leads?q=${encodeURIComponent(q)}` : "/app/leads");
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
        <Select
          items={items}
          value={currentMarket}
          onValueChange={(slug) => {
            router.push(slug && slug !== ALL ? `/app/leads?market=${encodeURIComponent(String(slug))}` : "/app/leads");
          }}
        >
          <SelectTrigger aria-label="Market" className="hidden h-9 w-48 bg-white md:flex">
            <MapPin className="size-4 text-orange-500" aria-hidden />
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {items.map((item) => (
              <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <AccountMenu email={email} />
      </div>
    </header>
  );
}
