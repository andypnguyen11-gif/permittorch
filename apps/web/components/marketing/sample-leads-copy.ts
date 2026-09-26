// Single source of truth for what the free sample-leads request actually
// delivers (mirrors the API's sample-lead job). Do not promise more than this.
export const SAMPLE_LEADS_PROMISE =
  "up to 5 of the highest-scoring opportunities (score 70+) in your market, usually within the hour, then a weekly update — you can opt out from any email";

export const SAMPLE_LEADS_HEADING = "Get free sample leads from your market";

/** Sentence-cased promise for standalone use: "We'll email you up to 5 …". */
export const SAMPLE_LEADS_SENTENCE = `We'll email you ${SAMPLE_LEADS_PROMISE}.`;
