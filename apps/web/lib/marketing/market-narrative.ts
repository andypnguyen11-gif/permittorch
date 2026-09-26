import type { FireCategory, MarketStats } from "@permittorch/types";

const CATEGORY_WORK: Record<FireCategory, string> = {
  FIRE_SPRINKLER: "sprinkler work",
  FIRE_ALARM: "fire alarm work",
  FIRE_SUPPRESSION: "fire suppression work",
  KITCHEN_SUPPRESSION: "kitchen hood suppression work",
  FIRE_INSPECTION: "fire inspections",
  VIOLATION_CORRECTION: "violation corrections",
  GENERAL_FIRE_PROTECTION: "general fire protection work",
};

const pct = (n: number, total: number) => Math.round((n / total) * 100);

/**
 * A short, data-driven description of a market's last 30 days, e.g.
 * "In the last 30 days, 36% of Austin's fire-protection permits were sprinkler
 * work. Fire alarm work was next at 26%." Null when there is nothing to describe.
 */
export function marketNarrative(city: string, stats: MarketStats): string | null {
  const total = stats.totalLast30Days;
  if (total <= 0) return null;
  const ranked = (Object.entries(stats.byCategory) as [FireCategory, number][])
    .filter(([, n]) => n > 0)
    .sort(([, a], [, b]) => b - a);
  if (ranked.length === 0) return null;
  const [topCat, topN] = ranked[0];
  let text = `In the last 30 days, ${pct(topN, total)}% of ${city}'s fire-protection permits were ${CATEGORY_WORK[topCat]}.`;
  if (ranked.length > 1) {
    const [nextCat, nextN] = ranked[1];
    const label = CATEGORY_WORK[nextCat];
    text += ` ${label[0].toUpperCase()}${label.slice(1)} was next at ${pct(nextN, total)}%.`;
  }
  return text;
}
