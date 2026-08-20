# PermitTorch WS3 — Marketing Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Build the complete public marketing surface of PermitTorch — homepage, pricing, how-it-works, three category landers, data-driven location pages, blog, terms/privacy, sitemap/robots, and the sample-leads lead magnet — all server-rendered with unique SEO metadata, running against the mock API (`NEXT_PUBLIC_API_MOCK=1`).

**Architecture:** All routes live in the `apps/web/app/(marketing)/` route group inside the WS0-scaffolded Next.js 15 App Router app. Pages are React Server Components that fetch via `lib/api.ts` (`getMarkets()`, `getMarketStats(slug)`) at build/request time; the only client components are the nav (mobile menu) and `SampleLeadsForm`. Shared marketing UI lives in `apps/web/components/marketing/`. SEO plumbing (`buildMetadata`, `jsonLd`) lives in `apps/web/lib/seo.ts`; `app/sitemap.ts` and `app/robots.ts` are top-level per Next conventions. Location pages are statically generated **only** from markets returned by `getMarkets()` (PRD §24 — no thin programmatic pages). Blog posts are typed TS objects (no CMS, no MDX pipeline).

**Tech Stack:** Next.js 15 App Router · TypeScript strict · Tailwind CSS · shadcn/ui (Button, Card, Accordion, Input — installed by WS0) · Vitest + @testing-library/react · `@permittorch/types` for all shared types.

**Spec:**
- `docs/superpowers/plans/2026-08-19-permittorch-mvp/00-overview-and-contracts.md` (LOCKED contracts — §2 constraints, §7 types, §8 API client)
- `Prd.md` §21–26 (pages, copy, pricing), §50 (SEO requirements), §59 (legal disclaimer copy), §69 (lead magnet), §16 (score explanation example), §24 (no thin SEO pages)
- `Architecture.md` §4.1 (web app surfaces)
- `CLAUDE.md` (git + testing rules)
- `UI Mockup.png` (style reference only: light theme, orange accents, clean SaaS look — data illustrative)

## Global Constraints

Copied from master plan §2, plus WS3-specific rules:

- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Test framework: Vitest + @testing-library/react. (No Playwright here — E2E is WS5.)
- Node 22 LTS, pnpm workspaces.
- Commit messages: imperative, descriptive, **no PR/task references, no Claude co-author trailers** (CLAUDE.md).
- No Redis, no Elasticsearch, no microservices, no message queues.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family / Tailwind `orange-500`) brand accents, generous whitespace. Mockup data is illustrative only.
- SEO pages only for markets with real data — `generateStaticParams` from `getMarkets()` exclusively, `notFound()` for anything else (PRD §24, CLAUDE.md guardrails).
- Data freshness is a feature: show "Updated N ago" honestly; never present stale data as current.
- **Worktree/branch:** work in `../pt-marketing` on branch `ws/marketing` (branched from `main` after WS0 merged).
- **File ownership (hard rule):** create/modify ONLY under `apps/web/app/(marketing)/`, `apps/web/components/marketing/`, `apps/web/lib/seo.ts`, `apps/web/app/sitemap.ts`, `apps/web/app/robots.ts`, `apps/web/lib/fixtures/markets.ts` (that single file — WS4 owns the rest of `lib/fixtures/`), `apps/web/__tests__/marketing/`. NEVER touch `app/app/`, `middleware.ts`, any `package.json`, `packages/types/`, `lib/api.ts`, root `app/layout.tsx`, or vitest/tailwind config.
- **Dependency rule:** WS0 installed all npm deps. If a shadcn/ui primitive turns out to be missing, do NOT run `shadcn add` (it edits `package.json` deps) — build a minimal local equivalent inside `components/marketing/` instead and note it in the final report.
- **Mock mode:** run dev/build/tests with `NEXT_PUBLIC_API_MOCK=1`. `getMarkets()`/`getMarketStats()` return fixtures; `submitSampleLeadRequest()` resolves without a network call.
- **Test scope:** run ONLY this workstream's tests: `pnpm vitest run __tests__/marketing` (from `apps/web/`). Full cross-suite verification happens in WS5.
- Typecheck gate before every commit: `pnpm tsc --noEmit` (from `apps/web/`).
- Import aliases: `@/` → `apps/web/` (WS0 default) and `@permittorch/types` for shared types.

---

### Task 1: Market fixtures (`lib/fixtures/markets.ts`)

The one file WS3 creates in WS4's `fixtures/` directory (coordination agreement: WS4 owns everything else in `lib/fixtures/` and will import these exports). Keep it minimal — exactly two exports, exactly matching §7 types.

**Files:**
- Create: `apps/web/lib/fixtures/markets.ts`
- Create: `apps/web/__tests__/marketing/fixtures-markets.test.ts`

**Interfaces:**
- Consumes: `Market`, `MarketStats`, `FireCategory` from `@permittorch/types`.
- Produces: `mockMarkets: Market[]` (houston-tx, dallas-tx, austin-tx) and `mockMarketStats: Record<string, MarketStats>` — consumed by WS0's `lib/api.ts` mock branch, by WS4's fixture set, and by this plan's tests/pages.

**Steps:**

- [ ] Write the failing test `apps/web/__tests__/marketing/fixtures-markets.test.ts`:

```typescript
import { describe, expect, it } from "vitest";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

const ALL_CATEGORIES = [
  "FIRE_SPRINKLER", "FIRE_ALARM", "FIRE_SUPPRESSION", "KITCHEN_SUPPRESSION",
  "FIRE_INSPECTION", "VIOLATION_CORRECTION", "GENERAL_FIRE_PROTECTION",
] as const;

describe("market fixtures", () => {
  it("exports exactly the three MVP markets", () => {
    expect(mockMarkets.map((m) => m.slug)).toEqual(["houston-tx", "dallas-tx", "austin-tx"]);
  });

  it("every market has id, name, city, state", () => {
    for (const m of mockMarkets) {
      expect(m.id).toBeTruthy();
      expect(m.name).toBeTruthy();
      expect(m.city).toBeTruthy();
      expect(m.state).toBe("TX");
    }
  });

  it("has stats for every market slug, keyed by slug", () => {
    for (const m of mockMarkets) {
      const stats = mockMarketStats[m.slug];
      expect(stats).toBeDefined();
      expect(stats.slug).toBe(m.slug);
    }
  });

  it("stats cover all seven fire categories and sum to totalLast30Days", () => {
    for (const stats of Object.values(mockMarketStats)) {
      for (const c of ALL_CATEGORIES) expect(typeof stats.byCategory[c]).toBe("number");
      const sum = Object.values(stats.byCategory).reduce((a, b) => a + b, 0);
      expect(sum).toBe(stats.totalLast30Days);
      expect(stats.lastUpdatedAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    }
  });
});
```

- [ ] Run `pnpm vitest run __tests__/marketing` — confirm it fails (module not found).
- [ ] Create `apps/web/lib/fixtures/markets.ts`:

```typescript
import type { Market, MarketStats } from "@permittorch/types";

export const mockMarkets: Market[] = [
  { id: "5f0c2c1a-9d61-4b1e-8a3e-1a1a1a1a1a1a", name: "Houston", city: "Houston", state: "TX", slug: "houston-tx" },
  { id: "5f0c2c1a-9d61-4b1e-8a3e-2b2b2b2b2b2b", name: "Dallas", city: "Dallas", state: "TX", slug: "dallas-tx" },
  { id: "5f0c2c1a-9d61-4b1e-8a3e-3c3c3c3c3c3c", name: "Austin", city: "Austin", state: "TX", slug: "austin-tx" },
];

export const mockMarketStats: Record<string, MarketStats> = {
  "houston-tx": {
    slug: "houston-tx",
    totalLast30Days: 137,
    byCategory: {
      FIRE_SPRINKLER: 52, FIRE_ALARM: 37, FIRE_SUPPRESSION: 12, KITCHEN_SUPPRESSION: 6,
      FIRE_INSPECTION: 12, VIOLATION_CORRECTION: 8, GENERAL_FIRE_PROTECTION: 10,
    },
    lastUpdatedAt: "2026-08-19T06:00:00Z",
  },
  "dallas-tx": {
    slug: "dallas-tx",
    totalLast30Days: 98,
    byCategory: {
      FIRE_SPRINKLER: 34, FIRE_ALARM: 28, FIRE_SUPPRESSION: 9, KITCHEN_SUPPRESSION: 5,
      FIRE_INSPECTION: 10, VIOLATION_CORRECTION: 5, GENERAL_FIRE_PROTECTION: 7,
    },
    lastUpdatedAt: "2026-08-19T06:00:00Z",
  },
  "austin-tx": {
    slug: "austin-tx",
    totalLast30Days: 74,
    byCategory: {
      FIRE_SPRINKLER: 27, FIRE_ALARM: 19, FIRE_SUPPRESSION: 7, KITCHEN_SUPPRESSION: 4,
      FIRE_INSPECTION: 8, VIOLATION_CORRECTION: 4, GENERAL_FIRE_PROTECTION: 5,
    },
    lastUpdatedAt: "2026-08-19T06:00:00Z",
  },
};
```

- [ ] Run `pnpm vitest run __tests__/marketing` — green.
- [ ] Sanity check the mock wiring: `grep -n "fixtures" apps/web/lib/api.ts` (READ ONLY — never edit it). Confirm the mock branch of `getMarkets`/`getMarketStats` resolves from `lib/fixtures/markets`. If WS0's api.ts expects different export names, STOP and flag the contract mismatch instead of renaming WS0 code.
- [ ] `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add market fixtures for marketing pages and mock API`

---

### Task 2: SEO helpers (`lib/seo.ts`) — TDD

**Files:**
- Create: `apps/web/lib/seo.ts`
- Create: `apps/web/__tests__/marketing/seo.test.ts`

**Interfaces:**
- Consumes: `Metadata` type from `next`.
- Produces (every marketing page + sitemap/robots consume these):
  - `SITE_URL = "https://permittorch.com"`, `SITE_NAME = "PermitTorch"`
  - `buildMetadata(input: { title: string; description: string; path: string; ogImage?: string }): Metadata`
  - `jsonLd(obj: object): { __html: string }` — for `<script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(...)} />`

**Steps:**

- [ ] Write the failing test `apps/web/__tests__/marketing/seo.test.ts`:

```typescript
import { describe, expect, it } from "vitest";
import { buildMetadata, jsonLd, SITE_URL } from "@/lib/seo";

describe("buildMetadata", () => {
  const base = { title: "Pricing — PermitTorch", description: "Plans for fire protection contractors.", path: "/pricing" };

  it("passes through title and description", () => {
    const md = buildMetadata(base);
    expect(md.title).toBe(base.title);
    expect(md.description).toBe(base.description);
  });

  it("builds canonical URL from the site base", () => {
    expect(buildMetadata(base).alternates?.canonical).toBe("https://permittorch.com/pricing");
    expect(buildMetadata({ ...base, path: "/" }).alternates?.canonical).toBe("https://permittorch.com/");
  });

  it("builds OpenGraph with siteName, url, and type website", () => {
    const og = buildMetadata(base).openGraph as Record<string, unknown>;
    expect(og.title).toBe(base.title);
    expect(og.description).toBe(base.description);
    expect(og.siteName).toBe("PermitTorch");
    expect(og.url).toBe("https://permittorch.com/pricing");
    expect(og.type).toBe("website");
  });

  it("builds a summary_large_image Twitter card", () => {
    const tw = buildMetadata(base).twitter as Record<string, unknown>;
    expect(tw.card).toBe("summary_large_image");
    expect(tw.title).toBe(base.title);
  });

  it("includes og/twitter images only when ogImage is provided", () => {
    const withImg = buildMetadata({ ...base, ogImage: `${SITE_URL}/og/pricing.png` });
    expect((withImg.openGraph as { images?: unknown[] }).images).toEqual([{ url: `${SITE_URL}/og/pricing.png` }]);
    expect((withImg.twitter as { images?: string[] }).images).toEqual([`${SITE_URL}/og/pricing.png`]);
    const without = buildMetadata(base);
    expect((without.openGraph as { images?: unknown }).images).toBeUndefined();
  });
});

describe("jsonLd", () => {
  it("serializes to JSON for dangerouslySetInnerHTML", () => {
    expect(jsonLd({ "@type": "Article", headline: "Hi" }).__html).toBe('{"@type":"Article","headline":"Hi"}');
  });

  it("escapes < to prevent script-tag breakout", () => {
    expect(jsonLd({ x: "</script><script>alert(1)" }).__html).not.toContain("</script>");
    expect(jsonLd({ x: "</script>" }).__html).toContain("\\u003c/script>");
  });
});
```

- [ ] Run `pnpm vitest run __tests__/marketing` — new file fails.
- [ ] Implement `apps/web/lib/seo.ts`:

```typescript
import type { Metadata } from "next";

export const SITE_URL = "https://permittorch.com";
export const SITE_NAME = "PermitTorch";

export interface BuildMetadataInput {
  title: string;
  description: string;
  path: string;       // must start with "/"
  ogImage?: string;   // absolute URL
}

export function buildMetadata({ title, description, path, ogImage }: BuildMetadataInput): Metadata {
  const url = new URL(path, SITE_URL).toString();
  return {
    title,
    description,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      siteName: SITE_NAME,
      type: "website",
      locale: "en_US",
      ...(ogImage ? { images: [{ url: ogImage }] } : {}),
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      ...(ogImage ? { images: [ogImage] } : {}),
    },
  };
}

/** Safe payload for <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(obj)} /> */
export function jsonLd(obj: object): { __html: string } {
  return { __html: JSON.stringify(obj).replace(/</g, "\\u003c") };
}
```

