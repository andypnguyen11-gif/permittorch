import type { PreSprinklerKind, PreSprinklerStanding } from "@permittorch/types";
import type { PreSprinklerQuery } from "@/lib/api";

export const PRE_SPRINKLER_KINDS: { value: PreSprinklerKind; label: string }[] = [
  { value: "NEW_HOME", label: "New home" }, { value: "ADU", label: "ADU" }, { value: "ADDITION", label: "Addition" },
];
export const PRE_SPRINKLER_STANDINGS: { value: PreSprinklerStanding | "ALL"; label: string }[] = [
  { value: "OPEN", label: "Open (no sprinkler permit yet)" },
  { value: "SPRINKLER_FILED", label: "Sprinkler permit filed" },
  { value: "CHECK_UNAVAILABLE", label: "Sprinkler check unavailable" },
  { value: "NO_LONGER_LISTED", label: "No longer listed" },
  { value: "ALL", label: "Every standing" },
];
const DATE = /^\d{4}-\d{2}-\d{2}$/;

export type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => (typeof sp[key] === "string" ? (sp[key] as string) : undefined);

/** Only values the page offers reach the API; anything else in the URL is ignored. */
export function readPreSprinklerQuery(sp: SearchParams, marketSlugs: string[]): PreSprinklerQuery {
  const market = one(sp, "market");
  const kind = one(sp, "kind");
  const standing = one(sp, "standing");
  const filedFrom = one(sp, "filedFrom");
  const filedTo = one(sp, "filedTo");
  return {
    market: market && marketSlugs.includes(market) ? market : undefined,
    kind: PRE_SPRINKLER_KINDS.find((k) => k.value === kind)?.value,
    standing: PRE_SPRINKLER_STANDINGS.find((s) => s.value === standing)?.value ?? "OPEN",
    filedFrom: filedFrom && DATE.test(filedFrom) ? filedFrom : undefined,
    filedTo: filedTo && DATE.test(filedTo) ? filedTo : undefined,
  };
}

