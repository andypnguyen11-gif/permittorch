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

  it("sends every CTA to /signup", () => {
    render(<PricingTiers />);
    const ctas = screen.getAllByRole("link", { name: /start free|choose/i });
    expect(ctas).toHaveLength(3);
    for (const c of ctas) expect(c.getAttribute("href")).toBe("/signup");
  });
});
