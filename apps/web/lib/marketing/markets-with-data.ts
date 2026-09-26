import type { Market, MarketStats } from "@permittorch/types";
import { getMarketStats, getMarkets } from "@/lib/api";

export interface MarketWithData { market: Market; stats: MarketStats }

/** A market has real data when it has been updated at least once and has recent fire permits. */
export function hasRealData(stats: MarketStats): boolean {
  return stats.lastUpdatedAt !== null && stats.totalLast30Days > 0;
}

/**
 * Markets that may get public marketing surface area (pages, sitemap entries,
 * dropdown options). A market whose stats request fails is excluded rather than
 * failing the whole page — no thin pages, and no outage cascade.
 */
export async function getMarketsWithData(): Promise<MarketWithData[]> {
  const markets = await getMarkets();
  const settled = await Promise.allSettled(markets.map((m) => getMarketStats(m.slug)));
  const out: MarketWithData[] = [];
  settled.forEach((r, i) => {
    if (r.status === "fulfilled" && hasRealData(r.value)) out.push({ market: markets[i], stats: r.value });
  });
  return out;
}
