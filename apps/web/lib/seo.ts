import type { Metadata } from "next";

export const SITE_URL = "https://permittorch.com";
export const SITE_NAME = "PermitTorch";
export const SITE_TAGLINE = "Fire protection leads from public permit data";

/**
 * Public path of app/(marketing)/opengraph-image.tsx. Next suffixes metadata
 * image routes inside route groups with a deterministic hash of the group path
 * ("/(marketing)" -> "pwu6ef"); a unit test recomputes it so a move fails loudly.
 * Pages set openGraph explicitly, so the file convention alone would only cover "/".
 */
export const DEFAULT_OG_IMAGE_PATH = "/opengraph-image-pwu6ef";

/** Default social card for every marketing page. */
export const DEFAULT_OG_IMAGE = {
  url: `${SITE_URL}${DEFAULT_OG_IMAGE_PATH}`,
  width: 1200,
  height: 630,
  alt: `${SITE_NAME} — ${SITE_TAGLINE}`,
};

export interface BuildMetadataInput {
  /**
   * Page title WITHOUT the brand — the root layout's template appends
   * " | PermitTorch". Set `absoluteTitle` to bypass the template (homepage).
   */
  title: string;
  description: string;
  path: string;       // must start with "/"
  ogImage?: string;   // absolute URL; defaults to DEFAULT_OG_IMAGE
  absoluteTitle?: boolean;
}

export function buildMetadata({ title, description, path, ogImage, absoluteTitle }: BuildMetadataInput): Metadata {
  const url = new URL(path, SITE_URL).toString();
  const image = ogImage ? { url: ogImage } : DEFAULT_OG_IMAGE;
  return {
    title: absoluteTitle ? { absolute: title } : title,
    description,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      siteName: SITE_NAME,
      type: "website",
      locale: "en_US",
      images: [image],
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      images: [image.url],
    },
  };
}

/** Safe payload for <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(obj)} /> */
export function jsonLd(obj: object): { __html: string } {
  return { __html: JSON.stringify(obj).replace(/</g, "\\u003c") };
}