- [ ] `pnpm vitest run __tests__/marketing` — green. `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add SEO metadata builder and JSON-LD helper`

---

### Task 3: Market slug ↔ location path mapping — TDD

Market slugs are `houston-tx`; location URLs are `/locations/texas/houston`. Define the mapping once, derive both directions from `Market` objects (never string-parse slugs into pages — always resolve against `getMarkets()` so unknown paths 404).

**Files:**
- Create: `apps/web/components/marketing/market-slug.ts`
- Create: `apps/web/__tests__/marketing/market-slug.test.ts`

**Interfaces:**
- Consumes: `Market` from `@permittorch/types`.
- Produces (consumed by locations pages, sitemap, locations index):
  - `stateDisplayName(abbrev: string): string` — `"TX"` → `"Texas"`
  - `marketToLocationParams(market: Market): { state: string; city: string }` — URL segments, e.g. `{ state: "texas", city: "houston" }`
  - `marketLocationPath(market: Market): string` — `"/locations/texas/houston"`
  - `findMarketByLocationParams(markets: Market[], state: string, city: string): Market | undefined`

**Steps:**

- [ ] Write the failing test `apps/web/__tests__/marketing/market-slug.test.ts`:

```typescript
import { describe, expect, it } from "vitest";
import type { Market } from "@permittorch/types";
import {
  findMarketByLocationParams, marketLocationPath, marketToLocationParams, stateDisplayName,
} from "@/components/marketing/market-slug";
import { mockMarkets } from "@/lib/fixtures/markets";

const houston = mockMarkets[0];
const sanAntonio: Market = { id: "x", name: "San Antonio", city: "San Antonio", state: "TX", slug: "san-antonio-tx" };

describe("marketToLocationParams", () => {
  it("maps houston-tx to texas/houston", () => {
    expect(marketToLocationParams(houston)).toEqual({ state: "texas", city: "houston" });
  });
  it("hyphenates multi-word cities", () => {
    expect(marketToLocationParams(sanAntonio)).toEqual({ state: "texas", city: "san-antonio" });
  });
  it("falls back to the raw abbreviation for unknown states", () => {
    expect(marketToLocationParams({ ...houston, state: "ZZ" }).state).toBe("zz");
  });
});

describe("marketLocationPath", () => {
  it("builds the full route path", () => {
    expect(marketLocationPath(houston)).toBe("/locations/texas/houston");
  });
});

describe("stateDisplayName", () => {
  it("expands abbreviations", () => {
    expect(stateDisplayName("TX")).toBe("Texas");
    expect(stateDisplayName("tx")).toBe("Texas");
  });
  it("returns the input for unknown abbreviations", () => {
    expect(stateDisplayName("ZZ")).toBe("ZZ");
  });
});

describe("findMarketByLocationParams", () => {
  it("resolves a market from URL params, case-insensitively", () => {
    expect(findMarketByLocationParams(mockMarkets, "texas", "houston")?.slug).toBe("houston-tx");
    expect(findMarketByLocationParams(mockMarkets, "Texas", "HOUSTON")?.slug).toBe("houston-tx");
  });
  it("returns undefined for unknown params — pages must notFound()", () => {
    expect(findMarketByLocationParams(mockMarkets, "texas", "el-paso")).toBeUndefined();
    expect(findMarketByLocationParams(mockMarkets, "oklahoma", "houston")).toBeUndefined();
  });
});
```

- [ ] Run tests — fail. Implement `apps/web/components/marketing/market-slug.ts`:

```typescript
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
```

- [ ] `pnpm vitest run __tests__/marketing` — green. `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add market slug to location path mapping`

---

### Task 4: Marketing layout — nav, footer, terms, privacy

**Files:**
- Create: `apps/web/components/marketing/site-nav.tsx`
- Create: `apps/web/components/marketing/site-footer.tsx`
- Create: `apps/web/app/(marketing)/layout.tsx`
- Create: `apps/web/app/(marketing)/terms/page.tsx`
- Create: `apps/web/app/(marketing)/privacy/page.tsx`
- Create: `apps/web/__tests__/marketing/site-nav.test.tsx`

**Interfaces:**
- Consumes: `Button` from `@/components/ui/button`; `buildMetadata` from `@/lib/seo`.
- Produces: `SiteNav`, `SiteFooter` (used by the layout; every later page renders inside this layout), `/terms`, `/privacy` routes.

**Steps:**

