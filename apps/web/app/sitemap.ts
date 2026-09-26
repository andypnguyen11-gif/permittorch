import type { MetadataRoute } from "next";
import { getMarketsWithData } from "@/lib/marketing/markets-with-data";
import { SITE_URL } from "@/lib/seo";
import { marketLocationPath } from "@/components/marketing/market-slug";
import { blogPosts } from "@/components/marketing/blog-posts";

// Hourly, so market pages appear/disappear with their data (no thin pages listed).
export const revalidate = 3600;

const STATIC_ROUTES: { path: string; priority: number }[] = [
  { path: "/", priority: 1.0 },
  { path: "/pricing", priority: 0.9 },
  { path: "/how-it-works", priority: 0.8 },
  { path: "/fire-protection-leads", priority: 0.9 },
  { path: "/fire-sprinkler-leads", priority: 0.9 },
  { path: "/fire-alarm-leads", priority: 0.9 },
  { path: "/locations", priority: 0.8 },
  { path: "/blog", priority: 0.6 },
  { path: "/terms", priority: 0.2 },
  { path: "/privacy", priority: 0.2 },
];

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const markets = (await getMarketsWithData()).map((e) => e.market);
  const url = (path: string) => new URL(path, SITE_URL).toString();
  return [
    ...STATIC_ROUTES.map((r) => ({
      url: url(r.path), priority: r.priority, changeFrequency: "weekly" as const,
    })),
    ...markets.map((m) => ({
      url: url(marketLocationPath(m)), priority: 0.8, changeFrequency: "daily" as const,
    })),
    ...blogPosts.map((p) => ({
      url: url(`/blog/${p.slug}`), priority: 0.5,
      changeFrequency: "monthly" as const, lastModified: p.publishedAt,
    })),
  ];
}
