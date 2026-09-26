import { describe, expect, it } from "vitest";
import { normalizeMetadataRoute } from "next/dist/lib/metadata/get-metadata-route";
import { buildMetadata, DEFAULT_OG_IMAGE, DEFAULT_OG_IMAGE_PATH, jsonLd, SITE_URL } from "@/lib/seo";

describe("buildMetadata", () => {
  const base = { title: "Pricing", description: "Plans for fire protection contractors.", path: "/pricing" };

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

  it("uses the provided ogImage, otherwise the default 1200x630 social card", () => {
    const withImg = buildMetadata({ ...base, ogImage: `${SITE_URL}/og/pricing.png` });
    expect((withImg.openGraph as { images?: unknown[] }).images).toEqual([{ url: `${SITE_URL}/og/pricing.png` }]);
    expect((withImg.twitter as { images?: string[] }).images).toEqual([`${SITE_URL}/og/pricing.png`]);
    const without = buildMetadata(base);
    expect((without.openGraph as { images?: unknown }).images).toEqual([DEFAULT_OG_IMAGE]);
    expect((without.twitter as { images?: string[] }).images).toEqual([`${SITE_URL}/opengraph-image-pwu6ef`]);
    expect(DEFAULT_OG_IMAGE).toMatchObject({ width: 1200, height: 630 });
  });

  it("points at the route Next actually serves for app/(marketing)/opengraph-image.tsx", () => {
    const route = normalizeMetadataRoute("/(marketing)/opengraph-image"); // "/(marketing)/opengraph-image-<hash>/route"
    expect(route.replace("/(marketing)", "").replace(/\/route$/, "")).toBe(DEFAULT_OG_IMAGE_PATH);
  });

  it("uses an absolute title only when asked (bypasses the root template)", () => {
    expect(buildMetadata({ ...base, absoluteTitle: true }).title).toEqual({ absolute: base.title });
    expect(buildMetadata(base).title).toBe(base.title);
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