- [ ] Write the failing nav test `apps/web/__tests__/marketing/site-nav.test.tsx` (add `// @vitest-environment jsdom` at the top if WS0's vitest config defaults to node):

```tsx
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SiteNav } from "@/components/marketing/site-nav";

describe("SiteNav", () => {
  it("renders the wordmark linking home", () => {
    render(<SiteNav />);
    const home = screen.getByRole("link", { name: /permittorch/i });
    expect(home).toHaveAttribute("href", "/");
  });

  it.each([
    ["Leads", "/fire-protection-leads"],
    ["Markets", "/locations"],
    ["How It Works", "/how-it-works"],
    ["Pricing", "/pricing"],
    ["Resources", "/blog"],
  ])("links %s to %s", (label, href) => {
    render(<SiteNav />);
    const links = screen.getAllByRole("link", { name: label });
    expect(links.some((l) => l.getAttribute("href") === href)).toBe(true);
  });

  it("renders Login and Start Free actions", () => {
    render(<SiteNav />);
    expect(screen.getAllByRole("link", { name: "Login" })[0]).toHaveAttribute("href", "/login");
    expect(screen.getAllByRole("link", { name: "Start Free" })[0]).toHaveAttribute("href", "/signup");
  });
});
```

If `toHaveAttribute` matchers are unavailable (WS0 may not have wired jest-dom), assert with `expect(home.getAttribute("href")).toBe("/")` instead — do NOT edit vitest config or package.json.

- [ ] Implement `apps/web/components/marketing/site-nav.tsx`:

```tsx
"use client";

import Link from "next/link";
import { useState } from "react";
import { Button } from "@/components/ui/button";

const NAV_LINKS = [
  { label: "Leads", href: "/fire-protection-leads" },
  { label: "Markets", href: "/locations" },
  { label: "How It Works", href: "/how-it-works" },
  { label: "Pricing", href: "/pricing" },
  { label: "Resources", href: "/blog" },
];

export function FlameMark({ className = "h-6 w-6 text-orange-500" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" className={className}>
      <path d="M12 2c.4 2.9-1.4 4.7-3 6.5C7.4 10.3 6 12.3 6 15a6 6 0 0 0 12 0c0-2.3-1-4.3-2.4-6C14.1 7.1 12.6 5.1 12 2Zm0 19a4 4 0 0 1-4-4c0-1.7.8-3 2-4.3.3 1.2 1 2.3 2 3.3 1-1 1.7-2.1 2-3.3 1.2 1.3 2 2.6 2 4.3a4 4 0 0 1-4 4Z" />
    </svg>
  );
}

export function SiteNav() {
  const [open, setOpen] = useState(false);
  return (
    <header className="sticky top-0 z-40 border-b border-neutral-200 bg-white/90 backdrop-blur">
      <nav className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
        <Link href="/" className="flex items-center gap-2 text-xl font-bold tracking-tight">
          <FlameMark />
          <span>Permit<span className="text-orange-500">Torch</span></span>
        </Link>

        <div className="hidden items-center gap-6 md:flex">
          {NAV_LINKS.map((l) => (
            <Link key={l.href} href={l.href}
              className="text-sm font-medium text-neutral-600 transition-colors hover:text-neutral-900">
              {l.label}
            </Link>
          ))}
        </div>

        <div className="hidden items-center gap-2 md:flex">
          <Button asChild variant="ghost"><Link href="/login">Login</Link></Button>
          <Button asChild className="bg-orange-500 text-white hover:bg-orange-600">
            <Link href="/signup">Start Free</Link>
          </Button>
        </div>

        <button type="button" aria-label="Toggle menu" aria-expanded={open}
          onClick={() => setOpen((v) => !v)}
          className="rounded-md p-2 text-neutral-600 hover:bg-neutral-100 md:hidden">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-6 w-6">
            {open
              ? <path strokeLinecap="round" d="M6 6l12 12M18 6L6 18" />
              : <path strokeLinecap="round" d="M4 7h16M4 12h16M4 17h16" />}
          </svg>
        </button>
      </nav>

      {open && (
        <div className="border-t border-neutral-200 bg-white px-4 pb-4 md:hidden">
          {NAV_LINKS.map((l) => (
            <Link key={l.href} href={l.href} onClick={() => setOpen(false)}
              className="block py-2.5 text-sm font-medium text-neutral-700">
              {l.label}
            </Link>
          ))}
          <div className="mt-3 flex gap-2">
            <Button asChild variant="outline" className="flex-1"><Link href="/login">Login</Link></Button>
            <Button asChild className="flex-1 bg-orange-500 text-white hover:bg-orange-600">
              <Link href="/signup">Start Free</Link>
            </Button>
          </div>
        </div>
      )}
    </header>
  );
}
```

- [ ] Implement `apps/web/components/marketing/site-footer.tsx`:

```tsx
import Link from "next/link";
import { FlameMark } from "@/components/marketing/site-nav";

const COLUMNS = [
  {
    heading: "Product",
    links: [
      { label: "Fire Protection Leads", href: "/fire-protection-leads" },
      { label: "Fire Sprinkler Leads", href: "/fire-sprinkler-leads" },
      { label: "Fire Alarm Leads", href: "/fire-alarm-leads" },
      { label: "How It Works", href: "/how-it-works" },
      { label: "Pricing", href: "/pricing" },
    ],
  },
  {
    heading: "Company",
    links: [
      { label: "Markets", href: "/locations" },
      { label: "Blog", href: "/blog" },
      { label: "Login", href: "/login" },
      { label: "Start Free", href: "/signup" },
    ],
  },
  {
    heading: "Legal",
    links: [
      { label: "Terms of Service", href: "/terms" },
      { label: "Privacy Policy", href: "/privacy" },
    ],
  },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-neutral-200 bg-neutral-50">
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-14 sm:px-6 md:grid-cols-4">
        <div>
          <div className="flex items-center gap-2 text-lg font-bold">
            <FlameMark className="h-5 w-5 text-orange-500" />
            <span>Permit<span className="text-orange-500">Torch</span></span>
          </div>
          <p className="mt-3 max-w-xs text-sm leading-relaxed text-neutral-500">
            Permit intelligence for fire-protection contractors.
          </p>
        </div>
        {COLUMNS.map((col) => (
          <div key={col.heading}>
            <h3 className="text-sm font-semibold text-neutral-900">{col.heading}</h3>
            <ul className="mt-3 space-y-2">
              {col.links.map((l) => (
                <li key={l.href}>
                  <Link href={l.href} className="text-sm text-neutral-500 hover:text-neutral-900">
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t border-neutral-200">
        <p className="mx-auto max-w-6xl px-4 py-6 text-xs leading-relaxed text-neutral-400 sm:px-6">
          &copy; 2026 PermitTorch. Lead data is derived from publicly available government permit and
          inspection records. PermitTorch does not guarantee accuracy or completeness — verify every
          opportunity independently. Records may be delayed or corrected by the issuing jurisdiction.
        </p>
      </div>
    </footer>
  );
}
```

- [ ] Implement `apps/web/app/(marketing)/layout.tsx`:

```tsx
import type { ReactNode } from "react";
import { SiteNav } from "@/components/marketing/site-nav";
import { SiteFooter } from "@/components/marketing/site-footer";

export default function MarketingLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col bg-white text-neutral-900">
      <SiteNav />
      <main className="flex-1">{children}</main>
      <SiteFooter />
    </div>
  );
}
```

- [ ] Implement `apps/web/app/(marketing)/terms/page.tsx` — real disclaimer copy per PRD §59:

```tsx
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";

export const metadata: Metadata = buildMetadata({
  title: "Terms of Service — PermitTorch",
  description: "The terms that govern your use of PermitTorch, including how we source public permit data and what we do and do not guarantee.",
  path: "/terms",
});

const SECTIONS: { heading: string; body: string[] }[] = [
  {
    heading: "1. The service",
    body: [
      "PermitTorch is a lead-intelligence service for fire-protection contractors. We monitor publicly available building permit and inspection records published by government jurisdictions, classify fire-related activity, and present it as scored opportunities.",
    ],
  },
  {
    heading: "2. Where the data comes from",
    body: [
      "All permit and inspection data in PermitTorch is collected from publicly available government records. For every record we retain the source URL, the issuing jurisdiction, and the time we retrieved it.",
      "PermitTorch does not create, alter, or certify government records. We organize and score what jurisdictions publish.",
    ],
  },
  {
    heading: "3. No guarantee of accuracy",
    body: [
      "PermitTorch does not guarantee the accuracy, completeness, or timeliness of any record. Jurisdictions may publish records late, amend them, or remove them. Lead scores are our own estimates of sales relevance — they are not a representation about any project, property, or party.",
      "You are responsible for independently verifying any opportunity before acting on it, including confirming permit status directly with the issuing jurisdiction.",
    ],
  },
  {
    heading: "4. Accounts and subscriptions",
    body: [
      "Paid plans are billed monthly in advance through Stripe. You may cancel at any time from your billing portal; access continues through the end of the paid period. Fees are non-refundable except where required by law.",
      "Your subscription entitles the users on your organization to the markets on your plan. Sharing exported data outside your organization, reselling it, or republishing it in bulk is not permitted.",
    ],
  },
  {
    heading: "5. Acceptable use",
    body: [
      "Do not scrape, crawl, or bulk-export the service beyond the export features your plan provides; do not use the service to harass property owners or misrepresent your relationship to a project; do not attempt to access markets or records outside your entitlement.",
    ],
  },
  {
    heading: "6. Limitation of liability",
    body: [
      "The service is provided as-is. To the maximum extent permitted by law, PermitTorch is not liable for lost profits, lost bids, or decisions made in reliance on the data. Our total liability is limited to the amount you paid us in the twelve months before the claim.",
    ],
  },
  {
    heading: "7. Changes",
    body: [
      "We may update these terms as the product evolves. Material changes will be announced by email to account holders before they take effect. Questions: support@permittorch.com.",
    ],
  },
];

export default function TermsPage() {
  return (
    <article className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight">Terms of Service</h1>
      <p className="mt-2 text-sm text-neutral-500">Last updated August 19, 2026</p>
      {SECTIONS.map((s) => (
        <section key={s.heading} className="mt-8">
          <h2 className="text-xl font-semibold">{s.heading}</h2>
          {s.body.map((p) => (
            <p key={p.slice(0, 32)} className="mt-3 leading-relaxed text-neutral-700">{p}</p>
          ))}
        </section>
      ))}
    </article>
  );
}
```

- [ ] Implement `apps/web/app/(marketing)/privacy/page.tsx` — same page shell and `SECTIONS` pattern, `buildMetadata({ title: "Privacy Policy — PermitTorch", description: "What PermitTorch collects, how we use it, and how public-record permit data fits in.", path: "/privacy" })`, with these sections (write them out fully in the file):
  1. **What we collect** — account details (name, email, company) via Clerk; billing via Stripe (we never store card numbers); sample-lead requests (name, work email, company, market); product usage analytics (PostHog).
  2. **Permit data is public-record data** — the leads themselves describe properties and permits published by government jurisdictions, not our users; we retain source URL, jurisdiction, and retrieval timestamp for every record; we do not guarantee its accuracy and users must verify independently; records may be delayed or corrected by jurisdictions.
  3. **How we use your information** — operate the service, send digests and sample leads you request, process payments, improve the product. We do not sell personal information.
  4. **Service providers** — Clerk (authentication), Stripe (billing), Resend (email), PostHog (analytics), hosting providers. Each receives only what it needs.
  5. **Retention and deletion** — account data kept while the account is active; email support@permittorch.com to delete your account and associated personal data.
  6. **Contact** — support@permittorch.com.
- [ ] `pnpm vitest run __tests__/marketing` — nav test green.
- [ ] Visual verify: from `apps/web/`, run `NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `http://localhost:3000/terms` and `/privacy`. Check: sticky white nav with flame + two-tone wordmark, orange Start Free button, all five nav links, mobile menu toggles at narrow width, footer columns + disclaimer line render, light theme throughout.
- [ ] `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add marketing layout with nav, footer, terms, and privacy pages`

---

### Task 5: `SampleLeadsForm` lead magnet component — TDD

The PRD §69 lead magnet: name, work email, company, market → `POST /api/sample-leads` via `submitSampleLeadRequest`. Server pages fetch markets and pass them in as a prop (keeps the client bundle free of fetching and makes tests trivial). Uses a styled native `<select>` (reliable in jsdom; radix Select is unnecessary here).

**Files:**
- Create: `apps/web/components/marketing/sample-leads-form.tsx`
- Create: `apps/web/__tests__/marketing/sample-leads-form.test.tsx`

**Interfaces:**
- Consumes: `submitSampleLeadRequest(input: { name: string; email: string; company: string; marketSlug: string }): Promise<void>` from `@/lib/api`; `Market` from `@permittorch/types`; `Button`, `Input` from shadcn/ui.
- Produces: `SampleLeadsForm({ markets }: { markets: Market[] })` — used by homepage (Task 6) and category landers (Task 9).

**Steps:**

- [ ] Write the failing test `apps/web/__tests__/marketing/sample-leads-form.test.tsx`:

```tsx
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";
import { mockMarkets } from "@/lib/fixtures/markets";
import * as api from "@/lib/api";

vi.mock("@/lib/api", () => ({ submitSampleLeadRequest: vi.fn() }));

describe("SampleLeadsForm", () => {
  beforeEach(() => vi.mocked(api.submitSampleLeadRequest).mockReset());

  async function fillValid(user: ReturnType<typeof userEvent.setup>) {
    await user.type(screen.getByLabelText(/name/i), "Dana Reyes");
    await user.type(screen.getByLabelText(/work email/i), "dana@reyesfire.com");
    await user.type(screen.getByLabelText(/company/i), "Reyes Fire Protection");
    await user.selectOptions(screen.getByLabelText(/market/i), "houston-tx");
  }

  it("lists every market as an option", () => {
    render(<SampleLeadsForm markets={mockMarkets} />);
    for (const m of mockMarkets) {
      expect(screen.getByRole("option", { name: `${m.city}, ${m.state}` })).toBeDefined();
    }
  });

  it("shows validation errors and does not submit when empty", async () => {
    const user = userEvent.setup();
    render(<SampleLeadsForm markets={mockMarkets} />);
    await user.click(screen.getByRole("button", { name: /send my sample leads/i }));
    expect(await screen.findByText("Enter your name.")).toBeDefined();
    expect(screen.getByText("Enter a valid work email.")).toBeDefined();
    expect(screen.getByText("Enter your company name.")).toBeDefined();
    expect(screen.getByText("Pick a market.")).toBeDefined();
    expect(api.submitSampleLeadRequest).not.toHaveBeenCalled();
  });

  it("rejects a malformed email", async () => {
    const user = userEvent.setup();
    render(<SampleLeadsForm markets={mockMarkets} />);
    await fillValid(user);
    await user.clear(screen.getByLabelText(/work email/i));
    await user.type(screen.getByLabelText(/work email/i), "not-an-email");
    await user.click(screen.getByRole("button", { name: /send my sample leads/i }));
    expect(await screen.findByText("Enter a valid work email.")).toBeDefined();
    expect(api.submitSampleLeadRequest).not.toHaveBeenCalled();
  });

  it("submits the exact payload and shows the success state", async () => {
    vi.mocked(api.submitSampleLeadRequest).mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<SampleLeadsForm markets={mockMarkets} />);
    await fillValid(user);
    await user.click(screen.getByRole("button", { name: /send my sample leads/i }));
    await waitFor(() =>
      expect(api.submitSampleLeadRequest).toHaveBeenCalledWith({
        name: "Dana Reyes",
        email: "dana@reyesfire.com",
        company: "Reyes Fire Protection",
        marketSlug: "houston-tx",
      }),
    );
    expect(await screen.findByText(/check your inbox — your sample leads are on the way/i)).toBeDefined();
  });

  it("shows an error message when the request fails", async () => {
    vi.mocked(api.submitSampleLeadRequest).mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    render(<SampleLeadsForm markets={mockMarkets} />);
    await fillValid(user);
    await user.click(screen.getByRole("button", { name: /send my sample leads/i }));
    expect(await screen.findByText(/something went wrong/i)).toBeDefined();
  });
});
```

- [ ] Run tests — fail. Implement `apps/web/components/marketing/sample-leads-form.tsx`:

```tsx
"use client";

import { useId, useState } from "react";
import Link from "next/link";
import type { Market } from "@permittorch/types";
import { submitSampleLeadRequest } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type Status = "idle" | "submitting" | "success" | "error";
interface Errors { name?: string; email?: string; company?: string; marketSlug?: string }

export function SampleLeadsForm({ markets }: { markets: Market[] }) {
  const uid = useId();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [company, setCompany] = useState("");
  const [marketSlug, setMarketSlug] = useState("");
  const [errors, setErrors] = useState<Errors>({});
  const [status, setStatus] = useState<Status>("idle");

  function validate(): Errors {
    const e: Errors = {};
    if (!name.trim()) e.name = "Enter your name.";
    if (!EMAIL_RE.test(email.trim())) e.email = "Enter a valid work email.";
    if (!company.trim()) e.company = "Enter your company name.";
    if (!marketSlug) e.marketSlug = "Pick a market.";
    return e;
  }

  async function handleSubmit(ev: React.FormEvent) {
    ev.preventDefault();
    const e = validate();
    setErrors(e);
    if (Object.keys(e).length > 0) return;
    setStatus("submitting");
    try {
      await submitSampleLeadRequest({
        name: name.trim(), email: email.trim(), company: company.trim(), marketSlug,
      });
      setStatus("success");
    } catch {
      setStatus("error");
    }
  }

  if (status === "success") {
    return (
      <div className="rounded-xl border border-orange-200 bg-white p-8 text-center shadow-sm">
        <p className="text-lg font-semibold">
          Check your inbox — your sample leads are on the way.
        </p>
        <p className="mt-2 text-neutral-600">Want new opportunities every morning?</p>
        <Button asChild className="mt-4 bg-orange-500 text-white hover:bg-orange-600">
          <Link href="/signup">Start Free</Link>
        </Button>
      </div>
    );
  }

  const field = (
    id: string, label: string, value: string, set: (v: string) => void,
    error: string | undefined, type = "text", placeholder = "",
  ) => (
    <div>
      <label htmlFor={id} className="mb-1.5 block text-sm font-medium text-neutral-700">{label}</label>
      <Input id={id} type={type} value={value} placeholder={placeholder}
        onChange={(ev) => set(ev.target.value)} aria-invalid={Boolean(error)} />
      {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
    </div>
  );

  return (
    <form onSubmit={handleSubmit} noValidate
      className="grid gap-4 rounded-xl border border-neutral-200 bg-white p-6 shadow-sm sm:grid-cols-2">
      {field(`${uid}-name`, "Name", name, setName, errors.name, "text", "Dana Reyes")}
      {field(`${uid}-email`, "Work email", email, setEmail, errors.email, "email", "you@yourcompany.com")}
      {field(`${uid}-company`, "Company", company, setCompany, errors.company, "text", "Reyes Fire Protection")}
      <div>
        <label htmlFor={`${uid}-market`} className="mb-1.5 block text-sm font-medium text-neutral-700">
          Market
        </label>
        <select id={`${uid}-market`} value={marketSlug}
          onChange={(ev) => setMarketSlug(ev.target.value)}
          aria-invalid={Boolean(errors.marketSlug)}
          className="h-9 w-full rounded-md border border-neutral-200 bg-white px-3 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-orange-500">
          <option value="">Choose your market…</option>
          {markets.map((m) => (
            <option key={m.slug} value={m.slug}>{`${m.city}, ${m.state}`}</option>
          ))}
        </select>
        {errors.marketSlug && <p className="mt-1 text-sm text-red-600">{errors.marketSlug}</p>}
      </div>
      <div className="sm:col-span-2">
        <Button type="submit" disabled={status === "submitting"}
          className="w-full bg-orange-500 text-white hover:bg-orange-600">
          {status === "submitting" ? "Sending…" : "Send My Sample Leads"}
        </Button>
        {status === "error" && (
          <p className="mt-2 text-sm text-red-600">
            Something went wrong sending your request. Try again in a minute.
          </p>
        )}
        <p className="mt-2 text-center text-xs text-neutral-400">
          5–10 real opportunities from your market. No spam, no obligation.
        </p>
      </div>
    </form>
  );
}
```

- [ ] `pnpm vitest run __tests__/marketing` — green. `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add sample leads request form`

---

### Task 6: Homepage `/`

Hero per PRD §22, 4-step strip, value bullets (social-proof-free — NO fake testimonials or invented customer counts), `#sample-leads` lead-magnet section, closing CTA band.

**Files:**
- Create: `apps/web/components/marketing/how-it-works-steps.ts` (shared step copy, reused expanded in Task 8)
- Create: `apps/web/app/(marketing)/page.tsx`

**Interfaces:**
- Consumes: `getMarkets(): Promise<Market[]>` from `@/lib/api`; `SampleLeadsForm` (Task 5); `buildMetadata` (Task 2); `Button`, `Card` from shadcn/ui.
- Produces: `/` route; `HOW_IT_WORKS_STEPS` export (Task 8 consumes).

**Steps:**

- [ ] Pre-flight route collision: WS0 deliberately left a placeholder at `apps/web/app/page.tsx` (it kept the scaffold building before WS3 existed). A root `page.tsx` and `(marketing)/page.tsx` cannot both resolve `/`, so WS3 is **explicitly authorized** to remove it as part of this task: `git rm apps/web/app/page.tsx` (include this deletion in this task's commit). This is the single sanctioned exception to WS3's file-ownership boundary.
- [ ] Create `apps/web/components/marketing/how-it-works-steps.ts`:

```typescript
export interface HowItWorksStep {
  step: number;
  title: string;
  summary: string;   // homepage strip
  detail: string;    // expanded on /how-it-works
}

export const HOW_IT_WORKS_STEPS: HowItWorksStep[] = [
  {
    step: 1,
    title: "We monitor public records",
    summary: "PermitTorch watches permit and inspection feeds across your market, every day.",
    detail:
      "Cities publish building permits and inspection results constantly — but across dozens of portals, formats, and update schedules. PermitTorch pulls from every active source in your market on a daily cycle, keeps a health check on each one, and shows you exactly when the data was last updated. If a source goes stale, we say so instead of pretending it is current.",
  },
  {
    step: 2,
    title: "We identify the fire work",
    summary: "Sprinkler, alarm, suppression, kitchen systems, failed inspections — classified automatically.",
    detail:
      "Most permits are noise for a fire contractor: plumbing, roofing, fences. PermitTorch classifies each record against fire-specific rules — sprinkler scope, alarm scope, suppression systems, kitchen hood systems, fire inspections, and code violations — so your feed contains only work you could actually bid.",
  },
  {
    step: 3,
    title: "We score every opportunity",
    summary: "A 0–100 score built from real signals: value, scope, timing, and who is already on the job.",
    detail:
      "Every lead gets a deterministic 0–100 score assembled from named signals — new commercial construction, detected fire scope, how recently it was filed, project value, square footage, and whether a fire contractor is already listed. No black box: each point traces to a signal you can read, so you can trust the ranking or overrule it.",
  },
  {
    step: 4,
    title: "You get actionable leads",
    summary: "A ranked list with the address, the permit, and the reason it matters. Chase the good ones first.",
    detail:
      "Open your dashboard or your morning digest and work top-down: address, permit number, filing date, estimated value, and a one-line reason this lead matters. Save the ones you are chasing, mark them contacted, export to CSV, and click through to the official government record any time.",
  },
];
```

- [ ] Create `apps/web/app/(marketing)/page.tsx`:

```tsx
import Link from "next/link";
import type { Metadata } from "next";
import { getMarkets } from "@/lib/api";
import { buildMetadata } from "@/lib/seo";
import { Button } from "@/components/ui/button";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";
import { HOW_IT_WORKS_STEPS } from "@/components/marketing/how-it-works-steps";

export const metadata: Metadata = buildMetadata({
  title: "PermitTorch — Fire Protection Leads From Public Permit Data",
  description:
    "PermitTorch monitors public permit and inspection records and identifies fire-protection opportunities before they disappear into another spreadsheet.",
  path: "/",
});

const VALUE_BULLETS = [
  {
    title: "Built only for fire protection",
    body: "No plumbing permits, no roofing noise. Sprinkler, alarm, suppression, kitchen systems, and inspections — that is the whole feed.",
  },
  {
    title: "Every score is explained",
    body: "A 91 is a 91 for reasons you can read: new commercial build, sprinkler scope detected, filed this week, no fire contractor listed.",
  },
  {
    title: "Freshness you can check",
    body: "Every lead shows when its source was last updated. We never dress up stale data as current.",
  },
  {
    title: "Straight to the record",
    body: "Each lead links to the official government permit page, so you can verify before you drive across town.",
  },
];

export default async function HomePage() {
  const markets = await getMarkets();
  return (
    <>
      {/* Hero */}
      <section className="mx-auto max-w-6xl px-4 pb-20 pt-24 text-center sm:px-6">
        <h1 className="mx-auto max-w-3xl text-4xl font-bold tracking-tight sm:text-6xl">
          Find the permits worth chasing<span className="text-orange-500">.</span>
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-lg leading-relaxed text-neutral-600">
          PermitTorch monitors public permit and inspection records and identifies
          fire-protection opportunities before they disappear into another spreadsheet.
        </p>
        <div className="mt-10 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Button asChild size="lg" className="bg-orange-500 px-8 text-white hover:bg-orange-600">
            <Link href="/signup">Find Leads in Your Market</Link>
          </Button>
          <Button asChild size="lg" variant="outline" className="px-8">
            <Link href="#sample-leads">See Sample Leads</Link>
          </Button>
        </div>
      </section>

      {/* How it works strip */}
      <section className="border-y border-neutral-200 bg-neutral-50">
        <div className="mx-auto grid max-w-6xl gap-8 px-4 py-16 sm:px-6 md:grid-cols-4">
          {HOW_IT_WORKS_STEPS.map((s) => (
            <div key={s.step}>
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-orange-100 text-sm font-bold text-orange-600">
                {s.step}
              </div>
              <h2 className="mt-4 text-base font-semibold">{s.title}</h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{s.summary}</p>
            </div>
          ))}
        </div>
        <div className="pb-12 text-center">
          <Link href="/how-it-works" className="text-sm font-medium text-orange-600 hover:text-orange-700">
            See how scoring works →
          </Link>
        </div>
      </section>

      {/* Value bullets (social-proof-free) */}
      <section className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
        <h2 className="text-center text-3xl font-bold tracking-tight">
          Why fire contractors use PermitTorch
        </h2>
        <div className="mt-12 grid gap-6 sm:grid-cols-2">
          {VALUE_BULLETS.map((b) => (
            <div key={b.title} className="rounded-xl border border-neutral-200 p-6">
              <h3 className="font-semibold">{b.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{b.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Lead magnet */}
      <section id="sample-leads" className="scroll-mt-20 border-y border-orange-100 bg-orange-50">
        <div className="mx-auto max-w-3xl px-4 py-20 sm:px-6">
          <h2 className="text-center text-3xl font-bold tracking-tight">
            See this week&apos;s hottest fire protection opportunities in your market
          </h2>
          <p className="mx-auto mt-4 max-w-xl text-center text-neutral-600">
            Tell us where you work and we&apos;ll email you 5–10 real, scored opportunities
            PermitTorch found there recently. Free — see the product before you sign up.
          </p>
          <div className="mt-10">
            <SampleLeadsForm markets={markets} />
          </div>
        </div>
      </section>

      {/* Closing CTA */}
      <section className="mx-auto max-w-6xl px-4 py-20 text-center sm:px-6">
        <h2 className="text-3xl font-bold tracking-tight">Get new opportunities every morning.</h2>
        <p className="mx-auto mt-4 max-w-xl text-neutral-600">
          Your competitors are refreshing permit portals by hand — or not looking at all.
          Be the first call on the jobs worth winning.
        </p>
        <Button asChild size="lg" className="mt-8 bg-orange-500 px-8 text-white hover:bg-orange-600">
          <Link href="/signup">Start Free</Link>
        </Button>
      </section>
    </>
  );
}
```

- [ ] Visual verify: `NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/`. Check: hero headline with orange period, both CTAs (primary → `/signup`, secondary smooth-anchors to `#sample-leads`), 4-step strip on the gray band, 4 value cards, orange lead-magnet band with working form (submit valid data → success state, since mock mode resolves), closing CTA. Check mobile width (~375px): everything stacks, no horizontal scroll.
- [ ] View page source (`curl -s localhost:3000 | head -80`): confirm `<title>`, meta description, `link rel="canonical" href="https://permittorch.com/"`, `og:` tags are server-rendered.
- [ ] `pnpm vitest run __tests__/marketing` still green. `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add marketing homepage with hero, how-it-works strip, and lead magnet`

---

### Task 7: `/pricing` — tier cards + FAQ accordion

Three tiers exactly per PRD §26. Pro is visually highlighted and badged "Recommended". All CTAs → `/signup` (checkout itself is WS2/WS4 territory). Tiers rendered by a sync `PricingTiers` component so it is unit-testable.

**Files:**
- Create: `apps/web/components/marketing/pricing-tiers.tsx`
- Create: `apps/web/app/(marketing)/pricing/page.tsx`
- Create: `apps/web/__tests__/marketing/pricing-tiers.test.tsx`

**Interfaces:**
- Consumes: `Button`, `Card`, `Accordion` (shadcn/ui); `buildMetadata`, `jsonLd` from `@/lib/seo`.
- Produces: `PricingTiers` component, `PRICING_TIERS` data, `/pricing` route.

**Steps:**

- [ ] Write the failing test `apps/web/__tests__/marketing/pricing-tiers.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PricingTiers, PRICING_TIERS } from "@/components/marketing/pricing-tiers";

describe("PricingTiers", () => {
  it("defines exactly Starter $49, Pro $129, Territory $249", () => {
    expect(PRICING_TIERS.map((t) => [t.name, t.price])).toEqual([
      ["Starter", 49], ["Pro", 129], ["Territory", 249],
    ]);
  });

  it("renders all three tiers with prices", () => {
    render(<PricingTiers />);
    for (const t of PRICING_TIERS) {
      expect(screen.getByText(t.name)).toBeDefined();
      expect(screen.getByText(`$${t.price}`)).toBeDefined();
    }
  });

  it("marks only Pro as Recommended", () => {
    render(<PricingTiers />);
    expect(screen.getAllByText("Recommended")).toHaveLength(1);
    expect(PRICING_TIERS.find((t) => t.highlighted)?.name).toBe("Pro");
  });

  it("sends every CTA to /signup", () => {
    render(<PricingTiers />);
    const ctas = screen.getAllByRole("link", { name: /start free|choose/i });
    expect(ctas).toHaveLength(3);
    for (const c of ctas) expect(c.getAttribute("href")).toBe("/signup");
  });
});
```

- [ ] Implement `apps/web/components/marketing/pricing-tiers.tsx`:

```tsx
import Link from "next/link";
import { Button } from "@/components/ui/button";

export interface PricingTier {
  name: string;
  price: number;          // USD per month
  blurb: string;
  features: string[];
  highlighted: boolean;
  cta: string;
}

export const PRICING_TIERS: PricingTier[] = [
  {
    name: "Starter",
    price: 49,
    blurb: "For a solo owner keeping an eye on one market.",
    features: ["1 market", "Weekly email digest", "Basic filtering", "Limited historical records"],
    highlighted: false,
    cta: "Choose Starter",
  },
  {
    name: "Pro",
    price: 129,
    blurb: "For contractors actively chasing new work every week.",
    features: [
      "1 market", "Daily updates", "Full lead scoring", "Saved opportunities",
      "Daily email digest", "CSV export", "30–90 days of history",
    ],
    highlighted: true,
    cta: "Start Free with Pro",
  },
  {
    name: "Territory",
    price: 249,
    blurb: "For teams covering multiple metros.",
    features: [
      "Up to 5 markets", "Multiple users", "Advanced filters", "Daily alerts",
      "Full historical records", "Priority support",
    ],
    highlighted: false,
    cta: "Choose Territory",
  },
];

function Check() {
  return (
    <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"
      className="mt-0.5 h-4 w-4 shrink-0 text-orange-500">
      <path fillRule="evenodd" clipRule="evenodd"
        d="M16.7 5.3a1 1 0 0 1 0 1.4l-7 7a1 1 0 0 1-1.4 0l-3-3a1 1 0 1 1 1.4-1.4L9 11.6l6.3-6.3a1 1 0 0 1 1.4 0Z" />
    </svg>
  );
}

export function PricingTiers() {
  return (
    <div className="grid gap-6 md:grid-cols-3">
      {PRICING_TIERS.map((t) => (
        <div key={t.name}
          className={
            t.highlighted
              ? "relative rounded-2xl border-2 border-orange-500 bg-white p-8 shadow-lg"
              : "rounded-2xl border border-neutral-200 bg-white p-8"
          }>
          {t.highlighted && (
            <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-orange-500 px-3 py-0.5 text-xs font-semibold text-white">
              Recommended
            </span>
          )}
          <h2 className="text-lg font-semibold">{t.name}</h2>
          <p className="mt-1 text-sm text-neutral-500">{t.blurb}</p>
          <p className="mt-5">
            <span className="text-4xl font-bold tracking-tight">${t.price}</span>
            <span className="text-neutral-500">/month</span>
          </p>
          <ul className="mt-6 space-y-2.5">
            {t.features.map((f) => (
              <li key={f} className="flex gap-2 text-sm text-neutral-700"><Check />{f}</li>
            ))}
          </ul>
          <Button asChild className={
            t.highlighted
              ? "mt-8 w-full bg-orange-500 text-white hover:bg-orange-600"
              : "mt-8 w-full"
          } variant={t.highlighted ? "default" : "outline"}>
            <Link href="/signup">{t.cta}</Link>
          </Button>
        </div>
      ))}
    </div>
  );
}
```

- [ ] Implement `apps/web/app/(marketing)/pricing/page.tsx` with metadata, the tiers, and the FAQ accordion (shadcn `Accordion type="single" collapsible`) plus FAQPage JSON-LD via `jsonLd`:

```tsx
import type { Metadata } from "next";
import { buildMetadata, jsonLd } from "@/lib/seo";
import { PricingTiers } from "@/components/marketing/pricing-tiers";
import {
  Accordion, AccordionContent, AccordionItem, AccordionTrigger,
} from "@/components/ui/accordion";

export const metadata: Metadata = buildMetadata({
  title: "Pricing — PermitTorch",
  description:
    "Plans for fire protection contractors: Starter $49/mo, Pro $129/mo with daily updates and CSV export, Territory $249/mo for up to 5 markets. Cancel anytime.",
  path: "/pricing",
});

export const PRICING_FAQ = [
  {
    q: "How does billing work?",
    a: "Plans are billed monthly through Stripe. You can upgrade, downgrade, or switch markets at any time, and Stripe prorates the difference automatically. No setup fees, no annual contracts.",
  },
  {
    q: "Is there a free trial?",
    a: "Yes. Every new account starts with a 7-day Pro trial in one market — you see real, fully scored leads before you're ever charged. You can also request free sample leads from any market without creating an account.",
  },
  {
    q: "What counts as a market?",
    a: "A market is a metro area PermitTorch actively covers — for example Houston, TX. Starter and Pro include one market of your choice; Territory covers up to five. We only sell markets where we have live data coverage, and we add new ones as coverage comes online.",
  },
  {
    q: "Can I cancel anytime?",
    a: "Yes. Cancel in two clicks from your billing portal — no phone call, no retention script. You keep full access through the end of your current billing period, and there are no cancellation fees.",
  },
];

export default function PricingPage() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="text-center text-4xl font-bold tracking-tight">
        Simple pricing. Real leads.
      </h1>
      <p className="mx-auto mt-4 max-w-xl text-center text-neutral-600">
        Every plan is month to month. Pick your market, start with a 7-day Pro trial,
        and cancel whenever you want.
      </p>
      <div className="mt-14">
        <PricingTiers />
      </div>

      <section className="mx-auto mt-24 max-w-2xl">
        <h2 className="text-center text-2xl font-bold tracking-tight">Pricing questions</h2>
        <Accordion type="single" collapsible className="mt-8">
          {PRICING_FAQ.map((f, i) => (
            <AccordionItem key={f.q} value={`faq-${i}`}>
              <AccordionTrigger className="text-left">{f.q}</AccordionTrigger>
              <AccordionContent className="text-neutral-600">{f.a}</AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd({
        "@context": "https://schema.org",
        "@type": "FAQPage",
        mainEntity: PRICING_FAQ.map((f) => ({
          "@type": "Question",
          name: f.q,
          acceptedAnswer: { "@type": "Answer", text: f.a },
        })),
      })} />
    </div>
  );
}
```

Note: if Next flags exporting `PRICING_FAQ` from a page file, move the array into `components/marketing/pricing-tiers.tsx` and import it — do not suppress the error.

- [ ] `pnpm vitest run __tests__/marketing` — green.
- [ ] Visual verify: open `/pricing`. Check: three cards, Pro elevated with orange border + Recommended pill, checkmark feature lists match PRD §26 exactly, accordion expands/collapses, all three CTAs land on `/signup`. Confirm unique title/canonical in page source.
- [ ] `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add pricing page with tier cards and billing FAQ`

---

### Task 8: `/how-it-works` — expanded steps + explainable-scoring example

**Files:**
- Create: `apps/web/app/(marketing)/how-it-works/page.tsx`

**Interfaces:**
- Consumes: `HOW_IT_WORKS_STEPS` (Task 6), `buildMetadata` (Task 2), `Button`.
- Produces: `/how-it-works` route.

**Steps:**

- [ ] Create `apps/web/app/(marketing)/how-it-works/page.tsx`:

```tsx
import Link from "next/link";
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { Button } from "@/components/ui/button";
import { HOW_IT_WORKS_STEPS } from "@/components/marketing/how-it-works-steps";

export const metadata: Metadata = buildMetadata({
  title: "How PermitTorch Works — From Public Records to Scored Fire Leads",
  description:
    "How PermitTorch turns public permit and inspection records into scored, explainable fire-protection leads: monitor, classify, score, deliver.",
  path: "/how-it-works",
});

// Static illustrative example per PRD §16 — clearly labeled, not live data.
const SCORE_EXAMPLE = {
  total: 91,
  headline: "New commercial build — sprinkler scope, no fire contractor listed",
  signals: [
    { label: "New commercial project", points: 25 },
    { label: "Sprinkler scope detected", points: 20 },
    { label: "Filed within 48 hours", points: 15 },
    { label: "Project value over $1M", points: 15 },
    { label: "No fire contractor listed", points: 10 },
    { label: "Large commercial property", points: 6 },
  ],
};

export default function HowItWorksPage() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">How PermitTorch works</h1>
      <p className="mt-4 max-w-2xl text-lg text-neutral-600">
        Four steps between a city permit portal and your morning lead list. No black box at any of them.
      </p>

      <ol className="mt-14 space-y-12">
        {HOW_IT_WORKS_STEPS.map((s) => (
          <li key={s.step} className="flex gap-5">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-orange-100 font-bold text-orange-600">
              {s.step}
            </div>
            <div>
              <h2 className="text-xl font-semibold">{s.title}</h2>
              <p className="mt-2 leading-relaxed text-neutral-600">{s.detail}</p>
            </div>
          </li>
        ))}
      </ol>

      <section className="mt-20">
        <h2 className="text-2xl font-bold tracking-tight">Every score is explainable</h2>
        <p className="mt-3 max-w-2xl leading-relaxed text-neutral-600">
          A lead score is never a mystery number. It is the sum of named signals, and you see
          every one of them on every lead. Here is what that looks like:
        </p>

        <div className="mt-8 rounded-2xl border border-neutral-200 bg-white p-6 shadow-sm sm:p-8">
          <p className="text-xs font-medium uppercase tracking-wide text-neutral-400">
            Illustrative example — not a live lead
          </p>
          <div className="mt-3 flex items-center gap-4">
            <span className="flex h-14 w-14 items-center justify-center rounded-xl bg-orange-500 text-2xl font-bold text-white">
              {SCORE_EXAMPLE.total}
            </span>
            <div>
              <h3 className="font-semibold">Why this is a {SCORE_EXAMPLE.total}</h3>
              <p className="text-sm text-neutral-500">{SCORE_EXAMPLE.headline}</p>
            </div>
          </div>
          <ul className="mt-6 divide-y divide-neutral-100">
            {SCORE_EXAMPLE.signals.map((s) => (
              <li key={s.label} className="flex items-center justify-between py-2.5 text-sm">
                <span className="text-neutral-700">{s.label}</span>
                <span className="font-semibold text-orange-600">+{s.points}</span>
              </li>
            ))}
          </ul>
        </div>
        <p className="mt-4 text-sm text-neutral-500">
          Scores run 0–100 and are computed by fixed, configurable rules — the same permit
          always scores the same way. Negative signals (old permits, closed permits) subtract
          points just as visibly.
        </p>
      </section>

      <section className="mt-20 rounded-2xl bg-orange-50 p-10 text-center">
        <h2 className="text-2xl font-bold tracking-tight">See it on your own market</h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">
          Start free and watch tomorrow&apos;s permits show up scored and sorted.
        </p>
        <Button asChild size="lg" className="mt-6 bg-orange-500 px-8 text-white hover:bg-orange-600">
          <Link href="/signup">Find Leads in Your Market</Link>
        </Button>
      </section>
    </div>
  );
}
```

- [ ] Visual verify: open `/how-it-works`. Check: four numbered expanded steps, the 91-score card with the six +N signal rows summing to 91, "Illustrative example" label visible, CTA band. Verify signal rows match PRD §16 exactly (+25/+20/+15/+15/+10/+6).
- [ ] `pnpm tsc --noEmit` — clean. `pnpm vitest run __tests__/marketing` — still green.
- [ ] Commit: `Add how-it-works page with explainable scoring example`

---

### Task 9: Category landers — `/fire-protection-leads`, `/fire-sprinkler-leads`, `/fire-alarm-leads`

One `CategoryLander` component parameterized by a content object; three thin route files. Each lander: unique headline + subline, three pain points, three "how PermitTorch finds it" paragraphs, lead-magnet form, CTA. Unique metadata per page.

**Files:**
- Create: `apps/web/components/marketing/category-lander.tsx` (component + `CATEGORY_LANDERS` content record)
- Create: `apps/web/app/(marketing)/fire-protection-leads/page.tsx`
- Create: `apps/web/app/(marketing)/fire-sprinkler-leads/page.tsx`
- Create: `apps/web/app/(marketing)/fire-alarm-leads/page.tsx`

**Interfaces:**
- Consumes: `getMarkets()`, `SampleLeadsForm`, `buildMetadata`, `Button`.
- Produces: `CategoryLander({ content, markets })`, `CATEGORY_LANDERS: Record<"fire-protection" | "fire-sprinkler" | "fire-alarm", CategoryLanderContent>`, three routes (sitemap Task 12 lists them).

**Steps:**

- [ ] Create `apps/web/components/marketing/category-lander.tsx`:

```tsx
import Link from "next/link";
import type { Market } from "@permittorch/types";
import { Button } from "@/components/ui/button";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";

export interface CategoryLanderContent {
  path: string;
  metaTitle: string;
  metaDescription: string;
  headline: string;
  subline: string;
  painHeading: string;
  painPoints: { title: string; body: string }[];
  howHeading: string;
  howParagraphs: string[];
  ctaHeading: string;
  ctaBody: string;
}

export const CATEGORY_LANDERS = {
  "fire-protection": {
    path: "/fire-protection-leads",
    metaTitle: "Fire Protection Leads From Live Permit Data — PermitTorch",
    metaDescription:
      "Scored fire protection leads pulled daily from public permit and inspection records: sprinkler, alarm, suppression, kitchen systems, and failed inspections.",
    headline: "Fire protection leads from live permit data.",
    subline:
      "Every fire-related opportunity in your market — sprinkler, alarm, suppression, kitchen systems, inspections — pulled from public records, scored, and explained.",
    painHeading: "Finding fire work is a part-time job you didn't sign up for",
    painPoints: [
      {
        title: "You hear about projects after they're bid",
        body: "By the time a job reaches a bid board or a GC's shortlist, the price war has started. The profitable window was weeks earlier, when the permit was filed.",
      },
      {
        title: "Permit portals weren't built for you",
        body: "Every city runs a different portal with different search, different fields, and different update schedules. Checking them all by hand costs hours a week.",
      },
      {
        title: "Most permits are noise",
        body: "Hundreds of new records a day, and a handful are fire work. Skimming plumbing and fence permits to find one sprinkler job is a bad use of anyone's time.",
      },
    ],
    howHeading: "How PermitTorch finds fire protection work",
    howParagraphs: [
      "PermitTorch monitors every active permit and inspection source in your market on a daily cycle and runs each new record through fire-specific classification rules. Sprinkler scope, alarm scope, suppression systems, kitchen hood systems, fire inspections, and code violations each get their own category — everything else is filtered out.",
      "Each fire-related record is then scored 0–100 from named signals: new commercial construction, detected fire scope, filing recency, project value, square footage, and whether a fire contractor is already listed on the permit. You see every signal behind every score.",
      "The result is a ranked feed with the address, permit number, estimated value, a one-line reason it matters, and a link to the official government record — fresh every morning, with an honest last-updated time on every source.",
    ],
    ctaHeading: "Stop refreshing permit portals.",
    ctaBody: "Start free and see the fire work in your market — scored, sorted, and explained.",
  },
  "fire-sprinkler": {
    path: "/fire-sprinkler-leads",
    metaTitle: "Fire Sprinkler Leads Before the Bid Hits the Street — PermitTorch",
    metaDescription:
      "Find fire sprinkler projects at the permit stage: new commercial builds, tenant improvements, and sprinkler-scope permits with no fire contractor listed yet.",
    headline: "Fire sprinkler leads before the bid hits the street.",
    subline:
      "New commercial builds and tenant improvements need sprinkler work early. PermitTorch spots the permits with sprinkler scope while the job is still up for grabs.",
    painHeading: "Sprinkler work gets decided early — earlier than you're hearing about it",
    painPoints: [
      {
        title: "The GC already has a number",
        body: "When a sprinkler package shows up on a bid board, three shops have usually priced it. Winning from there means cutting margin, not adding value.",
      },
      {
        title: "New construction hides in plain sight",
        body: "Every new commercial building over the code threshold needs sprinklers. Those projects are sitting in the permit record weeks before anyone calls a sprinkler contractor.",
      },
      {
        title: "TI work is scattered and constant",
        body: "Tenant improvements — head relocations, coverage changes, occupancy shifts — file year-round in small batches. Nobody has time to catch them all manually.",
      },
    ],
    howHeading: "How PermitTorch finds sprinkler work",
    howParagraphs: [
      "PermitTorch classifies permits into a dedicated fire-sprinkler category using scope language in the permit type and description — sprinkler systems, riser work, head counts, NFPA 13 references — plus new commercial construction that will require coverage.",
      "Sprinkler leads score highest when the signals stack: a new commercial project, sprinkler scope detected, filed in the last 48 hours, seven-figure project value, and no fire contractor listed on the permit yet. That last one matters most — it means the package likely hasn't been awarded.",
      "You get the address, the permit, the estimated value, and the reason it scored the way it did, with a link to the official record so you can verify scope before you pick up the phone.",
    ],
    ctaHeading: "Be the first sprinkler contractor to call.",
    ctaBody: "Start free and see this week's sprinkler-scope permits in your market.",
  },
  "fire-alarm": {
    path: "/fire-alarm-leads",
    metaTitle: "Fire Alarm Leads Straight From the Permit Record — PermitTorch",
    metaDescription:
      "Find fire alarm projects early: alarm-scope permits, tenant upfits, occupancy changes, and failed inspections that need a fire alarm contractor.",
    headline: "Fire alarm leads straight from the permit record.",
    subline:
      "Alarm work rides on tenant upfits, occupancy changes, and inspection failures. PermitTorch catches all three the day they hit the public record.",
    painHeading: "Alarm opportunities are frequent, small, and easy to miss",
    painPoints: [
      {
        title: "Upfits move fast",
        body: "A tenant improvement goes from permit to occupied in weeks. If you find the alarm scope a month late, someone else's panel is already on the wall.",
      },
      {
        title: "Failed inspections don't advertise",
        body: "A failed fire inspection or a violation notice is a ready-to-buy customer — but it's buried in inspection records nobody reads systematically.",
      },
      {
        title: "Monitoring contracts follow the install",
        body: "Every install you miss is also the recurring monitoring revenue you miss. The cost of a missed alarm lead compounds for years.",
      },
    ],
    howHeading: "How PermitTorch finds alarm work",
    howParagraphs: [
      "PermitTorch classifies permits into a dedicated fire-alarm category from scope language in permit types and descriptions — fire alarm systems, panel replacements, notification devices, NFPA 72 references — across every source in your market.",
      "It also watches the records that generate alarm work indirectly: commercial tenant improvements and occupancy changes that trigger alarm requirements, and failed fire inspections or violation corrections where the owner has to act now.",
      "Every alarm lead is scored 0–100 with visible signals — recency, project value, scope, whether a fire contractor is already listed — so your first calls of the morning go to the jobs most worth winning.",
    ],
    ctaHeading: "Catch the upfits and the failed inspections.",
    ctaBody: "Start free and see this week's alarm opportunities in your market.",
  },
} satisfies Record<string, CategoryLanderContent>;

export function CategoryLander({ content, markets }: { content: CategoryLanderContent; markets: Market[] }) {
  return (
    <>
      <section className="mx-auto max-w-4xl px-4 pb-16 pt-24 text-center sm:px-6">
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">{content.headline}</h1>
        <p className="mx-auto mt-5 max-w-2xl text-lg leading-relaxed text-neutral-600">{content.subline}</p>
        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Button asChild size="lg" className="bg-orange-500 px-8 text-white hover:bg-orange-600">
            <Link href="/signup">Find Leads in Your Market</Link>
          </Button>
          <Button asChild size="lg" variant="outline" className="px-8">
            <Link href="#sample-leads">See Sample Leads</Link>
          </Button>
        </div>
      </section>

      <section className="border-y border-neutral-200 bg-neutral-50">
        <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
          <h2 className="text-center text-2xl font-bold tracking-tight">{content.painHeading}</h2>
          <div className="mt-10 grid gap-6 md:grid-cols-3">
            {content.painPoints.map((p) => (
              <div key={p.title} className="rounded-xl border border-neutral-200 bg-white p-6">
                <h3 className="font-semibold">{p.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-neutral-600">{p.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
        <h2 className="text-2xl font-bold tracking-tight">{content.howHeading}</h2>
        {content.howParagraphs.map((p) => (
          <p key={p.slice(0, 32)} className="mt-4 leading-relaxed text-neutral-700">{p}</p>
        ))}
      </section>

      <section id="sample-leads" className="scroll-mt-20 border-y border-orange-100 bg-orange-50">
        <div className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
          <h2 className="text-center text-2xl font-bold tracking-tight">
            Get 5–10 sample leads from your market — free
          </h2>
          <div className="mt-8"><SampleLeadsForm markets={markets} /></div>
        </div>
      </section>

      <section className="mx-auto max-w-4xl px-4 py-16 text-center sm:px-6">
        <h2 className="text-2xl font-bold tracking-tight">{content.ctaHeading}</h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">{content.ctaBody}</p>
        <Button asChild size="lg" className="mt-6 bg-orange-500 px-8 text-white hover:bg-orange-600">
          <Link href="/signup">Start Free</Link>
        </Button>
      </section>
    </>
  );
}
```

- [ ] Create the three route files — identical shape, keyed content. Example `apps/web/app/(marketing)/fire-protection-leads/page.tsx`:

```tsx
import type { Metadata } from "next";
import { getMarkets } from "@/lib/api";
import { buildMetadata } from "@/lib/seo";
import { CategoryLander, CATEGORY_LANDERS } from "@/components/marketing/category-lander";

const content = CATEGORY_LANDERS["fire-protection"];

export const metadata: Metadata = buildMetadata({
  title: content.metaTitle,
  description: content.metaDescription,
  path: content.path,
});

export default async function FireProtectionLeadsPage() {
  return <CategoryLander content={content} markets={await getMarkets()} />;
}
```

Repeat for `fire-sprinkler-leads/page.tsx` (key `"fire-sprinkler"`) and `fire-alarm-leads/page.tsx` (key `"fire-alarm"`).

- [ ] Visual verify: open all three routes. Check each has distinct headline/copy, three pain cards, how-it-finds paragraphs, working sample form, CTA band. View source on each: unique `<title>`, unique meta description, correct canonical.
- [ ] `pnpm vitest run __tests__/marketing` — green. `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add fire protection category landing pages`

---

### Task 10: `/locations` index + `/locations/[state]/[city]` market pages

Data-driven SEO pages (PRD §23–24). Index lists active markets from `getMarkets()`. City pages are statically generated **only** for those markets (`generateStaticParams` + `dynamicParams = false` + explicit `notFound()`), fetch `getMarketStats(slug)`, and show real aggregates, an honest freshness line, clearly-labeled anonymized example leads, CTA, and FAQ with FAQPage JSON-LD.

**Files:**
- Create: `apps/web/components/marketing/category-labels.ts`
- Create: `apps/web/components/marketing/freshness-line.tsx`
- Create: `apps/web/app/(marketing)/locations/page.tsx`
- Create: `apps/web/app/(marketing)/locations/[state]/[city]/page.tsx`
- Create: `apps/web/__tests__/marketing/freshness-line.test.tsx`

**Interfaces:**
- Consumes: `getMarkets(): Promise<Market[]>`, `getMarketStats(slug: string): Promise<MarketStats>` from `@/lib/api`; `marketToLocationParams`, `marketLocationPath`, `findMarketByLocationParams`, `stateDisplayName` (Task 3); `buildMetadata`, `jsonLd` (Task 2).
- Produces: `/locations`, `/locations/[state]/[city]` routes; `CATEGORY_LABELS: Record<FireCategory, string>`; `FreshnessLine` + `relativeUpdatedLabel` (sitemap and WS5 smoke tests rely on these routes existing).

**Steps:**

- [ ] Create `apps/web/components/marketing/category-labels.ts`:

```typescript
import type { FireCategory } from "@permittorch/types";

export const CATEGORY_LABELS: Record<FireCategory, string> = {
  FIRE_SPRINKLER: "Fire sprinkler",
  FIRE_ALARM: "Fire alarm",
  FIRE_SUPPRESSION: "Fire suppression",
  KITCHEN_SUPPRESSION: "Kitchen suppression",
  FIRE_INSPECTION: "Fire inspection",
  VIOLATION_CORRECTION: "Violation correction",
  GENERAL_FIRE_PROTECTION: "General fire protection",
};
```

- [ ] TDD the freshness label. Failing test `apps/web/__tests__/marketing/freshness-line.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";
import { relativeUpdatedLabel } from "@/components/marketing/freshness-line";

const NOW = new Date("2026-08-19T12:00:00Z");

describe("relativeUpdatedLabel", () => {
  it("is honest when there is no update time", () => {
    expect(relativeUpdatedLabel(null, NOW)).toBe("Awaiting first data update");
  });
  it("reports minutes, hours, and days", () => {
    expect(relativeUpdatedLabel("2026-08-19T11:55:00Z", NOW)).toBe("Updated 5 minutes ago");
    expect(relativeUpdatedLabel("2026-08-19T06:00:00Z", NOW)).toBe("Updated 6 hours ago");
    expect(relativeUpdatedLabel("2026-08-16T12:00:00Z", NOW)).toBe("Updated 3 days ago");
  });
  it("uses singular units", () => {
    expect(relativeUpdatedLabel("2026-08-19T11:00:00Z", NOW)).toBe("Updated 1 hour ago");
    expect(relativeUpdatedLabel("2026-08-18T12:00:00Z", NOW)).toBe("Updated 1 day ago");
  });
});
```

- [ ] Implement `apps/web/components/marketing/freshness-line.tsx`:

```tsx
export function relativeUpdatedLabel(lastUpdatedAt: string | null, now: Date = new Date()): string {
  if (!lastUpdatedAt) return "Awaiting first data update";
  const ms = now.getTime() - new Date(lastUpdatedAt).getTime();
  const minutes = Math.max(1, Math.floor(ms / 60_000));
  const unit = (n: number, u: string) => `Updated ${n} ${u}${n === 1 ? "" : "s"} ago`;
  if (minutes < 60) return unit(minutes, "minute");
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return unit(hours, "hour");
  return unit(Math.floor(hours / 24), "day");
}

export function FreshnessLine({ lastUpdatedAt }: { lastUpdatedAt: string | null }) {
  return (
    <p className="inline-flex items-center gap-2 text-sm text-neutral-500">
      <span aria-hidden="true"
        className={`h-2 w-2 rounded-full ${lastUpdatedAt ? "bg-emerald-500" : "bg-neutral-300"}`} />
      {relativeUpdatedLabel(lastUpdatedAt)}
    </p>
  );
}
```

- [ ] Create `apps/web/app/(marketing)/locations/page.tsx`:

```tsx
import Link from "next/link";
import type { Metadata } from "next";
import { getMarkets } from "@/lib/api";
import { buildMetadata } from "@/lib/seo";
import { marketLocationPath, stateDisplayName } from "@/components/marketing/market-slug";

export const metadata: Metadata = buildMetadata({
  title: "Markets We Cover — PermitTorch",
  description:
    "Cities where PermitTorch actively monitors permit and inspection records for fire protection leads. We only list markets with live data coverage.",
  path: "/locations",
});

export default async function LocationsPage() {
  const markets = await getMarkets();
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">Markets we cover</h1>
      <p className="mt-4 max-w-2xl text-lg text-neutral-600">
        Every market below has live permit and inspection coverage today. We add cities as
        real data comes online — never before.
      </p>
      <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {markets.map((m) => (
          <Link key={m.slug} href={marketLocationPath(m)}
            className="group rounded-2xl border border-neutral-200 p-6 transition-shadow hover:shadow-md">
            <h2 className="text-xl font-semibold group-hover:text-orange-600">
              {m.city}, {m.state}
            </h2>
            <p className="mt-1 text-sm text-neutral-500">
              Fire protection leads in {m.city}, {stateDisplayName(m.state)}
            </p>
            <span className="mt-4 inline-block text-sm font-medium text-orange-600">
              View market →
            </span>
          </Link>
        ))}
      </div>
      <p className="mt-12 text-sm text-neutral-500">
        Don&apos;t see your city? New markets open regularly — <Link href="/signup" className="text-orange-600 underline">create a free account</Link> and we&apos;ll notify you when yours goes live.
      </p>
    </div>
  );
}
```

- [ ] Create `apps/web/app/(marketing)/locations/[state]/[city]/page.tsx` (Next 15: `params` is a Promise):

```tsx
import Link from "next/link";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getMarkets, getMarketStats } from "@/lib/api";
import { buildMetadata, jsonLd, SITE_URL } from "@/lib/seo";
import { Button } from "@/components/ui/button";
import { CATEGORY_LABELS } from "@/components/marketing/category-labels";
import { FreshnessLine } from "@/components/marketing/freshness-line";
import {
  findMarketByLocationParams, marketLocationPath, marketToLocationParams, stateDisplayName,
} from "@/components/marketing/market-slug";
import {
  Accordion, AccordionContent, AccordionItem, AccordionTrigger,
} from "@/components/ui/accordion";

// PRD §24: pages exist ONLY for markets returned by getMarkets(). No fabricated params.
export const dynamicParams = false;

interface Props { params: Promise<{ state: string; city: string }> }

export async function generateStaticParams() {
  const markets = await getMarkets();
  return markets.map((m) => marketToLocationParams(m));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { state, city } = await params;
  const market = findMarketByLocationParams(await getMarkets(), state, city);
  if (!market) return {};
  const stateName = stateDisplayName(market.state);
  return buildMetadata({
    title: `Fire Protection Leads in ${market.city}, ${stateName} — PermitTorch`,
    description: `Live fire protection lead data for ${market.city}, ${stateName}: sprinkler, alarm, and suppression opportunities from public permit records, updated daily.`,
    path: marketLocationPath(market),
  });
}

// Static, anonymized illustrations — clearly labeled on the page. Not live records.
const EXAMPLE_LEADS = [
  { score: 92, title: "Fire sprinkler system — new commercial build", detail: "New multi-story commercial construction, seven-figure valuation, no fire contractor listed on the permit.", meta: "Filed this week · Address available to subscribers" },
  { score: 86, title: "Fire alarm system — healthcare tenant upfit", detail: "Multi-floor tenant improvement with alarm scope in the permit description.", meta: "Filed this week · Address available to subscribers" },
  { score: 78, title: "Kitchen suppression — new restaurant", detail: "Restaurant build-out in a high-traffic retail corridor; hood system required.", meta: "Filed this month · Address available to subscribers" },
];

export default async function MarketPage({ params }: Props) {
  const { state, city } = await params;
  const market = findMarketByLocationParams(await getMarkets(), state, city);
  if (!market) notFound();

  const stats = await getMarketStats(market.slug);
  const stateName = stateDisplayName(market.state);
  const categories = (Object.entries(stats.byCategory) as [keyof typeof CATEGORY_LABELS, number][])
    .filter(([, n]) => n > 0)
    .sort(([, a], [, b]) => b - a);

  const faq = [
    { q: `Where does the ${market.city} data come from?`, a: `From publicly available permit and inspection records published by government jurisdictions in the ${market.city} area. Every lead links to the official source record.` },
    { q: "How fresh is the data?", a: "Sources are checked on a daily cycle and this page shows exactly when data was last updated. We never present stale data as current — if a source falls behind, we say so." },
    { q: "Are the example leads real?", a: "The examples above are illustrative and anonymized. Subscribers see full records: address, permit number, filing date, estimated value, score breakdown, and the official source link." },
    { q: `What does PermitTorch cost in ${market.city}?`, a: "Plans start at $49/month for one market, and every account starts with a 7-day Pro trial. See the pricing page for details." },
  ];

  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <nav className="text-sm text-neutral-500">
        <Link href="/locations" className="hover:text-neutral-900">Markets</Link>
        <span className="mx-2">/</span>
        <span>{market.city}, {market.state}</span>
      </nav>

      <h1 className="mt-4 text-4xl font-bold tracking-tight">
        Fire Protection Leads in {market.city}, {stateName}
      </h1>
      <div className="mt-4"><FreshnessLine lastUpdatedAt={stats.lastUpdatedAt} /></div>

      <section className="mt-10 rounded-2xl border border-neutral-200 bg-neutral-50 p-8">
        <p className="text-lg">
          PermitTorch identified{" "}
          <strong className="text-orange-600">
            {stats.totalLast30Days} fire-related opportunities
          </strong>{" "}
          in {market.city} during the last 30 days:
        </p>
        <dl className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          {categories.map(([cat, count]) => (
            <div key={cat} className="rounded-xl bg-white p-4 shadow-sm">
              <dt className="text-sm text-neutral-500">{CATEGORY_LABELS[cat]}</dt>
              <dd className="mt-1 text-2xl font-bold">{count}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section className="mt-16">
        <div className="flex items-baseline justify-between">
          <h2 className="text-2xl font-bold tracking-tight">What leads look like</h2>
          <span className="text-xs font-medium uppercase tracking-wide text-neutral-400">
            Illustrative examples — anonymized
          </span>
        </div>
        <div className="mt-6 grid gap-6 md:grid-cols-3">
          {EXAMPLE_LEADS.map((l) => (
            <div key={l.title} className="rounded-2xl border border-neutral-200 p-6">
              <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-orange-500 font-bold text-white">
                {l.score}
              </span>
              <h3 className="mt-4 font-semibold">{l.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{l.detail}</p>
              <p className="mt-3 text-xs text-neutral-400">{l.meta}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="mt-16 rounded-2xl bg-orange-50 p-10 text-center">
        <h2 className="text-2xl font-bold tracking-tight">
          Get {market.city} opportunities every morning
        </h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">
          Start free, pick {market.city} as your market, and see tomorrow&apos;s permits scored and sorted.
        </p>
        <Button asChild size="lg" className="mt-6 bg-orange-500 px-8 text-white hover:bg-orange-600">
          <Link href="/signup">Find Leads in {market.city}</Link>
        </Button>
      </section>

      <section className="mx-auto mt-16 max-w-2xl">
        <h2 className="text-2xl font-bold tracking-tight">Questions about {market.city} coverage</h2>
        <Accordion type="single" collapsible className="mt-6">
          {faq.map((f, i) => (
            <AccordionItem key={f.q} value={`faq-${i}`}>
              <AccordionTrigger className="text-left">{f.q}</AccordionTrigger>
              <AccordionContent className="text-neutral-600">{f.a}</AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd({
        "@context": "https://schema.org",
        "@type": "FAQPage",
        mainEntity: faq.map((f) => ({
          "@type": "Question", name: f.q,
          acceptedAnswer: { "@type": "Answer", text: f.a },
        })),
      })} />
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd({
        "@context": "https://schema.org",
        "@type": "BreadcrumbList",
        itemListElement: [
          { "@type": "ListItem", position: 1, name: "Markets", item: `${SITE_URL}/locations` },
          { "@type": "ListItem", position: 2, name: `${market.city}, ${market.state}`, item: `${SITE_URL}${marketLocationPath(market)}` },
        ],
      })} />
    </div>
  );
}
```

- [ ] `pnpm vitest run __tests__/marketing` — freshness tests green.
- [ ] Visual verify with `NEXT_PUBLIC_API_MOCK=1 pnpm dev`:
  - `/locations` shows exactly Houston, Dallas, Austin cards linking to `/locations/texas/houston` etc.
  - `/locations/texas/houston` shows the H1 with full state name, the 30-day total (137 with fixtures), category tiles sorted descending, green-dot freshness line, three example cards with the "Illustrative examples — anonymized" label, CTA, FAQ accordion.
  - `/locations/texas/el-paso` and `/locations/oklahoma/tulsa` return 404.
  - Page source: unique title/description/canonical + two JSON-LD scripts.
- [ ] `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add locations index and data-driven market city pages`

---

### Task 11: `/blog` — typed posts, index, and post pages

File-based posts as typed TS objects. Three real posts (full text below — use it verbatim), each 300+ words, enforced by test. Index lists posts newest-first; `/blog/[slug]` renders with `buildMetadata` + Article JSON-LD; unknown slugs 404.

**Files:**
- Create: `apps/web/components/marketing/blog-posts.ts`
- Create: `apps/web/app/(marketing)/blog/page.tsx`
- Create: `apps/web/app/(marketing)/blog/[slug]/page.tsx`
- Create: `apps/web/__tests__/marketing/blog-posts.test.ts`

**Interfaces:**
- Consumes: `buildMetadata`, `jsonLd`, `SITE_URL` (Task 2).
- Produces: `BlogPost` interface, `blogPosts: BlogPost[]`, `getBlogPost(slug)` — sitemap (Task 12) consumes `blogPosts`.

**Steps:**

- [ ] Write the failing shape test `apps/web/__tests__/marketing/blog-posts.test.ts`:

```typescript
import { describe, expect, it } from "vitest";
import { blogPosts, getBlogPost } from "@/components/marketing/blog-posts";

describe("blogPosts", () => {
  it("contains the three launch posts with unique slugs", () => {
    expect(blogPosts.map((p) => p.slug).sort()).toEqual([
      "how-fire-sprinkler-contractors-find-leads",
      "how-to-find-commercial-fire-protection-projects",
      "using-building-permits-for-lead-generation",
    ]);
  });

  it("every post has title, description, and a valid ISO date", () => {
    for (const p of blogPosts) {
      expect(p.title.length).toBeGreaterThan(10);
      expect(p.description.length).toBeGreaterThan(40);
      expect(Number.isNaN(Date.parse(p.publishedAt))).toBe(false);
    }
  });

  it("every post body is at least 300 words", () => {
    for (const p of blogPosts) {
      const words = p.sections
        .flatMap((s) => s.body)
        .join(" ")
        .split(/\s+/)
        .filter(Boolean).length;
      expect(words, `${p.slug} has ${words} words`).toBeGreaterThanOrEqual(300);
    }
  });

  it("getBlogPost resolves known slugs and returns undefined otherwise", () => {
    expect(getBlogPost("using-building-permits-for-lead-generation")?.title).toBeTruthy();
    expect(getBlogPost("nope")).toBeUndefined();
  });
});
```

- [ ] Implement `apps/web/components/marketing/blog-posts.ts` with this exact content:

```typescript
export interface BlogSection { heading?: string; body: string[] }
export interface BlogPost {
  slug: string;
  title: string;
  description: string;
  publishedAt: string; // ISO date
  sections: BlogSection[];
}

export const blogPosts: BlogPost[] = [
  {
    slug: "how-fire-sprinkler-contractors-find-leads",
    title: "How Fire Sprinkler Contractors Find Leads",
    description:
      "The four ways sprinkler contractors find work today, why most of them start too late, and how permit records move you to the front of the line.",
    publishedAt: "2026-08-19",
    sections: [
      {
        body: [
          "Most fire sprinkler contractors find work the same four ways: relationships with general contractors, bid boards, referrals, and repeat customers. All four can keep a shop busy. None of them puts you first in line — and in sprinkler work, the order of the line decides your margin.",
        ],
      },
      {
        heading: "The problem with waiting for the bid",
        body: [
          "By the time a sprinkler package appears on a bid board, the general contractor has usually already collected numbers from the shops they know. If you find the job there, you are the fourth bid — invited to lose, or to win on price alone. The same is true of relationship work: it is only as wide as your rolodex, and every GC you know also knows your competitors.",
          "The profitable moment is earlier, before the package is priced, when you can shape scope and be the contractor the GC calls instead of the one who calls last.",
        ],
      },
      {
        heading: "Permits are the earlier signal",
        body: [
          "Before ground breaks on almost any commercial project, someone files a building permit. That permit is a public record, and it is remarkably specific: address, project description, estimated value, filing date, and often the parties involved.",
          "For a sprinkler contractor, three permit patterns matter most. New commercial construction above the code threshold will need a sprinkler system — full stop. Tenant improvements frequently trigger head relocations, coverage changes, or full retrofits. And any permit whose description mentions sprinkler scope directly is a project actively shopping for your trade.",
        ],
      },
      {
        heading: "What to look for in the record",
        body: [
          "Recency first: a permit filed this week is a conversation; a permit filed four months ago is a spreadsheet entry. Then valuation and square footage, which sort the seven-figure builds from the closet remodels. Finally — and this is the one most people miss — look at who is listed on the permit. If no fire contractor appears yet, the package likely has not been awarded. That is your call to make.",
        ],
      },
      {
        heading: "Do it by hand, or let software watch",
        body: [
          "You can build this habit manually: bookmark every permit portal in your market, check them weekly, and keep a spreadsheet. It works, and plenty of shops do it — it just costs hours every week and misses everything filed between checks.",
          "Or let software do the watching. PermitTorch monitors the permit sources in your market daily, filters to fire-related work, and scores each opportunity by value, scope, timing, and whether a fire contractor is already attached. Either way, the contractors who read permits first get the first conversation — and the first conversation wins more work than the fourth bid ever will.",
        ],
      },
    ],
  },
  {
    slug: "using-building-permits-for-lead-generation",
    title: "Using Building Permits for Lead Generation",
    description:
      "Building permits are the most underused lead source in the trades: public, factual, and early. Here is how to turn them into a repeatable pipeline.",
    publishedAt: "2026-08-12",
    sections: [
      {
        body: [
          "Every commercial construction project in America leaves a paper trail, and it starts with a building permit. Permits are public records — anyone can read them — yet most contractors never do. That makes them the most underused lead source in the trades: factual, early, and free of the resale problem that plagues purchased lead lists.",
        ],
      },
      {
        heading: "What a permit actually tells you",
        body: [
          "A typical commercial permit record includes the project address, a permit type, a scope description, an estimated valuation, filing and issue dates, current status, and frequently the owner, applicant, and any contractors already attached. Read together, those fields answer the questions a salesperson actually has: What is being built? How big is it? How new is this? And has anyone in my trade already won it?",
        ],
      },
      {
        heading: "Why permits beat purchased lead lists",
        body: [
          "A purchased lead has usually been sold to five of your competitors before it reaches you, and it decays fast. A permit is different: it is a primary source. Nobody edited it for marketing, nobody resold it, and it appears at the earliest public moment of the project — often weeks before bid boards or word of mouth catch up. The contractor who reads it first has real first-mover advantage.",
        ],
      },
      {
        heading: "The catch: it is a grind",
        body: [
          "Here is why almost nobody does this. Every jurisdiction runs its own portal with its own search, its own field names, and its own update schedule. A single metro area can span a dozen sources. Checking them all weekly is hours of clicking, and most of what you will read is irrelevant to your trade — fences, water heaters, reroofs.",
        ],
      },
      {
        heading: "Make it repeatable",
        body: [
          "Whether you do it by hand or with software, the workflow is the same. Filter to your trade with keywords in the permit type and description. Sort by filing date — recency is everything, because the first call usually gets the meeting. Weight by valuation and square footage so the big jobs surface. And flag permits where no contractor in your trade is listed yet, because those projects have not awarded your scope.",
          "That is exactly the pipeline PermitTorch automates for fire protection: every source in your market checked daily, filtered to fire work, scored 0–100 with reasons you can read. However you run it, start reading permits — your next customer already filed one.",
        ],
      },
    ],
  },
  {
    slug: "how-to-find-commercial-fire-protection-projects",
    title: "How to Find Commercial Fire Protection Projects",
    description:
      "The five public-record signals that generate commercial fire protection work — new builds, tenant improvements, restaurants, warehouses, and failed inspections.",
    publishedAt: "2026-08-05",
    sections: [
      {
        body: [
          "Commercial fire protection work does not appear out of nowhere. It is generated by a handful of predictable events — and nearly every one of them shows up in public records before any bid is issued. If you know which signals to watch, you can build a project pipeline out of information anyone can access.",
        ],
      },
      {
        heading: "Signal 1: New commercial construction",
        body: [
          "Any new commercial building over the code threshold needs sprinklers, alarms, or both. New-construction permits carry the clearest budget signals in the public record: estimated valuation and square footage. A seven-figure new build filed this week is the single strongest fire protection lead that exists.",
        ],
      },
      {
        heading: "Signal 2: Tenant improvements and occupancy changes",
        body: [
          "TI permits are smaller but constant. A new tenant means reconfigured walls, which means relocated sprinkler heads and modified alarm coverage — and an occupancy change can trigger entirely new system requirements. Because TIs file year-round in every submarket, they are the steadiest source of mid-size fire work.",
        ],
      },
      {
        heading: "Signal 3: Restaurants and commercial kitchens",
        body: [
          "Every new restaurant, ghost kitchen, and cafeteria needs a kitchen hood suppression system, and health-and-fire review makes these projects easy to spot in permit descriptions. Restaurant build-outs also move fast, so recency matters more here than anywhere else.",
        ],
      },
      {
        heading: "Signal 4: Failed inspections and violations",
        body: [
          "A failed fire inspection or a code violation is a customer who has to buy — the fire marshal said so. These records are the most neglected signal in the industry because they sit in inspection systems separate from building permits. The contractor who monitors them gets warm calls with a deadline attached.",
        ],
      },
      {
        heading: "Prioritize, then call",
        body: [
          "Not every signal deserves a phone call. Rank what you find: project value and square footage set the size of the prize, filing recency sets the urgency, and a permit with no fire contractor listed means the scope is probably still open. Work the list top-down every morning.",
          "That ranking is precisely what PermitTorch computes — a 0–100 score per opportunity, with every contributing signal spelled out. But scored or not, the projects are already sitting in the public record. The only question is which contractor reads it first.",
        ],
      },
    ],
  },
];

export function getBlogPost(slug: string): BlogPost | undefined {
  return blogPosts.find((p) => p.slug === slug);
}
```

- [ ] Run `pnpm vitest run __tests__/marketing` — blog tests green (word counts must pass; if a post is under 300, expand its body — do not weaken the test).
- [ ] Create `apps/web/app/(marketing)/blog/page.tsx`:

```tsx
import Link from "next/link";
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { blogPosts } from "@/components/marketing/blog-posts";

export const metadata: Metadata = buildMetadata({
  title: "Resources for Fire Protection Contractors — PermitTorch Blog",
  description:
    "Practical guides on finding fire protection work through permit data: lead generation, prospecting, and reading public records like a salesperson.",
  path: "/blog",
});

const dateFmt = new Intl.DateTimeFormat("en-US", {
  year: "numeric", month: "long", day: "numeric", timeZone: "UTC",
});

export default function BlogIndexPage() {
  const posts = [...blogPosts].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt));
  return (
    <div className="mx-auto max-w-3xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">Resources</h1>
      <p className="mt-4 text-lg text-neutral-600">
        Practical guides on turning public permit data into fire protection work.
      </p>
      <div className="mt-12 space-y-10">
        {posts.map((p) => (
          <article key={p.slug}>
            <p className="text-sm text-neutral-400">{dateFmt.format(new Date(p.publishedAt))}</p>
            <h2 className="mt-1 text-2xl font-semibold">
              <Link href={`/blog/${p.slug}`} className="hover:text-orange-600">{p.title}</Link>
            </h2>
            <p className="mt-2 leading-relaxed text-neutral-600">{p.description}</p>
            <Link href={`/blog/${p.slug}`} className="mt-2 inline-block text-sm font-medium text-orange-600">
              Read the guide →
            </Link>
          </article>
        ))}
      </div>
    </div>
  );
}
```

- [ ] Create `apps/web/app/(marketing)/blog/[slug]/page.tsx`:

```tsx
import Link from "next/link";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { buildMetadata, jsonLd, SITE_URL } from "@/lib/seo";
import { Button } from "@/components/ui/button";
import { blogPosts, getBlogPost } from "@/components/marketing/blog-posts";

export const dynamicParams = false;

interface Props { params: Promise<{ slug: string }> }

export function generateStaticParams() {
  return blogPosts.map((p) => ({ slug: p.slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const post = getBlogPost((await params).slug);
  if (!post) return {};
  return buildMetadata({
    title: `${post.title} — PermitTorch`,
    description: post.description,
    path: `/blog/${post.slug}`,
  });
}

export default async function BlogPostPage({ params }: Props) {
  const post = getBlogPost((await params).slug);
  if (!post) notFound();

  return (
    <article className="mx-auto max-w-3xl px-4 py-20 sm:px-6">
      <Link href="/blog" className="text-sm text-neutral-500 hover:text-neutral-900">← All resources</Link>
      <h1 className="mt-4 text-4xl font-bold tracking-tight">{post.title}</h1>
      <p className="mt-3 text-sm text-neutral-400">
        {new Intl.DateTimeFormat("en-US", { year: "numeric", month: "long", day: "numeric", timeZone: "UTC" })
          .format(new Date(post.publishedAt))} · PermitTorch
      </p>
      <div className="mt-10 space-y-8">
        {post.sections.map((s, i) => (
          <section key={s.heading ?? `s-${i}`}>
            {s.heading && <h2 className="text-2xl font-semibold">{s.heading}</h2>}
            {s.body.map((p) => (
              <p key={p.slice(0, 32)} className="mt-4 leading-relaxed text-neutral-700">{p}</p>
            ))}
          </section>
        ))}
      </div>

      <aside className="mt-16 rounded-2xl bg-orange-50 p-8 text-center">
        <h2 className="text-xl font-bold">Get scored fire protection leads every morning</h2>
        <p className="mx-auto mt-2 max-w-md text-sm text-neutral-600">
          PermitTorch does everything in this guide automatically — monitored sources, fire-only
          classification, explainable 0–100 scores.
        </p>
        <Button asChild className="mt-4 bg-orange-500 text-white hover:bg-orange-600">
          <Link href="/signup">Start Free</Link>
        </Button>
      </aside>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd({
        "@context": "https://schema.org",
        "@type": "Article",
        headline: post.title,
        description: post.description,
        datePublished: post.publishedAt,
        mainEntityOfPage: `${SITE_URL}/blog/${post.slug}`,
        author: { "@type": "Organization", name: "PermitTorch", url: SITE_URL },
        publisher: { "@type": "Organization", name: "PermitTorch", url: SITE_URL },
      })} />
    </article>
  );
}
```

- [ ] Visual verify: `/blog` lists three posts newest-first with dates; each post page renders headings/paragraphs, the upsell aside, and Article JSON-LD in source; `/blog/made-up-slug` 404s.
- [ ] `pnpm vitest run __tests__/marketing` — green. `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add blog with three launch posts`

---

### Task 12: `app/sitemap.ts` + `app/robots.ts`

**Files:**
- Create: `apps/web/app/sitemap.ts`
- Create: `apps/web/app/robots.ts`
- Create: `apps/web/__tests__/marketing/sitemap.test.ts`

**Interfaces:**
- Consumes: `getMarkets()` from `@/lib/api`; `marketLocationPath` (Task 3); `blogPosts` (Task 11); `SITE_URL` (Task 2); `MetadataRoute` from `next`.
- Produces: `/sitemap.xml`, `/robots.txt`.

**Steps:**

- [ ] Write the failing test `apps/web/__tests__/marketing/sitemap.test.ts` (mock the api module so the test is hermetic):

```typescript
import { describe, expect, it, vi } from "vitest";
import { mockMarkets } from "@/lib/fixtures/markets";

vi.mock("@/lib/api", () => ({ getMarkets: vi.fn().mockResolvedValue(mockMarkets) }));

import sitemap from "@/app/sitemap";
import robots from "@/app/robots";

describe("sitemap", () => {
  it("includes all static marketing routes, market pages, and blog posts", async () => {
    const urls = (await sitemap()).map((e) => e.url);
    for (const path of [
      "/", "/pricing", "/how-it-works", "/fire-protection-leads", "/fire-sprinkler-leads",
      "/fire-alarm-leads", "/locations", "/blog", "/terms", "/privacy",
      "/locations/texas/houston", "/locations/texas/dallas", "/locations/texas/austin",
      "/blog/how-fire-sprinkler-contractors-find-leads",
      "/blog/using-building-permits-for-lead-generation",
      "/blog/how-to-find-commercial-fire-protection-projects",
    ]) {
      expect(urls).toContain(new URL(path, "https://permittorch.com").toString());
    }
  });

  it("contains no /app routes", async () => {
    const urls = (await sitemap()).map((e) => e.url);
    expect(urls.some((u) => u.includes("/app"))).toBe(false);
  });
});

describe("robots", () => {
  it("allows everything except the app, and references the sitemap", () => {
    const r = robots();
    const rules = Array.isArray(r.rules) ? r.rules[0] : r.rules;
    expect(rules?.allow).toBe("/");
    expect(rules?.disallow).toContain("/app/");
    expect(r.sitemap).toBe("https://permittorch.com/sitemap.xml");
  });
});
```

- [ ] Implement `apps/web/app/sitemap.ts`:

```typescript
import type { MetadataRoute } from "next";
import { getMarkets } from "@/lib/api";
import { SITE_URL } from "@/lib/seo";
import { marketLocationPath } from "@/components/marketing/market-slug";
import { blogPosts } from "@/components/marketing/blog-posts";

const STATIC_ROUTES: { path: string; priority: number }[] = [
  { path: "/", priority: 1.0 },
  { path: "/pricing", priority: 0.9 },
  { path: "/how-it-works", priority: 0.8 },
  { path: "/fire-protection-leads", priority: 0.9 },
  { path: "/fire-sprinkler-leads", priority: 0.9 },
  { path: "/fire-alarm-leads", priority: 0.9 },
  { path: "/locations", priority: 0.8 },
  { path: "/blog", priority: 0.6 },
  { path: "/terms", priority: 0.2 },
  { path: "/privacy", priority: 0.2 },
];

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const markets = await getMarkets();
  const url = (path: string) => new URL(path, SITE_URL).toString();
  return [
    ...STATIC_ROUTES.map((r) => ({
      url: url(r.path), priority: r.priority, changeFrequency: "weekly" as const,
    })),
    ...markets.map((m) => ({
      url: url(marketLocationPath(m)), priority: 0.8, changeFrequency: "daily" as const,
    })),
    ...blogPosts.map((p) => ({
      url: url(`/blog/${p.slug}`), priority: 0.5,
      changeFrequency: "monthly" as const, lastModified: p.publishedAt,
    })),
  ];
}
```

- [ ] Implement `apps/web/app/robots.ts`:

```typescript
import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/seo";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: "*", allow: "/", disallow: ["/app/", "/app"] }],
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
```

- [ ] `pnpm vitest run __tests__/marketing` — green.
- [ ] Visual verify: `curl -s localhost:3000/sitemap.xml` lists all 16 URLs; `curl -s localhost:3000/robots.txt` shows `Allow: /`, `Disallow: /app/`, and the sitemap line.
- [ ] `pnpm tsc --noEmit` — clean.
- [ ] Commit: `Add sitemap and robots for marketing routes`

---

### Task 13: Final verification pass

**Files:** none new (fixes only, within owned paths).

**Interfaces:** n/a — verification gate before WS5 merge.

**Steps:**

- [ ] Full workstream test run: `pnpm vitest run __tests__/marketing` (from `apps/web/`) — all suites green (fixtures, seo, market-slug, site-nav, sample-leads-form, pricing-tiers, freshness-line, blog-posts, sitemap).
- [ ] `pnpm tsc --noEmit` — zero errors.
- [ ] Production build proof: `NEXT_PUBLIC_API_MOCK=1 pnpm build`. Confirm the build output shows `/locations/[state]/[city]` prerendered for exactly `texas/houston`, `texas/dallas`, `texas/austin` (SSG ●), and `/blog/[slug]` for exactly the three post slugs. Any extra or missing static params is a bug.
- [ ] Full visual sweep on the production build (`NEXT_PUBLIC_API_MOCK=1 pnpm start`): `/`, `/pricing`, `/how-it-works`, all three category landers, `/locations`, `/locations/texas/houston`, `/blog`, one blog post, `/terms`, `/privacy` — desktop and ~375px width; no horizontal scroll anywhere; nav/footer on every page; sample form success state works on homepage and a lander.
- [ ] SEO spot-audit against PRD §50: every page has unique title + unique description + canonical + og tags (`curl -s <page> | grep -E "canonical|og:title|description"` across routes — no two pages identical).
- [ ] `git status` — working tree clean except owned paths; `git log --oneline main..ws/marketing` — messages imperative, no task refs, no co-author trailers.
- [ ] Fix anything found (within owned paths only), re-run the three gates, commit fixes with descriptive messages (e.g. `Fix mobile overflow on pricing cards`).
- [ ] Do NOT merge — hand off per master plan (WS3 merges after WS2, before WS4; WS5 flips `NEXT_PUBLIC_API_MOCK` off and runs Playwright).

---

## Contract gaps resolved by this plan (flag to coordinator if wrong)

1. **Slug mapping home:** the master plan defines no location for the `houston-tx` ↔ `/locations/texas/houston` mapping; it lives in `components/marketing/market-slug.ts` (WS3-owned) and resolves against `getMarkets()` rather than parsing slugs, so unknown URLs 404 per PRD §24.
2. **Fixture export names:** `mockMarkets` / `mockMarketStats` as directed; Task 1 includes a read-only check that WS0's `lib/api.ts` mock branch matches, with an explicit stop-and-escalate if it doesn't.
3. **`jsonLd` shape:** contracts don't define it; standardized as `(obj) => ({ __html })` for `dangerouslySetInnerHTML`, with `<` escaped.
4. **shadcn availability:** WS0 owns dependency installation; if a primitive (notably Accordion) is missing, build a minimal local equivalent in `components/marketing/` — never touch `package.json`.
5. **`/login`, `/signup`, root `app/page.tsx`:** WS3 only links to `/login` and `/signup` (WS0/WS4 own auth routes); Task 6 removes WS0's deliberate root `app/page.tsx` placeholder (`git rm`, explicitly authorized) so `(marketing)/page.tsx` can own `/`.





