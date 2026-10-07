import { describe, expect, it } from "vitest";
import { buildLeadsSearch, parseLeadsSearchParams } from "@/components/app/leads/query";

describe("parseLeadsSearchParams", () => {
  it("parses a full set of valid params", () => {
    expect(parseLeadsSearchParams({
      market: "houston-tx", category: "FIRE_ALARM", minScore: "90",
      maxAgeDays: "7", status: "NEW", q: "warehouse", page: "2",
    })).toEqual({
      market: "houston-tx", category: "FIRE_ALARM", minScore: 90,
      maxAgeDays: 7, status: "NEW", q: "warehouse", page: 2,
    });
  });

  it("drops invalid enum values and non-numeric numbers", () => {
    expect(parseLeadsSearchParams({
      category: "NOT_A_CATEGORY", status: "BOGUS", minScore: "abc", page: "0",
    })).toEqual({});
  });

  it("takes the first value of repeated params and ignores empties", () => {
    expect(parseLeadsSearchParams({ q: ["fire", "x"], market: "" })).toEqual({ q: "fire" });
  });
});

describe("buildLeadsSearch", () => {
  it("round-trips a query into a canonical search string", () => {
    expect(buildLeadsSearch({ category: "FIRE_SPRINKLER", minScore: 90 }))
      .toBe("?category=FIRE_SPRINKLER&minScore=90");
    expect(buildLeadsSearch({})).toBe("");
    expect(buildLeadsSearch({ page: 1 })).toBe("");
    expect(buildLeadsSearch({ q: "smoke & fire" })).toBe("?q=smoke+%26+fire");
    expect(buildLeadsSearch({ page: 3, status: "FAILED" })).toBe("?status=FAILED&page=3");
  });
});

describe("parseLeadsSearchParams bounds", () => {
  it("drops out-of-range scores and ages and trims overlong search text", () => {
    expect(parseLeadsSearchParams({ minScore: "101", maxAgeDays: "5000" })).toEqual({});
    expect(parseLeadsSearchParams({ q: "x".repeat(300) }).q).toHaveLength(200);
    expect(parseLeadsSearchParams({ q: "   " })).toEqual({});
  });
});

describe("excludeContractorStatus", () => {
  it("parses a known contractor status and drops an unknown one", () => {
    expect(parseLeadsSearchParams({ excludeContractorStatus: "FIRE_CONTRACTOR_NAMED" }))
      .toEqual({ excludeContractorStatus: "FIRE_CONTRACTOR_NAMED" });
    expect(parseLeadsSearchParams({ excludeContractorStatus: "AWARDED" })).toEqual({});
    expect(parseLeadsSearchParams({ excludeContractorStatus: "" })).toEqual({});
  });

  it("round-trips through the URL alongside the other filters", () => {
    const search = buildLeadsSearch({ category: "FIRE_ALARM", excludeContractorStatus: "FIRE_CONTRACTOR_NAMED", page: 2 });
    expect(search).toBe("?category=FIRE_ALARM&excludeContractorStatus=FIRE_CONTRACTOR_NAMED&page=2");
    expect(parseLeadsSearchParams(Object.fromEntries(new URLSearchParams(search))))
      .toEqual({ category: "FIRE_ALARM", excludeContractorStatus: "FIRE_CONTRACTOR_NAMED", page: 2 });
  });
});
