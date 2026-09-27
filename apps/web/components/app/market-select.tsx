"use client";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { MapPin } from "lucide-react";
import type { Market } from "@permittorch/types";
import type { LeadsQuery } from "@/lib/api";
import { track } from "@/lib/analytics";
import { buildLeadsSearch, parseLeadsSearchParams } from "@/components/app/leads/query";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

export const ALL_MARKETS = "all";

/**
 * The leads URL after applying `patch` to the current leads filters. On
 * /app/leads every other filter (and q) is kept; elsewhere we start fresh.
 * The page always resets, since the result set changes.
 */
export function leadsHrefWith(
  pathname: string,
  searchParams: URLSearchParams,
  patch: Partial<LeadsQuery>,
): string {
  const current = pathname === "/app/leads"
    ? parseLeadsSearchParams(Object.fromEntries(searchParams.entries()))
    : {};
  return `/app/leads${buildLeadsSearch({ ...current, ...patch, page: undefined })}`;
}

/** Reads the current leads query from the URL (empty off /app/leads). */
export function useLeadsLocation() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const onLeads = pathname === "/app/leads";
  return {
    pathname,
    searchParams,
    currentMarket: onLeads ? searchParams.get("market") ?? ALL_MARKETS : ALL_MARKETS,
    currentQ: onLeads ? searchParams.get("q") ?? "" : "",
  };
}

export function MarketSelect({ markets, className, onChanged }: {
  markets: Market[];
  className?: string;
  onChanged?: () => void;
}) {
  const router = useRouter();
  const { pathname, searchParams, currentMarket } = useLeadsLocation();
  const items = [
    { value: ALL_MARKETS, label: "All my markets" },
    ...markets.map((m) => ({ value: m.slug, label: m.name })),
  ];

  return (
    <Select
      items={items}
      value={currentMarket}
      onValueChange={(slug) => {
        const market = slug && slug !== ALL_MARKETS ? String(slug) : undefined;
        track("filter_changed", { filter: "market", value: market ?? ALL_MARKETS });
        router.push(leadsHrefWith(pathname, searchParams, { market }));
        onChanged?.();
      }}
    >
      <SelectTrigger aria-label="Market" className={cn("h-9 w-48 bg-white", className)}>
        <MapPin className="size-4 text-orange-500" aria-hidden />
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {items.map((item) => (
          <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
