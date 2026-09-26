// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
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
