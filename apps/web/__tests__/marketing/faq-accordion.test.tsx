import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { FaqAccordion, faqPageJsonLd } from "@/components/marketing/faq-accordion";
import { PRICING_FAQ } from "@/components/marketing/pricing-faq";

describe("FaqAccordion", () => {
  it("puts every collapsed answer in the server HTML (hidden until found)", () => {
    const html = renderToStaticMarkup(<FaqAccordion items={PRICING_FAQ} />);
    for (const f of PRICING_FAQ) {
      // React escapes apostrophes; compare on an apostrophe-free prefix.
      expect(html).toContain(f.a.split("'")[0]);
    }
    // Collapsed panels stay mounted (Base UI upgrades hidden → until-found on the client).
    expect(html.match(/role="region"/g)).toHaveLength(PRICING_FAQ.length);
  });

  it("builds FAQPage JSON-LD from the same entries", () => {
    const ld = faqPageJsonLd(PRICING_FAQ);
    expect(ld["@type"]).toBe("FAQPage");
    expect(ld.mainEntity.map((q) => q.acceptedAnswer.text)).toEqual(PRICING_FAQ.map((f) => f.a));
  });
});

describe("PRICING_FAQ copy", () => {
  const text = PRICING_FAQ.map((f) => `${f.q} ${f.a}`).join(" ");

  it("describes the checkout trial truthfully", () => {
    expect(text).toContain(
      "Every paid plan starts with a 7-day free trial when you subscribe at checkout (card required; cancel anytime before it ends)",
    );
    expect(text).not.toMatch(/Pro trial/);
  });

  it("does not promise self-serve market switching", () => {
    expect(text).not.toMatch(/switch markets/i);
    expect(text).toContain("Need a different market? Contact support and we'll move your subscription.");
  });

  it("uses a supported market as its example", () => {
    expect(text).toContain("Austin, TX");
    expect(text).not.toMatch(/Houston/);
  });
});
