import type { Market, MarketStats } from "@permittorch/types";
import { getAllMarketStats, getMarkets } from "@/lib/api";

export interface MarketWithData { market: Market; stats: MarketStats }

/**
 * Markets the app serves to subscribers but does not market yet. Mirrors the API seeder's
 * NotYetPublicMarketSlugs: Central New Jersey and Fort Bend County stay off public pages until
 * their data is in and checked. Removing a slug here (and adding the market to the source
 * registry) markets it.
 */
export const NOT_YET_PUBLIC_MARKET_SLUGS: ReadonlySet<string> = new Set([
  "central-new-jersey-nj",
  "fort-bend-county-tx",
]);

export function isPublicMarket(slug: string): boolean {
  return !NOT_YET_PUBLIC_MARKET_SLUGS.has(slug);
}

/** A market has real data when it has been updated at least once and has recent fire permits. */
export function hasRealData(stats: MarketStats): boolean {
  return stats.lastUpdatedAt !== null && stats.totalLast30Days > 0;
}

/**
 * Markets that may get public marketing surface area (pages, sitemap entries,
 * dropdown options). Two requests total (market list + bulk stats), never one
 * per market: every marketing render calls this, and per-market fetches trip
 * the API's anonymous rate limit. A market without a stats entry is excluded
 * rather than failing the whole page — no thin pages.
 */
export async function getMarketsWithData(): Promise<MarketWithData[]> {
  const [markets, allStats] = await Promise.all([getMarkets(), getAllMarketStats()]);
  const statsBySlug = new Map(allStats.map((s) => [s.slug, s]));
  const out: MarketWithData[] = [];
  for (const market of markets) {
    const stats = statsBySlug.get(market.slug);
    if (stats && hasRealData(stats) && isPublicMarket(market.slug)) out.push({ market, stats });
  }
  return out;
}
