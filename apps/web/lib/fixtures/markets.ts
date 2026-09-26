// MARKET FIXTURES (WS3-owned). Illustrative mock markets/stats used by
// lib/api.ts's mock branch (getMarkets/getMarketStats) and by marketing pages
// and tests. Keep both export names — lib/api.ts imports them by name.
import type { Market, MarketStats } from "@permittorch/types";

export const mockMarkets: Market[] = [
  { id: "5f0c2c1a-9d61-4b1e-8a3e-1a1a1a1a1a1a", name: "Houston", city: "Houston", state: "TX", slug: "houston-tx" },
  { id: "5f0c2c1a-9d61-4b1e-8a3e-2b2b2b2b2b2b", name: "Dallas", city: "Dallas", state: "TX", slug: "dallas-tx" },
  { id: "5f0c2c1a-9d61-4b1e-8a3e-3c3c3c3c3c3c", name: "Austin", city: "Austin", state: "TX", slug: "austin-tx" },
];

export const mockMarketStats: Record<string, MarketStats> = {
  "houston-tx": {
    slug: "houston-tx",
    totalLast30Days: 137,
    byCategory: {
      FIRE_SPRINKLER: 52, FIRE_ALARM: 37, FIRE_SUPPRESSION: 12, KITCHEN_SUPPRESSION: 6,
      FIRE_INSPECTION: 12, VIOLATION_CORRECTION: 8, GENERAL_FIRE_PROTECTION: 10,
    },
    lastUpdatedAt: "2026-08-19T06:00:00Z",
  },
  "dallas-tx": {
    slug: "dallas-tx",
    totalLast30Days: 98,
    byCategory: {
      FIRE_SPRINKLER: 34, FIRE_ALARM: 28, FIRE_SUPPRESSION: 9, KITCHEN_SUPPRESSION: 5,
      FIRE_INSPECTION: 10, VIOLATION_CORRECTION: 5, GENERAL_FIRE_PROTECTION: 7,
    },
    lastUpdatedAt: "2026-08-19T06:00:00Z",
  },
  "austin-tx": {
    slug: "austin-tx",
    totalLast30Days: 74,
    byCategory: {
      FIRE_SPRINKLER: 27, FIRE_ALARM: 19, FIRE_SUPPRESSION: 7, KITCHEN_SUPPRESSION: 4,
      FIRE_INSPECTION: 8, VIOLATION_CORRECTION: 4, GENERAL_FIRE_PROTECTION: 5,
    },
    lastUpdatedAt: "2026-08-19T06:00:00Z",
  },
};
