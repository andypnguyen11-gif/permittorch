import { describe, expect, it, vi } from "vitest";
import type { Metadata } from "next";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

vi.mock("@/lib/api", () => ({
  getMarkets: vi.fn(async () => mockMarkets),
  getMarketStats: vi.fn(async (slug: string) => mockMarketStats[slug]),
  getAllMarketStats: vi.fn(async () => Object.values(mockMarketStats)),
}));

import { metadata as home } from "@/app/(marketing)/page";
import { metadata as pricing } from "@/app/(marketing)/pricing/page";
import { metadata as howItWorks } from "@/app/(marketing)/how-it-works/page";
import { metadata as fireProtection } from "@/app/(marketing)/fire-protection-leads/page";
import { metadata as fireSprinkler } from "@/app/(marketing)/fire-sprinkler-leads/page";
import { metadata as fireAlarm } from "@/app/(marketing)/fire-alarm-leads/page";
import { metadata as locations } from "@/app/(marketing)/locations/page";
import { metadata as blog } from "@/app/(marketing)/blog/page";
import { metadata as terms } from "@/app/(marketing)/terms/page";
import { metadata as privacy } from "@/app/(marketing)/privacy/page";
import { generateMetadata as marketMetadata } from "@/app/(marketing)/locations/[state]/[city]/page";
import { generateMetadata as postMetadata } from "@/app/(marketing)/blog/[slug]/page";
import { marketToLocationParams } from "@/components/marketing/market-slug";
import { blogPosts } from "@/components/marketing/blog-posts";

// Mirrors the frozen root layout: title.template = "%s | PermitTorch".
function renderedTitle(md: Metadata): string {
  const t = md.title;
  if (typeof t === "string") return `${t} | PermitTorch`;
  if (t && typeof t === "object" && "absolute" in t) return t.absolute;
  throw new Error(`Unexpected title shape: ${JSON.stringify(t)}`);
}

async function allPageMetadata(): Promise<[string, Metadata][]> {
  const entries: [string, Metadata][] = [
    ["/", home], ["/pricing", pricing], ["/how-it-works", howItWorks],
    ["/fire-protection-leads", fireProtection], ["/fire-sprinkler-leads", fireSprinkler],
    ["/fire-alarm-leads", fireAlarm], ["/locations", locations], ["/blog", blog],
    ["/terms", terms], ["/privacy", privacy],
  ];
  for (const m of mockMarkets) {
    const params = marketToLocationParams(m);
    entries.push([`market:${m.slug}`, await marketMetadata({ params: Promise.resolve(params) })]);
  }
  for (const p of blogPosts) {
    entries.push([`post:${p.slug}`, await postMetadata({ params: Promise.resolve({ slug: p.slug }) })]);
  }
  return entries;
}

describe("marketing page metadata", () => {
  it("never repeats the brand in a rendered <title>", async () => {
    for (const [page, md] of await allPageMetadata()) {
      const title = renderedTitle(md);
      expect(title.match(/PermitTorch/g)?.length, `${page}: ${title}`).toBe(1);
    }
  });

  it("marks blog posts as OpenGraph articles and other pages as websites", async () => {
    for (const [page, md] of await allPageMetadata()) {
      const og = md.openGraph as { type?: string; publishedTime?: string };
      expect(og.type, page).toBe(page.startsWith("post:") ? "article" : "website");
      if (page.startsWith("post:")) expect(og.publishedTime).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    }
  });

  it("points every page's social image at /og-image.png", async () => {
    for (const [page, md] of await allPageMetadata()) {
      expect((md.openGraph as { images?: { url: string }[] }).images?.[0]?.url, page).toBe("https://permittorch.com/og-image.png");
      expect((md.twitter as { images?: string[] }).images, page).toEqual(["https://permittorch.com/og-image.png"]);
    }
  });

  it("uses the absolute branded title on the homepage", () => {
    expect(renderedTitle(home)).toBe("PermitTorch — Fire Protection Leads From Public Permit Data");
  });

  it("gives every page a unique title", async () => {
    const titles = (await allPageMetadata()).map(([, md]) => renderedTitle(md));
    expect(new Set(titles).size).toBe(titles.length);
  });

  it("gives every page a unique canonical URL", async () => {
    const canon = (await allPageMetadata()).map(([, md]) => String(md.alternates?.canonical));
    expect(canon.every((c) => c.startsWith("https://permittorch.com/"))).toBe(true);
    expect(new Set(canon).size).toBe(canon.length);
  });
});
