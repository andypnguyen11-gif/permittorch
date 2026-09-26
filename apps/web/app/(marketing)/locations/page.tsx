import Link from "next/link";
import type { Metadata } from "next";
import { getMarkets } from "@/lib/api";
import { buildMetadata } from "@/lib/seo";
import { marketLocationPath, stateDisplayName } from "@/components/marketing/market-slug";

export const metadata: Metadata = buildMetadata({
  title: "Markets We Cover — PermitTorch",
  description:
    "Cities where PermitTorch actively monitors permit and inspection records for fire protection leads. We only list markets with live data coverage.",
  path: "/locations",
});

export default async function LocationsPage() {
  const markets = await getMarkets();
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">Markets we cover</h1>
      <p className="mt-4 max-w-2xl text-lg text-neutral-600">
        Every market below has live permit and inspection coverage today. We add cities as
        real data comes online — never before.
      </p>
      <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {markets.map((m) => (
          <Link key={m.slug} href={marketLocationPath(m)}
            className="group rounded-2xl border border-neutral-200 p-6 transition-shadow hover:shadow-md">
            <h2 className="text-xl font-semibold group-hover:text-orange-600">
              {m.city}, {m.state}
            </h2>
            <p className="mt-1 text-sm text-neutral-500">
              Fire protection leads in {m.city}, {stateDisplayName(m.state)}
            </p>
            <span className="mt-4 inline-block text-sm font-medium text-orange-600">
              View market →
            </span>
          </Link>
        ))}
      </div>
      <p className="mt-12 text-sm text-neutral-500">
        Don&apos;t see your city? New markets open regularly — <Link href="/signup" className="text-orange-600 underline">create a free account</Link> and we&apos;ll notify you when yours goes live.
      </p>
    </div>
  );
}
