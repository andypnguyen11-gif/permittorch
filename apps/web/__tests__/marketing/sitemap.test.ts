import { describe, expect, it, vi } from "vitest";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

vi.mock("@/lib/api", () => ({
  getMarkets: vi.fn().mockResolvedValue(mockMarkets),
  getMarketStats: vi.fn(async (slug: string) => mockMarketStats[slug]),
  getAllMarketStats: vi.fn(async () => Object.values(mockMarketStats)),
}));

import sitemap from "@/app/sitemap";
import robots from "@/app/robots";

describe("sitemap", () => {
  it("includes all static marketing routes, market pages, and blog posts", async () => {
    const urls = (await sitemap()).map((e) => e.url);
    for (const path of [
      "/", "/pricing", "/how-it-works", "/fire-protection-leads", "/fire-sprinkler-leads",
      "/fire-alarm-leads", "/locations", "/blog", "/terms", "/privacy",
      "/locations/texas/houston", "/locations/texas/dallas", "/locations/texas/austin",
      "/blog/how-fire-sprinkler-contractors-find-leads",
      "/blog/using-building-permits-for-lead-generation",
      "/blog/how-to-find-commercial-fire-protection-projects",
    ]) {
      expect(urls).toContain(new URL(path, "https://permittorch.com").toString());
    }
  });

  it("contains no /app routes", async () => {
    const urls = (await sitemap()).map((e) => e.url);
    expect(urls.some((u) => u.includes("/app"))).toBe(false);
  });
});

describe("robots", () => {
  it("allows everything except the app, and references the sitemap", () => {
    const r = robots();
    const rules = Array.isArray(r.rules) ? r.rules[0] : r.rules;
    expect(rules?.allow).toBe("/");
    expect(rules?.disallow).toContain("/app/");
    expect(r.sitemap).toBe("https://permittorch.com/sitemap.xml");
  });
});
