import { describe, expect, it } from "vitest";
import { articleJsonLd, organizationJsonLd, websiteJsonLd } from "@/lib/marketing/structured-data";
import { pricingProductJsonLd, PRICING_TIERS } from "@/components/marketing/pricing-tiers";
import { blogPosts } from "@/components/marketing/blog-posts";
import { SCORE_EXAMPLE, SCORE_WEIGHTS } from "@/components/marketing/score-example";
import * as og from "@/components/marketing/og-card";

describe("structured data", () => {
  it("describes the Organization and WebSite", () => {
    expect(organizationJsonLd()).toMatchObject({ "@type": "Organization", name: "PermitTorch", url: "https://permittorch.com" });
    expect(websiteJsonLd()).toMatchObject({ "@type": "WebSite", name: "PermitTorch", url: "https://permittorch.com" });
  });

  it("lists three monthly USD offers on the pricing Product", () => {
    const ld = pricingProductJsonLd();
    expect(ld["@type"]).toBe("Product");
    expect(ld.offers).toHaveLength(3);
    ld.offers.forEach((o, i) => {
      expect(o.priceCurrency).toBe("USD");
      expect(o.price).toBe(PRICING_TIERS[i].price.toFixed(2));
      expect(o.priceSpecification.unitCode).toBe("MON");
    });
  });

  it("gives every Article a dateModified and an image", () => {
    for (const p of blogPosts) {
      const ld = articleJsonLd(p);
      expect(ld.dateModified).toBe(p.publishedAt);
      expect(ld.image[0]).toBe("https://permittorch.com/og-image.png");
    }
  });

  it("ships a 1200x630 PNG social card", () => {
    expect(og.size).toEqual({ width: 1200, height: 630 });
    expect(og.contentType).toBe("image/png");
    expect(og.alt).toMatch(/PermitTorch/);
  });
});

describe("illustrative score example", () => {
  it("starts from the BASE_SCORE baseline row", () => {
    expect(SCORE_EXAMPLE.signals[0]).toEqual({
      type: "BASE_SCORE", label: "Baseline for a classified fire-protection permit", points: 30,
    });
  });

  it("uses the configured engine weights", () => {
    expect(SCORE_WEIGHTS).toEqual({
      BASE_SCORE: 30, NEW_COMMERCIAL_BUILD: 25, FIRE_SPRINKLER_SCOPE: 25,
      PERMIT_RECENT: 15, HIGH_PROJECT_VALUE: 10, NO_CONTRACTOR_LISTED: 10,
      FIRE_ALARM_SCOPE: 20, FAILED_INSPECTION: 20, LARGE_SQUARE_FOOTAGE: 10,
      FIRE_CONTRACTOR_ASSIGNED: -25, OLD_PERMIT: -20, CLOSED_PERMIT: -30,
    });
    for (const s of SCORE_EXAMPLE.signals) expect(s.points).toBe(SCORE_WEIGHTS[s.type]);
  });

  it("totals a realistic, unclamped 90–95", () => {
    const sum = SCORE_EXAMPLE.signals.reduce((a, s) => a + s.points, 0);
    expect(SCORE_EXAMPLE.total).toBe(sum);
    expect(sum).toBeGreaterThanOrEqual(90);
    expect(sum).toBeLessThanOrEqual(95);
  });
});

describe("/og-image.png route", () => {
  it("renders the social card as a PNG", async () => {
    const { GET, dynamic } = await import("@/app/(marketing)/og-image.png/route");
    expect(dynamic).toBe("force-static");
    const res = GET();
    expect(res.headers.get("content-type")).toBe("image/png");
  });
});
