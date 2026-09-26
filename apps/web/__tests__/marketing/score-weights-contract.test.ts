import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { SCORE_WEIGHTS } from "@/components/marketing/score-example";

// The marketing score explainer hand-mirrors the API's scoring config. The API
// is the source of truth (appsettings.json Scoring:Weights + ScoringEngine.BaseScore);
// this test makes any drift fail CI.
const apiDir = path.resolve(__dirname, "../../../api");
const appsettings = JSON.parse(readFileSync(path.join(apiDir, "appsettings.json"), "utf8")) as {
  Scoring: { Weights: Record<string, number> };
};
const engine = readFileSync(path.join(apiDir, "Domain/Scoring/ScoringEngine.cs"), "utf8");

describe("SCORE_WEIGHTS ↔ API scoring config", () => {
  it("mirrors Scoring:Weights exactly, plus BASE_SCORE", () => {
    const { BASE_SCORE, ...weights } = SCORE_WEIGHTS;
    expect(weights).toEqual(appsettings.Scoring.Weights);
    expect(BASE_SCORE).toBe(30);
  });

  it("matches the engine's BaseScore constant", () => {
    const match = engine.match(/public const int BaseScore = (-?\d+);/);
    expect(match).not.toBeNull();
    expect(SCORE_WEIGHTS.BASE_SCORE).toBe(Number(match![1]));
  });
});
