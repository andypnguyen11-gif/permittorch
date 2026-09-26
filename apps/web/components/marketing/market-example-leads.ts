import { scoreFor } from "@/components/marketing/score-example";

// Static, anonymized illustrations for market pages — labelled on the page as
// not records from the city. Scores are computed from engine weights so every
// example is a score the real engine could produce.
export const EXAMPLE_LEADS = [
  {
    score: scoreFor(["NEW_COMMERCIAL_BUILD", "FIRE_SPRINKLER_SCOPE", "NO_CONTRACTOR_LISTED"]),
    title: "Fire sprinkler system — new commercial build",
    detail: "New commercial construction with sprinkler scope in the permit description and no fire contractor listed.",
    meta: "Address available to subscribers",
  },
  {
    score: scoreFor(["FIRE_ALARM_SCOPE", "PERMIT_RECENT", "HIGH_PROJECT_VALUE"]),
    title: "Fire alarm system — healthcare tenant upfit",
    detail: "Tenant improvement with alarm scope in the permit description, filed in the last 72 hours, valued over $500K.",
    meta: "Address available to subscribers",
  },
  {
    score: scoreFor(["PERMIT_RECENT", "NO_CONTRACTOR_LISTED"]),
    title: "Kitchen suppression — new restaurant",
    detail: "Restaurant build-out needing a hood suppression system, filed in the last 72 hours, no fire contractor listed.",
    meta: "Address available to subscribers",
  },
];
