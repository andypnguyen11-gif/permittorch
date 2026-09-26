// @vitest-environment jsdom
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import type { MarketStats } from "@permittorch/types";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

vi.mock("@/lib/api", () => ({
  getMarkets: vi.fn(async () => mockMarkets),
  getMarketStats: vi.fn(async (slug: string) => mockMarketStats[slug]),
}));
vi.mock("next/navigation", () => ({ notFound: () => { throw new Error("NEXT_NOT_FOUND"); } }));

import { REGISTRY_MARKETS, REGISTRY_SOURCES, getMarketSources } from "@/lib/marketing/source-registry";
import { marketNarrative } from "@/lib/marketing/market-narrative";
import { MarketSources } from "@/components/marketing/market-sources";
import { EXAMPLE_LEADS } from "@/components/marketing/market-example-leads";
import MarketPage from "@/app/(marketing)/locations/[state]/[city]/page";

afterEach(() => cleanup());

const REGISTRY_JSON = join(import.meta.dirname, "../../../../docs/superpowers/plans/2026-08-19-permittorch-mvp/scraper-source-registry.json");

describe("source registry", () => {
  it("has the 31 scraper-supported markets and 40 sources", () => {
    expect(REGISTRY_MARKETS).toHaveLength(31);
    expect(REGISTRY_SOURCES).toHaveLength(40);
    expect(new Set(REGISTRY_MARKETS.map((m) => m.slug)).size).toBe(31);
    expect(new Set(REGISTRY_SOURCES.map((s) => s.sourceId)).size).toBe(40);
  });

  it("gives every market at least one source and every source a known market", () => {
    const slugs = new Set(REGISTRY_MARKETS.map((m) => m.slug));
    for (const s of REGISTRY_SOURCES) expect(slugs.has(s.marketSlug), s.sourceId).toBe(true);
    for (const m of REGISTRY_MARKETS) expect(getMarketSources(m.slug)!.sources.length, m.slug).toBeGreaterThan(0);
  });

  it("matches the captured registry JSON exactly", () => {
    const json = JSON.parse(readFileSync(REGISTRY_JSON, "utf8"));
    expect(REGISTRY_MARKETS).toEqual(json.markets);
    expect(REGISTRY_SOURCES).toEqual(json.sources);
  });

  it("does not list unsupported metros", () => {
    expect(getMarketSources("houston-tx")).toBeUndefined();
    expect(getMarketSources("dallas-tx")).toBeUndefined();
  });
});

describe("marketNarrative", () => {
  it("reports the top category and its share of the 30-day total", () => {
    expect(marketNarrative("Austin", mockMarketStats["austin-tx"])).toBe(
      "In the last 30 days, 36% of Austin's fire-protection permits were sprinkler work. Fire alarm work was next at 26%.",
    );
  });

  it("follows the stats, not a template", () => {
    const stats: MarketStats = {
      slug: "x", totalLast30Days: 50, lastUpdatedAt: "2026-09-26T00:00:00Z",
      byCategory: {
        FIRE_SPRINKLER: 5, FIRE_ALARM: 30, FIRE_SUPPRESSION: 0, KITCHEN_SUPPRESSION: 15,
        FIRE_INSPECTION: 0, VIOLATION_CORRECTION: 0, GENERAL_FIRE_PROTECTION: 0,
      },
    };
    expect(marketNarrative("Tulsa", stats)).toBe(
      "In the last 30 days, 60% of Tulsa's fire-protection permits were fire alarm work. Kitchen hood suppression work was next at 30%.",
    );
  });

  it("is null when there is nothing to describe", () => {
    expect(marketNarrative("Nowhere", { ...mockMarketStats["austin-tx"], totalLast30Days: 0 })).toBeNull();
  });
});

describe("MarketSources", () => {
  it("lists the market's sources with portal type and jurisdiction", () => {
    render(<MarketSources slug="austin-tx" />);
    expect(screen.getByRole("heading", { name: "Where the data comes from" })).toBeDefined();
    expect(screen.getByText("Austin Issued Construction Permits")).toBeDefined();
    expect(screen.getByText("Socrata open data portal")).toBeDefined();
    expect(screen.getByText(/Austin, Texas/)).toBeDefined();
  });

  it("lists every source for multi-source markets", () => {
    render(<MarketSources slug="sacramento-ca" />);
    expect(screen.getAllByRole("listitem")).toHaveLength(4);
  });

  it("renders no source list for an unknown slug", () => {
    const { container } = render(<MarketSources slug="houston-tx" />);
    expect(container.innerHTML).toBe("");
  });
});

describe("market page content", () => {
  it("renders the narrative, sources, and honest example labelling for Austin", async () => {
    render(await MarketPage({ params: Promise.resolve({ state: "texas", city: "austin" }) }));
    expect(screen.getByText(/36% of Austin's fire-protection permits were sprinkler work/)).toBeDefined();
    expect(screen.getByText("Austin Issued Construction Permits")).toBeDefined();
    expect(screen.getByText("Illustrative examples — not records from Austin")).toBeDefined();
  });

  it("omits the source section for a market outside the registry", async () => {
    render(await MarketPage({ params: Promise.resolve({ state: "texas", city: "houston" }) }));
    expect(screen.queryByRole("heading", { name: "Where the data comes from" })).toBeNull();
    expect(screen.getByText("Illustrative examples — not records from Houston")).toBeDefined();
  });

  it("uses example scores the engine can actually produce", () => {
    expect(EXAMPLE_LEADS.map((l) => l.score)).toEqual([90, 75, 55]);
  });
});
