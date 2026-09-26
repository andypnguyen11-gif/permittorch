import { describe, expect, it } from "vitest";
import {
  fitSelection, isValidSelection, marketLimit, parsePlanTier, toggleMarket,
} from "@/components/app/account/plan-selection";

describe("parsePlanTier", () => {
  it.each([["STARTER", "STARTER"], ["pro", "PRO"], [" Territory ", "TERRITORY"]])("accepts %s", (raw, plan) => {
    expect(parsePlanTier(raw)).toBe(plan);
  });

  it.each([undefined, null, "", "FREE", "ENTERPRISE", ["PRO"], "PRO;drop"])("rejects %s", (raw) => {
    expect(parsePlanTier(raw)).toBeNull();
  });
});

describe("market picker rules", () => {
  it("allows one market on Starter/Pro and five on Territory", () => {
    expect(marketLimit("STARTER")).toBe(1);
    expect(marketLimit("PRO")).toBe(1);
    expect(marketLimit("TERRITORY")).toBe(5);
  });

  it("replaces the single market on Starter/Pro", () => {
    expect(toggleMarket("PRO", [], "austin-tx")).toEqual(["austin-tx"]);
    expect(toggleMarket("STARTER", ["austin-tx"], "dallas-tx")).toEqual(["dallas-tx"]);
  });

  it("toggles markets on Territory and caps the selection at five", () => {
    let sel: string[] = [];
    for (const s of ["a", "b", "c", "d", "e", "f"]) sel = toggleMarket("TERRITORY", sel, s);
    expect(sel).toEqual(["a", "b", "c", "d", "e"]);
    expect(toggleMarket("TERRITORY", sel, "c")).toEqual(["a", "b", "d", "e"]);
  });

  it("trims a Territory selection when switching down to a one-market plan", () => {
    expect(fitSelection("PRO", ["a", "b", "c"])).toEqual(["a"]);
    expect(fitSelection("TERRITORY", ["a", "b"])).toEqual(["a", "b"]);
  });

  it("validates the checkout body the API accepts", () => {
    expect(isValidSelection("PRO", [])).toBe(false);
    expect(isValidSelection("PRO", ["a"])).toBe(true);
    expect(isValidSelection("STARTER", ["a", "b"])).toBe(false);
    expect(isValidSelection("TERRITORY", ["a", "b", "c", "d", "e"])).toBe(true);
    expect(isValidSelection("TERRITORY", ["a", "b", "c", "d", "e", "f"])).toBe(false);
    expect(isValidSelection("TERRITORY", ["a", "a"])).toBe(false);
  });
});
