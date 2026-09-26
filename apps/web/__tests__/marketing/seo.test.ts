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
