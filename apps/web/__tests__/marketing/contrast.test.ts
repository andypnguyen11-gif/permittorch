import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";
import { BADGE_CLASSES, ORANGE_TEXT, ctaClasses } from "@/components/marketing/cta";

const ROOT = join(import.meta.dirname, "..", "..");
const DIRS = ["app/(marketing)", "components/marketing"];

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) return sources(p);
    return /\.(ts|tsx)$/.test(name) && !p.endsWith("cta.ts") ? [p] : [];
  });
}

const FILES = DIRS.flatMap((d) => sources(join(ROOT, d)));

// Low-contrast patterns banned from marketing sources (use cta.ts helpers instead).
const BANNED: [RegExp, string][] = [
  [/(?<![\w:-])text-orange-(500|600)\b/, "orange-500/600 text fails AA under 24px — use text-orange-700"],
  [/\bhover:text-orange-(500|600)\b/, "orange hover text must be orange-700 or darker"],
  [/\btext-neutral-(300|400)\b/, "fine print must be at least text-neutral-500"],
  [/\bbg-orange-(500|600)\b[^"'`]*\btext-white\b|\btext-white\b[^"'`]*\bbg-orange-(500|600)\b/, "white on orange-500/600 fails AA — use bg-orange-700"],
  [/\bhover:bg-orange-600\b/, "hard-coded CTA colours — use ctaClasses()"],
];

describe("marketing colour contrast rules", () => {
  it("finds the marketing sources", () => {
    expect(FILES.length).toBeGreaterThan(20);
  });

  it.each(BANNED)("no source matches %s", (re, why) => {
    const hits = FILES.flatMap((f) =>
      readFileSync(f, "utf8").split("\n")
        .map((line, i) => ({ line, i }))
        .filter(({ line }) => re.test(line))
        .map(({ i, line }) => `${relative(ROOT, f)}:${i + 1}: ${line.trim()}`),
    );
    expect(hits, why).toEqual([]);
  });

  it("primary CTAs use AA-compliant orange-700 with a visible focus ring", () => {
    const c = ctaClasses();
    for (const cls of ["bg-orange-700", "text-white", "hover:bg-orange-800", "focus-visible:ring-2",
      "focus-visible:ring-orange-700", "focus-visible:ring-offset-2"]) {
      expect(c.split(" ")).toContain(cls);
    }
    expect(ctaClasses("hero").split(" ")).toEqual(expect.arrayContaining(["h-11", "px-6", "text-base", "font-semibold"]));
    expect(BADGE_CLASSES).toBe("bg-orange-700 text-white");
    expect(ORANGE_TEXT).toBe("text-orange-700");
  });
});
