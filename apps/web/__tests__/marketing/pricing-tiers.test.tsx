// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PricingTiers, PRICING_TIERS } from "@/components/marketing/pricing-tiers";

afterEach(() => cleanup());

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

  it("sends each CTA to /signup with its plan", () => {
    render(<PricingTiers />);
    const ctas = screen.getAllByRole("link", { name: /start free|choose/i });
    expect(ctas.map((c) => c.getAttribute("href"))).toEqual([
      "/signup?plan=STARTER", "/signup?plan=PRO", "/signup?plan=TERRITORY",
    ]);
  });

  it("introduces Territory as everything in Pro, plus more", () => {
    render(<PricingTiers />);
    expect(screen.getByText("Everything in Pro, plus:")).toBeDefined();
    expect(PRICING_TIERS.find((t) => t.name === "Territory")?.featuresIntro).toBe("Everything in Pro, plus:");
  });

  it("lists CSV export explicitly on both Pro and Territory, not Starter", () => {
    const hasCsv = (name: string) =>
      PRICING_TIERS.find((t) => t.name === name)!.features.some((f) => /CSV export/.test(f));
    expect(hasCsv("Pro")).toBe(true);
    expect(hasCsv("Territory")).toBe(true);
    expect(hasCsv("Starter")).toBe(false);
  });
});

describe("/pricing during the free pilot (NEXT_PUBLIC_FREE_PILOT)", () => {
  afterEach(() => vi.unstubAllEnvs());

  it("says it is free and sends the CTA to sign-up instead of a paid plan", async () => {
    vi.stubEnv("NEXT_PUBLIC_FREE_PILOT", "1");
    const { default: PricingPage } = await import("@/app/(marketing)/pricing/page");
    const { container } = render(PricingPage());
    expect(screen.getByRole("heading", { level: 1, name: "Free during the pilot" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Create a free account" }).getAttribute("href")).toBe("/signup");
    expect(screen.queryByText("$129")).toBeNull();
    expect(screen.queryByRole("link", { name: /choose|start free with/i })).toBeNull();
    expect(container.querySelector('script[type="application/ld+json"]')).toBeNull();
  });

  it("shows the paid tiers when the flag is unset", async () => {
    vi.stubEnv("NEXT_PUBLIC_FREE_PILOT", "");
    const { default: PricingPage } = await import("@/app/(marketing)/pricing/page");
    render(PricingPage());
    expect(screen.getByText("$129")).toBeDefined();
    expect(screen.queryByText("Free during the pilot")).toBeNull();
  });
});

describe("isFreePilot", () => {
  afterEach(() => vi.unstubAllEnvs());

  it.each([["1", true], ["true", true], ["TRUE", true], ["0", false], ["", false]])(
    "NEXT_PUBLIC_FREE_PILOT=%s → %s", async (value, expected) => {
      vi.stubEnv("NEXT_PUBLIC_FREE_PILOT", value);
      const { isFreePilot } = await import("@/lib/free-pilot");
      expect(isFreePilot()).toBe(expected);
    });
});
