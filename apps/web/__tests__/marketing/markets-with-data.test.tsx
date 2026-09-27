// @vitest-environment jsdom
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import type { Market, MarketStats } from "@permittorch/types";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

// Houston/Dallas/Austin have data; El Paso has zero permits; Tulsa has never
// been updated; Mesa's stats request fails.
const ZERO: Market = { id: "z1", name: "El Paso", city: "El Paso", state: "TX", slug: "el-paso-tx" };
const NEVER: Market = { id: "z2", name: "Tulsa", city: "Tulsa", state: "OK", slug: "tulsa-ok" };
const BROKEN: Market = { id: "z3", name: "Mesa", city: "Mesa", state: "AZ", slug: "mesa-az" };
const empty = (slug: string, lastUpdatedAt: string | null): MarketStats => ({
  slug, totalLast30Days: 0, lastUpdatedAt,
  byCategory: {
    FIRE_SPRINKLER: 0, FIRE_ALARM: 0, FIRE_SUPPRESSION: 0, KITCHEN_SUPPRESSION: 0,
    FIRE_INSPECTION: 0, VIOLATION_CORRECTION: 0, GENERAL_FIRE_PROTECTION: 0,
  },
});
const STATS: Record<string, MarketStats> = {
  ...mockMarketStats,
  "el-paso-tx": empty("el-paso-tx", "2026-08-19T06:00:00Z"),
  "tulsa-ok": { ...mockMarketStats["austin-tx"], slug: "tulsa-ok", lastUpdatedAt: null },
};

vi.mock("@/lib/api", () => ({
  getMarkets: vi.fn(async () => [...mockMarkets, ZERO, NEVER, BROKEN]),
  getMarketStats: vi.fn(async (slug: string) => {
    const s = STATS[slug];
    if (!s) throw new Error(`stats unavailable for ${slug}`);
    return s;
  }),
  // Mesa has no stats entry (its per-market stats request fails too).
  getAllMarketStats: vi.fn(async () => Object.values(STATS)),
}));

vi.mock("next/navigation", () => ({
  notFound: () => { throw new Error("NEXT_NOT_FOUND"); },
  usePathname: () => "/",
}));

import * as api from "@/lib/api";
import { getMarketsWithData, hasRealData } from "@/lib/marketing/markets-with-data";
import sitemap from "@/app/sitemap";
import LocationsPage from "@/app/(marketing)/locations/page";
import MarketPage, { generateMetadata, generateStaticParams } from "@/app/(marketing)/locations/[state]/[city]/page";

afterEach(() => cleanup());

const DATA_SLUGS = ["houston-tx", "dallas-tx", "austin-tx"];

describe("getMarketsWithData", () => {
  it("keeps only markets with an update time and recent permits, settling failures", async () => {
    const entries = await getMarketsWithData();
    expect(entries.map((e) => e.market.slug)).toEqual(DATA_SLUGS);
    expect(entries[0].stats).toBe(STATS["houston-tx"]);
  });

  it("makes two API calls in total, never one per market", async () => {
    vi.mocked(api.getMarkets).mockClear();
    vi.mocked(api.getMarketStats).mockClear();
    vi.mocked(api.getAllMarketStats).mockClear();
    await getMarketsWithData();
    expect(api.getMarkets).toHaveBeenCalledTimes(1);
    expect(api.getAllMarketStats).toHaveBeenCalledTimes(1);
    expect(api.getMarketStats).not.toHaveBeenCalled();
  });

  it("hasRealData requires both a timestamp and at least one permit", () => {
    expect(hasRealData(STATS["austin-tx"])).toBe(true);
    expect(hasRealData(STATS["el-paso-tx"])).toBe(false);
    expect(hasRealData(STATS["tulsa-ok"])).toBe(false);
  });
});

describe("zero-data markets get no public surface", () => {
  beforeAll(() => { vi.spyOn(console, "error").mockImplementation(() => {}); });

  it("are excluded from generateStaticParams", async () => {
    const params = await generateStaticParams();
    expect(params).toEqual([
      { state: "texas", city: "houston" }, { state: "texas", city: "dallas" }, { state: "texas", city: "austin" },
    ]);
  });

  it("are excluded from the sitemap", async () => {
    const urls = (await sitemap()).map((e) => e.url);
    expect(urls).toContain("https://permittorch.com/locations/texas/austin");
    expect(urls.some((u) => /el-paso|tulsa|mesa/.test(u))).toBe(false);
  });

  it("are excluded from the /locations index", async () => {
    render(await LocationsPage());
    expect(screen.getByRole("heading", { name: "Austin, TX" })).toBeDefined();
    expect(screen.queryByText(/El Paso/)).toBeNull();
    expect(screen.queryByText(/Tulsa/)).toBeNull();
    expect(screen.queryByText(/Mesa/)).toBeNull();
  });

  it.each([
    ["texas", "el-paso"], ["oklahoma", "tulsa"], ["texas", "nowhere"],
  ])("/locations/%s/%s returns notFound()", async (state, city) => {
    await expect(MarketPage({ params: Promise.resolve({ state, city }) })).rejects.toThrow("NEXT_NOT_FOUND");
    expect(await generateMetadata({ params: Promise.resolve({ state, city }) })).toEqual({});
  });

  it("an unknown slug 404s after one getMarkets call and no stats calls", async () => {
    vi.mocked(api.getMarkets).mockClear();
    vi.mocked(api.getMarketStats).mockClear();
    await expect(MarketPage({ params: Promise.resolve({ state: "texas", city: "nowhere" }) }))
      .rejects.toThrow("NEXT_NOT_FOUND");
    expect(api.getMarkets).toHaveBeenCalledTimes(1);
    expect(api.getMarketStats).not.toHaveBeenCalled();
  });

  it("a real market whose stats request fails throws instead of 404ing", async () => {
    const params = { state: "arizona", city: "mesa" };
    await expect(MarketPage({ params: Promise.resolve(params) })).rejects.toThrow("stats unavailable for mesa-az");
    await expect(generateMetadata({ params: Promise.resolve(params) })).rejects.toThrow("stats unavailable");
  });

  it("fetches stats only for the requested market", async () => {
    vi.mocked(api.getMarketStats).mockClear();
    await MarketPage({ params: Promise.resolve({ state: "texas", city: "austin" }) });
    expect(api.getMarketStats).toHaveBeenCalledTimes(1);
    expect(api.getMarketStats).toHaveBeenCalledWith("austin-tx");
  });

  it("a market with data still renders", async () => {
    const el = await MarketPage({ params: Promise.resolve({ state: "texas", city: "austin" }) });
    expect(el).toBeTruthy();
  });
});
