// Illustrative score breakdown for /how-it-works (PRD §16). It mirrors the real
// scoring engine: every classified fire-protection permit starts from the
// persisted BASE_SCORE signal (30), then adds configured signal weights and is
// clamped to 0–100. Keep these weights in sync with the API's scoring config.

export const SCORE_WEIGHTS = {
  BASE_SCORE: 30,
  NEW_COMMERCIAL_BUILD: 25,
  FIRE_SPRINKLER_SCOPE: 25,
  PERMIT_RECENT: 15,
  HIGH_PROJECT_VALUE: 10,
  NO_CONTRACTOR_LISTED: 10,
} as const;

export type ExampleSignalType = keyof typeof SCORE_WEIGHTS;

export interface ScoreExampleSignal { type: ExampleSignalType; label: string; points: number }

const signal = (type: ExampleSignalType, label: string): ScoreExampleSignal =>
  ({ type, label, points: SCORE_WEIGHTS[type] });

const SIGNALS: ScoreExampleSignal[] = [
  signal("BASE_SCORE", "Baseline for a classified fire-protection permit"),
  signal("NEW_COMMERCIAL_BUILD", "New commercial construction"),
  signal("FIRE_SPRINKLER_SCOPE", "Sprinkler scope detected in the permit"),
  signal("PERMIT_RECENT", "Filed within the last few days"),
];

export const SCORE_EXAMPLE = {
  total: Math.min(100, Math.max(0, SIGNALS.reduce((sum, s) => sum + s.points, 0))),
  headline: "New commercial build — sprinkler scope, filed this week",
  signals: SIGNALS,
};
