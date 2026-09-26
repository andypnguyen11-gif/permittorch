import Link from "next/link";
import type { Metadata } from "next";
import { getMarketsWithData } from "@/lib/marketing/markets-with-data";
import { buildMetadata, jsonLd } from "@/lib/seo";
import { organizationJsonLd, websiteJsonLd } from "@/lib/marketing/structured-data";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { ICON_ACCENT, ctaClasses } from "@/components/marketing/cta";
import { HeroLeadPreview } from "@/components/marketing/hero-lead-preview";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";
import { HOW_IT_WORKS_STEPS } from "@/components/marketing/how-it-works-steps";
import { SAMPLE_LEADS_PROMISE } from "@/components/marketing/sample-leads-copy";

export const metadata: Metadata = buildMetadata({
  title: "PermitTorch — Fire Protection Leads From Public Permit Data",
  absoluteTitle: true,
  description:
    "PermitTorch monitors public permit and inspection records and identifies fire-protection opportunities before they disappear into another spreadsheet.",
  path: "/",
});

// Freshness guardrail: re-render hourly so market lists never go stale.
export const revalidate = 3600;

const VALUE_BULLETS = [
  {
    title: "Built only for fire protection",
    body: "No plumbing permits, no roofing noise. Sprinkler, alarm, suppression, kitchen systems, and inspections — that is the whole feed.",
  },
  {
    title: "Every score is explained",
    body: "A 95 is a 95 for reasons you can read: a classified fire-protection permit, new commercial build, sprinkler scope detected, filed this week.",
  },
  {
    title: "Freshness you can check",
    body: "Every lead shows when its source was last updated. We never dress up stale data as current.",
  },
  {
    title: "Straight to the record",
    body: "Each lead links to the official government permit page, so you can verify before you drive across town.",
  },
];

export default async function HomePage() {
  const markets = (await getMarketsWithData()).map((e) => e.market);
  return (
    <>
      {/* Hero */}
      <section className="mx-auto grid max-w-6xl items-center gap-12 px-4 pb-20 pt-20 sm:px-6 md:grid-cols-2 md:pt-24">
        <div className="text-center md:text-left">
          <h1 className="text-4xl font-bold tracking-tight sm:text-5xl lg:text-6xl">
            Find the permits worth chasing<span aria-hidden="true" className={ICON_ACCENT}>.</span>
          </h1>
          <p className="mt-6 text-lg leading-relaxed text-neutral-600">
            PermitTorch monitors public permit and inspection records and identifies
            fire-protection opportunities before they disappear into another spreadsheet.
          </p>
          <div className="mt-10 flex flex-col items-center gap-3 sm:flex-row sm:justify-center md:justify-start">
            <Link href="/signup" className={ctaClasses("hero")}>
              Find Leads in Your Market
            </Link>
            <Link href="#sample-leads"
              className={cn(buttonVariants({ size: "lg", variant: "outline" }), "h-11 px-6 text-base font-semibold")}>
              See Sample Leads
            </Link>
          </div>
        </div>
        <HeroLeadPreview />
      </section>

      {/* How it works strip */}
      <section aria-labelledby="home-how-heading" className="border-y border-neutral-200 bg-neutral-50">
        <h2 id="home-how-heading" className="pt-14 text-center text-2xl font-bold tracking-tight">
          How PermitTorch works
        </h2>
        <ol className="mx-auto grid max-w-6xl gap-8 px-4 py-12 sm:px-6 md:grid-cols-4">
          {HOW_IT_WORKS_STEPS.map((s) => (
            <li key={s.step}>
              <div aria-hidden="true" className="flex h-9 w-9 items-center justify-center rounded-full bg-orange-100 text-sm font-bold text-orange-700">
                {s.step}
              </div>
              <h3 className="mt-4 text-base font-semibold">{s.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{s.summary}</p>
            </li>
          ))}
        </ol>
        <div className="pb-12 text-center">
          <Link href="/how-it-works" className="text-sm font-medium text-orange-700 hover:text-orange-800">
            See how scoring works →
          </Link>
        </div>
      </section>

      {/* Value bullets (social-proof-free) */}
      <section className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
        <h2 className="text-center text-3xl font-bold tracking-tight">
          Why fire contractors use PermitTorch
        </h2>
        <div className="mt-12 grid gap-6 sm:grid-cols-2">
          {VALUE_BULLETS.map((b) => (
            <div key={b.title} className="rounded-xl border border-neutral-200 p-6">
              <h3 className="font-semibold">{b.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{b.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Lead magnet */}
      <section id="sample-leads" className="scroll-mt-20 border-y border-orange-100 bg-orange-50">
        <div className="mx-auto max-w-3xl px-4 py-20 sm:px-6">
          <h2 className="text-center text-3xl font-bold tracking-tight">
            See this week&apos;s hottest fire protection opportunities in your market
          </h2>
          <p className="mx-auto mt-4 max-w-xl text-center text-neutral-600">
            Tell us where you work and we&apos;ll email you {SAMPLE_LEADS_PROMISE}. Free — see
            the product before you sign up.
          </p>
          <div className="mt-10">
            <SampleLeadsForm markets={markets} />
          </div>
        </div>
      </section>

      {/* Closing CTA */}
      <section className="mx-auto max-w-6xl px-4 py-20 text-center sm:px-6">
        <h2 className="text-3xl font-bold tracking-tight">Get new opportunities every morning.</h2>
        <p className="mx-auto mt-4 max-w-xl text-neutral-600">
          Your competitors are refreshing permit portals by hand — or not looking at all.
          Be the first call on the jobs worth winning.
        </p>
        <Link href="/signup"
          className={ctaClasses("lg", "mt-8")}>
          Start Free
        </Link>
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(organizationJsonLd())} />
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(websiteJsonLd())} />
    </>
  );
}
