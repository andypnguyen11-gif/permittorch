import type { LeadOutcome } from "@permittorch/types";

/** The user's own words about a lead. The app's own copy still says only what the record shows. */
export const OUTCOME_LABELS: Record<LeadOutcome, string> = {
  WORTH_CONTACTING: "Worth contacting",
  ALREADY_AWARDED: "Already awarded",
  WRONG_TRADE: "Wrong trade",
  TOO_LATE: "Too late",
};

export const OUTCOMES = Object.keys(OUTCOME_LABELS) as LeadOutcome[];
