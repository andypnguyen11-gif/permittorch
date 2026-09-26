import Link from "next/link";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getMarketStats, getMarkets } from "@/lib/api";
import { getMarketsWithData, hasRealData } from "@/lib/marketing/markets-with-data";
import { buildMetadata, jsonLd } from "@/lib/seo";
import { Breadcrumbs, breadcrumbJsonLd } from "@/components/marketing/breadcrumbs";
import { ctaClasses } from "@/components/marketing/cta";
import { CATEGORY_LABELS } from "@/components/marketing/category-labels";
import { FreshnessLine } from "@/components/marketing/freshness-line";
import {
  findMarketByLocationParams, marketLocationPath, marketToLocationParams, stateDisplayName,
} from "@/components/marketing/market-slug";
import { FaqAccordion, faqPageJsonLd } from "@/components/marketing/faq-accordion";
import { EXAMPLE_LEADS } from "@/components/marketing/market-example-leads";
import { MarketSources } from "@/components/marketing/market-sources";
import { marketNarrative } from "@/lib/marketing/market-narrative";
import { getMarketSources } from "@/lib/marketing/source-registry";

// PRD §24 + CLAUDE.md: pages exist ONLY for markets with real data. Params are
// prebuilt from getMarketsWithData(); at request time the market is resolved from
// the catalog first (junk URLs cost one getMarkets call), then its own stats are
// fetched. Unknown city or no data → notFound(). A stats *failure* throws, so ISR
// keeps serving the last good render instead of caching a 404. Dynamic
// params stay enabled so a market whose data comes online after deploy gets its
// page on the next hourly revalidation, matching the (also revalidated) sitemap.
export const dynamicParams = true;
export const revalidate = 3600;

interface Props { params: Promise<{ state: string; city: string }> }

export async function generateStaticParams() {
  return (await getMarketsWithData()).map((e) => marketToLocationParams(e.market));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { state, city } = await params;
  const entry = await resolveMarket(state, city);
  if (!entry) return {};
  const { market } = entry;
  const stateName = stateDisplayName(market.state);
  return buildMetadata({
    title: `Fire Protection Leads in ${market.city}, ${stateName}`,
    description: `Live fire protection lead data for ${market.city}, ${stateName}: sprinkler, alarm, and suppression opportunities from public permit records, updated daily.`,
    path: marketLocationPath(market),
  });
}

/** Market + stats for URL params; null when unknown or without real data. Stats errors propagate. */
async function resolveMarket(state: string, city: string) {
  const market = findMarketByLocationParams(await getMarkets(), state, city);
  if (!market) return null;
  const stats = await getMarketStats(market.slug);
  return hasRealData(stats) ? { market, stats } : null;
}

export default async function MarketPage({ params }: Props) {
  const { state, city } = await params;
  const entry = await resolveMarket(state, city);
  if (!entry) notFound();
  const { market, stats } = entry;
  const stateName = stateDisplayName(market.state);
  const categories = (Object.entries(stats.byCategory) as [keyof typeof CATEGORY_LABELS, number][])
    .filter(([, n]) => n > 0)
    .sort(([, a], [, b]) => b - a);

  const narrative = marketNarrative(market.city, stats);
  const registry = getMarketSources(market.slug);
  const sourceNames = registry?.sources.map((s) => s.name) ?? [];

  const crumbs = [
    { name: "Home", path: "/" },
    { name: "Markets", path: "/locations" },
    { name: `${market.city}, ${market.state}`, path: marketLocationPath(market) },
  ];

  const faq = [
    { q: `Where does the ${market.city} data come from?`, a: sourceNames.length > 0
      ? `From public government records for ${market.city}, ${stateName}: ${sourceNames.join("; ")}. Every lead links to the official source record.`
      : `From publicly available permit and inspection records published by government jurisdictions in the ${market.city} area. Every lead links to the official source record.` },
    { q: "How fresh is the data?", a: "Sources are checked on a daily cycle and this page shows exactly when data was last updated. We never present stale data as current — if a source falls behind, we say so." },
    { q: "Are the example leads real?", a: "The examples above are illustrative — they are not records from this market. Subscribers see full records: address, permit number, filing date, estimated value, score breakdown, and the official source link." },
    { q: `What does PermitTorch cost in ${market.city}?`, a: "Plans start at $49/month for one market. Every paid plan starts with a 7-day free trial when you subscribe at checkout (card required; cancel anytime before it ends). See the pricing page for details." },
  ];

  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <Breadcrumbs items={crumbs} />

      <h1 className="mt-4 text-4xl font-bold tracking-tight">
        Fire Protection Leads in {market.city}, {stateName}
      </h1>
      <div className="mt-4"><FreshnessLine lastUpdatedAt={stats.lastUpdatedAt} /></div>

      <section className="mt-10 rounded-2xl border border-neutral-200 bg-neutral-50 p-8">
        <p className="text-lg">
          PermitTorch identified{" "}
          <strong className="text-orange-700">
            {stats.totalLast30Days} fire-related opportunities
          </strong>{" "}
          in {market.city} during the last 30 days:
        </p>
        {narrative && <p className="mt-3 text-neutral-700">{narrative}</p>}
        <dl className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          {categories.map(([cat, count]) => (
            <div key={cat} className="rounded-xl bg-white p-4 shadow-sm">
              <dt className="text-sm text-neutral-500">{CATEGORY_LABELS[cat]}</dt>
              <dd className="mt-1 text-2xl font-bold">{count}</dd>
            </div>
          ))}
        </dl>
      </section>

      <MarketSources slug={market.slug} />

      <section className="mt-16">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-2xl font-bold tracking-tight">What leads look like</h2>
          <span className="text-xs font-medium uppercase tracking-wide text-neutral-500">
            Illustrative examples — not records from {market.city}
          </span>
        </div>
        <div className="mt-6 grid gap-6 md:grid-cols-3">
          {EXAMPLE_LEADS.map((l) => (
            <div key={l.title} className="rounded-2xl border border-neutral-200 p-6">
              <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-orange-700 font-bold text-white">
                {l.score}
              </span>
              <h3 className="mt-4 font-semibold">{l.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{l.detail}</p>
              <p className="mt-3 text-xs text-neutral-500">{l.meta}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="mt-16 rounded-2xl bg-orange-50 p-10 text-center">
        <h2 className="text-2xl font-bold tracking-tight">
          Get {market.city} opportunities every morning
        </h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">
          Start free, pick {market.city} as your market, and see tomorrow&apos;s permits scored and sorted.
        </p>
        <Link href="/signup"
          className={ctaClasses("lg", "mt-6")}>
          Find Leads in {market.city}
        </Link>
      </section>

      <section className="mx-auto mt-16 max-w-2xl">
        <h2 className="text-2xl font-bold tracking-tight">Questions about {market.city} coverage</h2>
        <FaqAccordion items={faq} className="mt-6" />
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(faqPageJsonLd(faq))} />
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(breadcrumbJsonLd(crumbs))} />
    </div>
  );
}
