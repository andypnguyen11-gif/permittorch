import { stateDisplayName } from "@/components/marketing/market-slug";
import { PORTAL_TYPE_LABELS, getMarketSources } from "@/lib/marketing/source-registry";

/** "Where the data comes from" for a market; renders nothing for slugs outside the registry. */
export function MarketSources({ slug }: { slug: string }) {
  const entry = getMarketSources(slug);
  if (!entry || entry.sources.length === 0) return null;
  const { market, sources } = entry;
  return (
    <section aria-labelledby="market-sources-heading" className="mt-16">
      <h2 id="market-sources-heading" className="text-2xl font-bold tracking-tight">
        Where the data comes from
      </h2>
      <p className="mt-3 max-w-2xl leading-relaxed text-neutral-600">
        Jurisdiction: <strong className="font-semibold text-neutral-900">
          {market.city}, {stateDisplayName(market.state)}
        </strong>. PermitTorch reads {sources.length === 1 ? "this public government data source" : `these ${sources.length} public government data sources`} on
        a daily cycle, and every lead links back to the official record.
      </p>
      <ul className="mt-6 grid gap-4 sm:grid-cols-2">
        {sources.map((s) => (
          <li key={s.sourceId} className="rounded-xl border border-neutral-200 p-4">
            <p className="font-semibold text-neutral-900">{s.name}</p>
            <p className="mt-1 text-sm text-neutral-500">{PORTAL_TYPE_LABELS[s.portalType]}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
