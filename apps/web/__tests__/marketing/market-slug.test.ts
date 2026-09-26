import { describe, expect, it } from "vitest";
import type { Market } from "@permittorch/types";
import {
  findMarketByLocationParams, marketLocationPath, marketToLocationParams, stateDisplayName,
} from "@/components/marketing/market-slug";
import { mockMarkets } from "@/lib/fixtures/markets";

const houston = mockMarkets[0];
const sanAntonio: Market = { id: "x", name: "San Antonio", city: "San Antonio", state: "TX", slug: "san-antonio-tx" };

describe("marketToLocationParams", () => {
  it("maps houston-tx to texas/houston", () => {
    expect(marketToLocationParams(houston)).toEqual({ state: "texas", city: "houston" });
  });
  it("hyphenates multi-word cities", () => {
    expect(marketToLocationParams(sanAntonio)).toEqual({ state: "texas", city: "san-antonio" });
  });
  it("falls back to the raw abbreviation for unknown states", () => {
    expect(marketToLocationParams({ ...houston, state: "ZZ" }).state).toBe("zz");
  });
});

describe("marketLocationPath", () => {
  it("builds the full route path", () => {
    expect(marketLocationPath(houston)).toBe("/locations/texas/houston");
  });
});

describe("stateDisplayName", () => {
  it("expands abbreviations", () => {
    expect(stateDisplayName("TX")).toBe("Texas");
    expect(stateDisplayName("tx")).toBe("Texas");
  });
  it("returns the input for unknown abbreviations", () => {
    expect(stateDisplayName("ZZ")).toBe("ZZ");
  });
});

describe("findMarketByLocationParams", () => {
  it("resolves a market from URL params, case-insensitively", () => {
    expect(findMarketByLocationParams(mockMarkets, "texas", "houston")?.slug).toBe("houston-tx");
    expect(findMarketByLocationParams(mockMarkets, "Texas", "HOUSTON")?.slug).toBe("houston-tx");
  });
  it("returns undefined for unknown params — pages must notFound()", () => {
    expect(findMarketByLocationParams(mockMarkets, "texas", "el-paso")).toBeUndefined();
    expect(findMarketByLocationParams(mockMarkets, "oklahoma", "houston")).toBeUndefined();
  });
});
