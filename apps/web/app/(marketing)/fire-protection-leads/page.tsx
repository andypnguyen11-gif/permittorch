import type { Metadata } from "next";
import { getMarketsWithData } from "@/lib/marketing/markets-with-data";
import { buildMetadata } from "@/lib/seo";
import { CategoryLander, CATEGORY_LANDERS } from "@/components/marketing/category-lander";

const content = CATEGORY_LANDERS["fire-protection"];

export const revalidate = 3600;

export const metadata: Metadata = buildMetadata({
  title: content.metaTitle,
  description: content.metaDescription,
  path: content.path,
});

export default async function FireProtectionLeadsPage() {
  return <CategoryLander content={content} markets={(await getMarketsWithData()).map((e) => e.market)} />;
}
