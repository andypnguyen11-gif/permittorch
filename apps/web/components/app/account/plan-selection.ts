import type { PlanTier } from "@permittorch/types";

export const PLAN_TIERS: readonly PlanTier[] = ["STARTER", "PRO", "TERRITORY"];
export const PLAN_LABELS: Record<PlanTier, string> = { STARTER: "Starter", PRO: "Pro", TERRITORY: "Territory" };

/** Market allowance per plan — mirrors the API's checkout validation. */
export const MAX_TERRITORY_MARKETS = 5;
export function marketLimit(plan: PlanTier): number {
  return plan === "TERRITORY" ? MAX_TERRITORY_MARKETS : 1;
}

/** Parses an untrusted `?plan=` value against the PlanTier union (case-insensitive). */
export function parsePlanTier(value: unknown): PlanTier | null {
  if (typeof value !== "string") return null;
  const upper = value.trim().toUpperCase();
  return (PLAN_TIERS as readonly string[]).includes(upper) ? (upper as PlanTier) : null;
}

/**
 * Next selection after the user picks `slug`. Starter/Pro hold exactly one
 * market, so picking replaces it; Territory toggles, capped at five.
 */
export function toggleMarket(plan: PlanTier, selected: string[], slug: string): string[] {
  if (plan !== "TERRITORY") return [slug];
  if (selected.includes(slug)) return selected.filter((s) => s !== slug);
  if (selected.length >= MAX_TERRITORY_MARKETS) return selected;
  return [...selected, slug];
}

/** Trims a selection to what `plan` allows (e.g. switching from Territory down to Pro). */
export function fitSelection(plan: PlanTier, selected: string[]): string[] {
  return selected.slice(0, marketLimit(plan));
}

/** True when the selection is a valid checkout body for `plan`. */
export function isValidSelection(plan: PlanTier, selected: string[]): boolean {
  return selected.length >= 1 && selected.length <= marketLimit(plan)
    && new Set(selected).size === selected.length;
}
