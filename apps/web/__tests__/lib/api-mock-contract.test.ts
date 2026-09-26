import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import * as fixtures from "@/lib/fixtures";
import * as markets from "@/lib/fixtures/markets";

// lib/api.ts reaches the fixtures by name in mock mode; a renamed or missing
// export would only fail at runtime in the mock build. Parse the client and
// make sure every name it calls resolves to a function (or array/record).
const source = readFileSync(path.resolve(__dirname, "../../lib/api.ts"), "utf8");

describe("lib/api.ts mock branch ↔ lib/fixtures exports", () => {
  const indexCalls = [...source.matchAll(/\(await fixtures\(\)\)\.(\w+)\(/g)].map((m) => m[1]);
  const marketImports = [...source.matchAll(/import\("@\/lib\/fixtures\/markets"\)\)\.(\w+)/g)].map((m) => m[1]);

  it("finds the mock branches it is checking", () => {
    expect(indexCalls.length).toBeGreaterThanOrEqual(15);
    expect(marketImports).toEqual(expect.arrayContaining(["mockMarkets", "mockMarketStats"]));
  });

  it.each(indexCalls)("fixtures index exports %s as a function", (name) => {
    expect(typeof (fixtures as Record<string, unknown>)[name]).toBe("function");
  });

  it.each(marketImports)("fixtures/markets exports %s", (name) => {
    expect((markets as Record<string, unknown>)[name]).toBeDefined();
  });

  it("every exported api function (except markets) has a mock branch", () => {
    const exported = [...source.matchAll(/export async function (\w+)\(/g)].map((m) => m[1])
      .filter((n) => n !== "apiFetch" && n !== "getMarkets" && n !== "getMarketStats");
    expect(new Set(indexCalls)).toEqual(new Set(exported));
  });
});
