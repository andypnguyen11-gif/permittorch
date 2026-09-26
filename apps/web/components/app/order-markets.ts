import type { Market } from "@permittorch/types";

/** Entitled markets first, then every other active market (deduped by slug). */
export function orderMarkets(all: Market[], entitled: Market[]): Market[] {
  const slugs = new Set(entitled.map((m) => m.slug));
  return [...entitled, ...all.filter((m) => !slugs.has(m.slug))];
}
