import type { Metadata } from "next";

export const SITE_URL = "https://permittorch.com";
export const SITE_NAME = "PermitTorch";

export interface BuildMetadataInput {
  title: string;
  description: string;
  path: string;       // must start with "/"
  ogImage?: string;   // absolute URL
}

export function buildMetadata({ title, description, path, ogImage }: BuildMetadataInput): Metadata {
  const url = new URL(path, SITE_URL).toString();
  return {
    title,
    description,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      siteName: SITE_NAME,
      type: "website",
      locale: "en_US",
      ...(ogImage ? { images: [{ url: ogImage }] } : {}),
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      ...(ogImage ? { images: [ogImage] } : {}),
    },
  };
}

/** Safe payload for <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(obj)} /> */
export function jsonLd(obj: object): { __html: string } {
  return { __html: JSON.stringify(obj).replace(/</g, "\\u003c") };
}
