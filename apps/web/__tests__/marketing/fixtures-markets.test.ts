import { describe, expect, it } from "vitest";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

const ALL_CATEGORIES = [
  "FIRE_SPRINKLER", "FIRE_ALARM", "FIRE_SUPPRESSION", "KITCHEN_SUPPRESSION",
  "FIRE_INSPECTION", "VIOLATION_CORRECTION", "GENERAL_FIRE_PROTECTION",
] as const;

describe("market fixtures", () => {
  it("exports exactly the three MVP markets", () => {
    expect(mockMarkets.map((m) => m.slug)).toEqual(["houston-tx", "dallas-tx", "austin-tx"]);
  });

  it("every market has id, name, city, state", () => {
    for (const m of mockMarkets) {
      expect(m.id).toBeTruthy();
      expect(m.name).toBeTruthy();
      expect(m.city).toBeTruthy();
      expect(m.state).toBe("TX");
    }
  });

  it("has stats for every market slug, keyed by slug", () => {
    for (const m of mockMarkets) {
      const stats = mockMarketStats[m.slug];
      expect(stats).toBeDefined();
      expect(stats.slug).toBe(m.slug);
    }
  });

  it("stats cover all seven fire categories and sum to totalLast30Days", () => {
    for (const stats of Object.values(mockMarketStats)) {
      for (const c of ALL_CATEGORIES) expect(typeof stats.byCategory[c]).toBe("number");
      const sum = Object.values(stats.byCategory).reduce((a, b) => a + b, 0);
      expect(sum).toBe(stats.totalLast30Days);
      expect(stats.lastUpdatedAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    }
  });
});
