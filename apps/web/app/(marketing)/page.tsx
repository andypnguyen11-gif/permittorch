import Link from "next/link";
import type { Metadata } from "next";
import { getMarkets } from "@/lib/api";
import { buildMetadata } from "@/lib/seo";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";
import { HOW_IT_WORKS_STEPS } from "@/components/marketing/how-it-works-steps";

export const metadata: Metadata = buildMetadata({
  title: "PermitTorch — Fire Protection Leads From Public Permit Data",
  description:
    "PermitTorch monitors public permit and inspection records and identifies fire-protection opportunities before they disappear into another spreadsheet.",
  path: "/",
});

const VALUE_BULLETS = [
  {
    title: "Built only for fire protection",
    body: "No plumbing permits, no roofing noise. Sprinkler, alarm, suppression, kitchen systems, and inspections — that is the whole feed.",
  },
  {
    title: "Every score is explained",
    body: "A 91 is a 91 for reasons you can read: new commercial build, sprinkler scope detected, filed this week, no fire contractor listed.",
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
  const markets = await getMarkets();
  return (
    <>
      {/* Hero */}
      <section className="mx-auto max-w-6xl px-4 pb-20 pt-24 text-center sm:px-6">
        <h1 className="mx-auto max-w-3xl text-4xl font-bold tracking-tight sm:text-6xl">
          Find the permits worth chasing<span className="text-orange-500">.</span>
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-lg leading-relaxed text-neutral-600">
          PermitTorch monitors public permit and inspection records and identifies
          fire-protection opportunities before they disappear into another spreadsheet.
        </p>
        <div className="mt-10 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Link href="/signup"
            className={cn(buttonVariants({ size: "lg" }), "bg-orange-500 px-8 text-white hover:bg-orange-600")}>
            Find Leads in Your Market
          </Link>
          <Link href="#sample-leads" className={cn(buttonVariants({ size: "lg", variant: "outline" }), "px-8")}>
            See Sample Leads
          </Link>
        </div>
      </section>

      {/* How it works strip */}
      <section className="border-y border-neutral-200 bg-neutral-50">
        <div className="mx-auto grid max-w-6xl gap-8 px-4 py-16 sm:px-6 md:grid-cols-4">
          {HOW_IT_WORKS_STEPS.map((s) => (
            <div key={s.step}>
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-orange-100 text-sm font-bold text-orange-600">
                {s.step}
              </div>
              <h2 className="mt-4 text-base font-semibold">{s.title}</h2>
              <p className="mt-2 text-sm leading-relaxed text-neutral-600">{s.summary}</p>
            </div>
          ))}
        </div>
        <div className="pb-12 text-center">
          <Link href="/how-it-works" className="text-sm font-medium text-orange-600 hover:text-orange-700">
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
            Tell us where you work and we&apos;ll email you 5–10 real, scored opportunities
            PermitTorch found there recently. Free — see the product before you sign up.
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
          className={cn(buttonVariants({ size: "lg" }), "mt-8 bg-orange-500 px-8 text-white hover:bg-orange-600")}>
          Start Free
        </Link>
      </section>
    </>
  );
}
