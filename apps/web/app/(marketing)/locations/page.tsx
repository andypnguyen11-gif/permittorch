import Link from "next/link";
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { getMarketsWithData } from "@/lib/marketing/markets-with-data";
import { marketLocationPath, stateDisplayName } from "@/components/marketing/market-slug";
import { FreshnessLine } from "@/components/marketing/freshness-line";

export const revalidate = 3600;

export const metadata: Metadata = buildMetadata({
  title: "Fire Protection Lead Markets We Cover",
  description:
    "Cities where PermitTorch actively monitors permit and inspection records for fire protection leads. We only list markets with live data coverage.",
  path: "/locations",
});

export default async function LocationsPage() {
  const entries = await getMarketsWithData();
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">Markets we cover</h1>
      <p className="mt-4 max-w-2xl text-lg text-neutral-600">
        {entries.length > 0
          ? "The markets listed below have fire-related permit data from the last 30 days. A market appears here only once real data is flowing — never before."
          : "We are finishing the first data refresh for our markets. Markets appear here as soon as real permit data is flowing — never before."}
      </p>
      {entries.length > 0 && (
        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {entries.map(({ market: m, stats }) => (
            <Link key={m.slug} href={marketLocationPath(m)}
              className="group rounded-2xl border border-neutral-200 p-6 transition-shadow hover:shadow-md">
              <h2 className="text-xl font-semibold group-hover:text-orange-700">
                {m.city}, {m.state}
              </h2>
              <p className="mt-1 text-sm text-neutral-500">
                {stats.totalLast30Days} fire-related permits in {m.city}, {stateDisplayName(m.state)} in the last 30 days
              </p>
              <div className="mt-2"><FreshnessLine lastUpdatedAt={stats.lastUpdatedAt} /></div>
              <span className="mt-4 inline-block text-sm font-medium text-orange-700">
                View market →
              </span>
            </Link>
          ))}
        </div>
      )}
      <p className="mt-12 text-sm text-neutral-500">
        Don&apos;t see your city? We only list a market once its public permit data is flowing.
        Email <a href="mailto:support@permittorch.com" className="text-orange-700 underline">support@permittorch.com</a> to
        ask about yours.
      </p>
    </div>
  );
}
