import type { Market } from "@permittorch/types";

const STATE_NAMES: Record<string, string> = {
  AL: "Alabama", AK: "Alaska", AZ: "Arizona", AR: "Arkansas", CA: "California",
  CO: "Colorado", CT: "Connecticut", DE: "Delaware", DC: "District of Columbia",
  FL: "Florida", GA: "Georgia", HI: "Hawaii", ID: "Idaho", IL: "Illinois",
  IN: "Indiana", IA: "Iowa", KS: "Kansas", KY: "Kentucky", LA: "Louisiana",
  ME: "Maine", MD: "Maryland", MA: "Massachusetts", MI: "Michigan", MN: "Minnesota",
  MS: "Mississippi", MO: "Missouri", MT: "Montana", NE: "Nebraska", NV: "Nevada",
  NH: "New Hampshire", NJ: "New Jersey", NM: "New Mexico", NY: "New York",
  NC: "North Carolina", ND: "North Dakota", OH: "Ohio", OK: "Oklahoma", OR: "Oregon",
  PA: "Pennsylvania", RI: "Rhode Island", SC: "South Carolina", SD: "South Dakota",
  TN: "Tennessee", TX: "Texas", UT: "Utah", VT: "Vermont", VA: "Virginia",
  WA: "Washington", WV: "West Virginia", WI: "Wisconsin", WY: "Wyoming",
};

const slugify = (s: string) => s.trim().toLowerCase().replace(/\s+/g, "-");

/** "TX" -> "Texas"; unknown abbreviations are returned unchanged. */
export function stateDisplayName(abbrev: string): string {
  return STATE_NAMES[abbrev.toUpperCase()] ?? abbrev;
}

/** URL segments for /locations/[state]/[city], e.g. houston-tx -> { state: "texas", city: "houston" } */
export function marketToLocationParams(market: Market): { state: string; city: string } {
  const full = STATE_NAMES[market.state.toUpperCase()];
  return { state: slugify(full ?? market.state), city: slugify(market.city) };
}

export function marketLocationPath(market: Market): string {
  const { state, city } = marketToLocationParams(market);
  return `/locations/${state}/${city}`;
}

/** Resolve URL params back to a real market. Undefined means the page must notFound(). */
export function findMarketByLocationParams(
  markets: Market[], state: string, city: string,
): Market | undefined {
  const s = state.toLowerCase();
  const c = city.toLowerCase();
  return markets.find((m) => {
    const p = marketToLocationParams(m);
    return p.state === s && p.city === c;
  });
}
