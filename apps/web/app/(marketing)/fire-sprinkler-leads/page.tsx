import type { Metadata } from "next";
import { getMarkets } from "@/lib/api";
import { buildMetadata } from "@/lib/seo";
import { CategoryLander, CATEGORY_LANDERS } from "@/components/marketing/category-lander";

const content = CATEGORY_LANDERS["fire-sprinkler"];

export const metadata: Metadata = buildMetadata({
  title: content.metaTitle,
  description: content.metaDescription,
  path: content.path,
});

export default async function FireSprinklerLeadsPage() {
  return <CategoryLander content={content} markets={await getMarkets()} />;
}
