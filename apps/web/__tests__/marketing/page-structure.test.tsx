// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ReactElement } from "react";
import { cleanup, render, screen, within } from "@testing-library/react";
import { mockMarkets, mockMarketStats } from "@/lib/fixtures/markets";

vi.mock("@/lib/api", async (importOriginal) => ({
  ApiError: (await importOriginal<typeof import("@/lib/api")>()).ApiError,
  getMarkets: vi.fn(async () => mockMarkets),
  getMarketStats: vi.fn(async (slug: string) => mockMarketStats[slug]),
  getAllMarketStats: vi.fn(async () => Object.values(mockMarketStats)),
  submitSampleLeadRequest: vi.fn(),
}));
vi.mock("next/navigation", () => ({
  notFound: () => { throw new Error("NEXT_NOT_FOUND"); },
  usePathname: () => "/",
}));

import HomePage from "@/app/(marketing)/page";
import PricingPage from "@/app/(marketing)/pricing/page";
import HowItWorksPage from "@/app/(marketing)/how-it-works/page";
import FireProtectionLeadsPage from "@/app/(marketing)/fire-protection-leads/page";
import LocationsPage from "@/app/(marketing)/locations/page";
import MarketPage from "@/app/(marketing)/locations/[state]/[city]/page";
import BlogIndexPage from "@/app/(marketing)/blog/page";
import BlogPostPage from "@/app/(marketing)/blog/[slug]/page";
import { SiteFooter } from "@/components/marketing/site-footer";
import { Breadcrumbs, breadcrumbJsonLd } from "@/components/marketing/breadcrumbs";

afterEach(() => cleanup());

/** One h1; each heading at most one level deeper than the one before it. */
function expectValidHeadingOrder(container: HTMLElement) {
  const levels = [...container.querySelectorAll("h1,h2,h3,h4,h5,h6")].map((h) => Number(h.tagName[1]));
  expect(levels.filter((l) => l === 1)).toHaveLength(1);
  expect(levels[0]).toBe(1);
  levels.forEach((l, i) => {
    if (i > 0) expect(l, `heading levels ${levels.join(",")}`).toBeLessThanOrEqual(levels[i - 1] + 1);
  });
}

const PAGES: [string, () => Promise<ReactElement>][] = [
  ["/", () => HomePage()],
  ["/pricing", async () => PricingPage()],
  ["/how-it-works", async () => HowItWorksPage()],
  ["/fire-protection-leads", () => FireProtectionLeadsPage()],
  ["/locations", () => LocationsPage()],
  ["/locations/texas/austin", () => MarketPage({ params: Promise.resolve({ state: "texas", city: "austin" }) })],
  ["/blog", async () => BlogIndexPage()],
  ["/blog/[slug]", () => BlogPostPage({ params: Promise.resolve({ slug: "using-building-permits-for-lead-generation" }) })],
];

describe("heading structure", () => {
  it.each(PAGES)("%s has one h1 and no skipped heading levels", async (_path, page) => {
    const { container } = render(await page());
    expectValidHeadingOrder(container);
  });

  it("the footer introduces no headings (column labels are plain text)", () => {
    const { container } = render(<SiteFooter />);
    expect(container.querySelectorAll("h1,h2,h3,h4,h5,h6")).toHaveLength(0);
    expect(screen.getByRole("list", { name: "Product" })).toBeDefined();
  });
});

describe("homepage hero preview", () => {
  it("shows an illustrative Austin lead card with a score badge", async () => {
    render(await HomePage());
    const figure = screen.getByRole("figure");
    expect(within(figure).getByText("Illustrative — not a live record")).toBeDefined();
    expect(within(figure).getByText(/Austin, TX/)).toBeDefined();
    expect(within(figure).getByText("95")).toBeDefined();
  });

  it("publishes Organization and WebSite JSON-LD", async () => {
    const { container } = render(await HomePage());
    const types = [...container.querySelectorAll('script[type="application/ld+json"]')]
      .map((s) => JSON.parse(s.textContent!)["@type"]);
    expect(types).toEqual(expect.arrayContaining(["Organization", "WebSite"]));
  });
});

describe("Breadcrumbs", () => {
  const items = [
    { name: "Home", path: "/" },
    { name: "Markets", path: "/locations" },
    { name: "Austin, TX", path: "/locations/texas/austin" },
  ];

  it("is a labelled nav with hidden separators and a current page", () => {
    const { container } = render(<Breadcrumbs items={items} />);
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(within(nav).getAllByRole("listitem")).toHaveLength(3);
    const seps = [...container.querySelectorAll("li > span")].filter((s) => s.textContent === "/");
    expect(seps).toHaveLength(2);
    for (const s of seps) expect(s).toHaveAttribute("aria-hidden", "true");
    expect(screen.getByText("Austin, TX")).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("href", "/");
  });

  it("emits BreadcrumbList JSON-LD starting at Home", () => {
    const ld = breadcrumbJsonLd(items);
    expect(ld.itemListElement[0]).toEqual({
      "@type": "ListItem", position: 1, name: "Home", item: "https://permittorch.com/",
    });
    expect(ld.itemListElement.map((i) => i.position)).toEqual([1, 2, 3]);
  });

  it("is rendered on market pages with Home first", async () => {
    render(await MarketPage({ params: Promise.resolve({ state: "texas", city: "austin" }) }));
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(within(nav).getAllByRole("listitem")[0].textContent).toContain("Home");
  });
});
