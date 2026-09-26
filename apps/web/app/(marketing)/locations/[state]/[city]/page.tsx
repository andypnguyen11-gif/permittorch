import Link from "next/link";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getMarkets, getMarketStats } from "@/lib/api";
import { buildMetadata, jsonLd, SITE_URL } from "@/lib/seo";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { CATEGORY_LABELS } from "@/components/marketing/category-labels";
import { FreshnessLine } from "@/components/marketing/freshness-line";
import {
  findMarketByLocationParams, marketLocationPath, marketToLocationParams, stateDisplayName,
} from "@/components/marketing/market-slug";
import {
  Accordion, AccordionContent, AccordionItem, AccordionTrigger,
} from "@/components/ui/accordion";

// PRD §24: pages exist ONLY for markets returned by getMarkets(). No fabricated params.
export const dynamicParams = false;

interface Props { params: Promise<{ state: string; city: string }> }

export async function generateStaticParams() {
  const markets = await getMarkets();
  return markets.map((m) => marketToLocationParams(m));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { state, city } = await params;
  const market = findMarketByLocationParams(await getMarkets(), state, city);
  if (!market) return {};
  const stateName = stateDisplayName(market.state);
  return buildMetadata({
    title: `Fire Protection Leads in ${market.city}, ${stateName} — PermitTorch`,
    description: `Live fire protection lead data for ${market.city}, ${stateName}: sprinkler, alarm, and suppression opportunities from public permit records, updated daily.`,
    path: marketLocationPath(market),
  });
}

// Static, anonymized illustrations — clearly labeled on the page. Not live records.
const EXAMPLE_LEADS = [
  { score: 92, title: "Fire sprinkler system — new commercial build", detail: "New multi-story commercial construction, seven-figure valuation, no fire contractor listed on the permit.", meta: "Filed this week · Address available to subscribers" },
  { score: 86, title: "Fire alarm system — healthcare tenant upfit", detail: "Multi-floor tenant improvement with alarm scope in the permit description.", meta: "Filed this week · Address available to subscribers" },
  { score: 78, title: "Kitchen suppression — new restaurant", detail: "Restaurant build-out in a high-traffic retail corridor; hood system required.", meta: "Filed this month · Address available to subscribers" },
];

export default async function MarketPage({ params }: Props) {
  const { state, city } = await params;
  const market = findMarketByLocationParams(await getMarkets(), state, city);
  if (!market) notFound();

  const stats = await getMarketStats(market.slug);
  const stateName = stateDisplayName(market.state);
  const categories = (Object.entries(stats.byCategory) as [keyof typeof CATEGORY_LABELS, number][])
    .filter(([, n]) => n > 0)
    .sort(([, a], [, b]) => b - a);

  const faq = [
    { q: `Where does the ${market.city} data come from?`, a: `From publicly available permit and inspection records published by government jurisdictions in the ${market.city} area. Every lead links to the official source record.` },
    { q: "How fresh is the data?", a: "Sources are checked on a daily cycle and this page shows exactly when data was last updated. We never present stale data as current — if a source falls behind, we say so." },
    { q: "Are the example leads real?", a: "The examples above are illustrative and anonymized. Subscribers see full records: address, permit number, filing date, estimated value, score breakdown, and the official source link." },
    { q: `What does PermitTorch cost in ${market.city}?`, a: "Plans start at $49/month for one market, and every account starts with a 7-day Pro trial. See the pricing page for details." },
  ];

  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <nav className="text-sm text-neutral-500">
        <Link href="/locations" className="hover:text-neutral-900">Markets</Link>
        <span className="mx-2">/</span>
        <span>{market.city}, {market.state}</span>
      </nav>

      <h1 className="mt-4 text-4xl font-bold tracking-tight">
        Fire Protection Leads in {market.city}, {stateName}
      </h1>
      <div className="mt-4"><FreshnessLine lastUpdatedAt={stats.lastUpdatedAt} /></div>

      <section className="mt-10 rounded-2xl border border-neutral-200 bg-neutral-50 p-8">
        <p className="text-lg">
          PermitTorch identified{" "}
          <strong className="text-orange-600">
            {stats.totalLast30Days} fire-related opportunities
          </strong>{" "}
          in {market.city} during the last 30 days:
        </p>
        <dl className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          {categories.map(([cat, count]) => (
            <div key={cat} className="rounded-xl bg-white p-4 shadow-sm">
              <dt className="text-sm text-neutral-500">{CATEGORY_LABELS[cat]}</dt>
              <dd className="mt-1 text-2xl font-bold">{count}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section className="mt-16">
        <div className="flex items-baseline justify-between">
          <h2 className="text-2xl font-bold tracking-tight">What leads look like</h2>
          <span className="text-xs font-medium uppercase tracking-wide text-neutral-400">
            Illustrative examples — anonymized
          </span>
        </div>
        <div className="mt-6 grid gap-6 md:grid-cols-3">
          {EXAMPLE_LEADS.map((l) => (
            <div key={l.title} className="rounded-2xl border border-neutral-200 p-6">
              <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-orange-500 font-bold text-white">
                {l.score}
              </span>
              <h3 className="mt-4 font-semibold">{l.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{l.detail}</p>
              <p className="mt-3 text-xs text-neutral-400">{l.meta}</p>
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
          className={cn(buttonVariants({ size: "lg" }), "mt-6 bg-orange-500 px-8 text-white hover:bg-orange-600")}>
          Find Leads in {market.city}
        </Link>
      </section>

      <section className="mx-auto mt-16 max-w-2xl">
        <h2 className="text-2xl font-bold tracking-tight">Questions about {market.city} coverage</h2>
        <Accordion className="mt-6">
          {faq.map((f, i) => (
            <AccordionItem key={f.q} value={`faq-${i}`}>
              <AccordionTrigger className="text-left">{f.q}</AccordionTrigger>
              <AccordionContent className="text-neutral-600">{f.a}</AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd({
        "@context": "https://schema.org",
        "@type": "FAQPage",
        mainEntity: faq.map((f) => ({
          "@type": "Question", name: f.q,
          acceptedAnswer: { "@type": "Answer", text: f.a },
        })),
      })} />
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd({
        "@context": "https://schema.org",
        "@type": "BreadcrumbList",
        itemListElement: [
          { "@type": "ListItem", position: 1, name: "Markets", item: `${SITE_URL}/locations` },
          { "@type": "ListItem", position: 2, name: `${market.city}, ${market.state}`, item: `${SITE_URL}${marketLocationPath(market)}` },
        ],
      })} />
    </div>
  );
}
