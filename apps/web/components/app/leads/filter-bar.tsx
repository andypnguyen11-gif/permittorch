"use client";
import { useRouter } from "next/navigation";
import { X } from "lucide-react";
import type { FireCategory, PermitStatus } from "@permittorch/types";
import type { LeadsQuery } from "@/lib/api";
import { CATEGORY_LABELS } from "@/components/app/category-chip";
import { Button } from "@/components/ui/button";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { buildLeadsSearch, FIRE_CATEGORIES, PERMIT_STATUSES } from "./query";

export type FilterKey = "category" | "score" | "age" | "status";

// Pure mapping so the filter→query logic is unit-testable without the popup.
export function nextSearchFor(query: LeadsQuery, key: FilterKey, value: string): string {
  const next: LeadsQuery = { ...query, page: undefined };
  if (key === "category") next.category = value === "all" ? undefined : (value as FireCategory);
  if (key === "score") next.minScore = value === "all" ? undefined : Number.parseInt(value, 10);
  if (key === "age") next.maxAgeDays = value === "all" ? undefined : Number.parseInt(value, 10);
  if (key === "status") next.status = value === "all" ? undefined : (value as PermitStatus);
  return buildLeadsSearch(next);
}

type Option = { value: string; label: string };

const CATEGORY_OPTIONS: Option[] = [
  { value: "all", label: "All" },
  ...FIRE_CATEGORIES.map((c) => ({ value: c, label: CATEGORY_LABELS[c] })),
];
const SCORE_OPTIONS: Option[] = [
  { value: "all", label: "Any" }, { value: "90", label: "90+" },
  { value: "80", label: "80+" }, { value: "70", label: "70+" },
];
const AGE_OPTIONS: Option[] = [
  { value: "all", label: "Any time" }, { value: "1", label: "Today" },
  { value: "3", label: "Last 3 days" }, { value: "7", label: "Last 7 days" },
  { value: "30", label: "Last 30 days" },
];
export const STATUS_LABELS: Record<PermitStatus, string> = {
  NEW: "New", ACTIVE: "Active", INSPECTION: "Inspection",
  FAILED: "Failed", CLOSED: "Closed", UNKNOWN: "Unknown",
};
const STATUS_OPTIONS: Option[] = [
  { value: "all", label: "All" },
  ...PERMIT_STATUSES.map((s) => ({ value: s, label: STATUS_LABELS[s] })),
];

function withCurrent(options: Option[], value: string): Option[] {
  // A deep link may carry a value outside the preset list (e.g. minScore=85).
  return options.some((o) => o.value === value) ? options : [...options, { value, label: value }];
}

function FilterSelect({ label, value, options, onChange, width }: {
  label: string; value: string; options: Option[]; onChange: (v: string) => void; width: string;
}) {
  const items = withCurrent(options, value);
  return (
    <Select items={items} value={value} onValueChange={(v) => onChange(String(v ?? "all"))}>
      <SelectTrigger aria-label={label} className={`h-9 bg-white ${width}`}>
        <span className="text-xs font-semibold text-stone-500">{label}</span>
        <SelectValue className="font-medium text-stone-900" />
      </SelectTrigger>
      <SelectContent alignItemWithTrigger={false} align="start">
        {items.map((o) => (
          <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export function FilterBar({ query }: { query: LeadsQuery }) {
  const router = useRouter();
  const push = (key: FilterKey, value: string) =>
    router.push(`/app/leads${nextSearchFor(query, key, value)}`);
  const active = query.category || query.minScore != null || query.maxAgeDays != null || query.status;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <FilterSelect label="Category" width="w-56" value={query.category ?? "all"}
        options={CATEGORY_OPTIONS} onChange={(v) => push("category", v)} />
      <FilterSelect label="Score" width="w-32" value={query.minScore != null ? String(query.minScore) : "all"}
        options={SCORE_OPTIONS} onChange={(v) => push("score", v)} />
      <FilterSelect label="Age" width="w-44" value={query.maxAgeDays != null ? String(query.maxAgeDays) : "all"}
        options={AGE_OPTIONS} onChange={(v) => push("age", v)} />
      <FilterSelect label="Status" width="w-40" value={query.status ?? "all"}
        options={STATUS_OPTIONS} onChange={(v) => push("status", v)} />
      {active && (
        <Button variant="ghost" size="sm" className="h-9 text-stone-500"
          onClick={() => router.push(`/app/leads${buildLeadsSearch({ market: query.market, q: query.q })}`)}>
          <X aria-hidden /> Clear filters
        </Button>
      )}
    </div>
  );
}
