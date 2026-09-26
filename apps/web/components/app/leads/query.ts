import type { LeadsQuery } from "@/lib/api";
import type { FireCategory, PermitStatus } from "@permittorch/types";

export const FIRE_CATEGORIES: FireCategory[] = [
  "FIRE_SPRINKLER", "FIRE_ALARM", "FIRE_SUPPRESSION", "KITCHEN_SUPPRESSION",
  "FIRE_INSPECTION", "VIOLATION_CORRECTION", "GENERAL_FIRE_PROTECTION",
];
export const PERMIT_STATUSES: PermitStatus[] = [
  "NEW", "ACTIVE", "INSPECTION", "FAILED", "CLOSED", "UNKNOWN",
];

type SP = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined): string | undefined => {
  const s = Array.isArray(v) ? v[0] : v;
  return s === undefined || s === "" ? undefined : s;
};
const int = (v: string | undefined, min: number, max: number): number | undefined => {
  if (v === undefined || !/^\d+$/.test(v)) return undefined;
  const n = Number.parseInt(v, 10);
  return n >= min && n <= max ? n : undefined;
};

const MAX_Q_LENGTH = 200;

// Client-side shaping of URL state only; the API re-validates every filter.
export function parseLeadsSearchParams(sp: SP): LeadsQuery {
  const query: LeadsQuery = {};
  const market = first(sp.market);
  if (market) query.market = market;
  const category = first(sp.category);
  if (category && (FIRE_CATEGORIES as string[]).includes(category)) {
    query.category = category as FireCategory;
  }
  const minScore = int(first(sp.minScore), 1, 100);
  if (minScore !== undefined) query.minScore = minScore;
  const maxAgeDays = int(first(sp.maxAgeDays), 1, 365);
  if (maxAgeDays !== undefined) query.maxAgeDays = maxAgeDays;
  const status = first(sp.status);
  if (status && (PERMIT_STATUSES as string[]).includes(status)) {
    query.status = status as PermitStatus;
  }
  const q = first(sp.q)?.trim().slice(0, MAX_Q_LENGTH);
  if (q) query.q = q;
  const page = int(first(sp.page), 1, 10_000);
  if (page !== undefined) query.page = page;
  return query;
}

export function buildLeadsSearch(query: LeadsQuery): string {
  const params = new URLSearchParams();
  if (query.market) params.set("market", query.market);
  if (query.category) params.set("category", query.category);
  if (query.minScore != null) params.set("minScore", String(query.minScore));
  if (query.maxAgeDays != null) params.set("maxAgeDays", String(query.maxAgeDays));
  if (query.status) params.set("status", query.status);
  if (query.q) params.set("q", query.q);
  if (query.page != null && query.page > 1) params.set("page", String(query.page));
  const s = params.toString();
  return s ? `?${s}` : "";
}
