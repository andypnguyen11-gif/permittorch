import type { Metadata } from "next";
import { buildMetadata, jsonLd } from "@/lib/seo";
import { TrackOnMount } from "@/components/app/track-on-mount";
import { PricingTiers, pricingProductJsonLd } from "@/components/marketing/pricing-tiers";
import { FaqAccordion, faqPageJsonLd } from "@/components/marketing/faq-accordion";
import { PRICING_FAQ } from "@/components/marketing/pricing-faq";
import { FreePilotOffer } from "@/components/marketing/free-pilot-offer";
import { isFreePilot } from "@/lib/free-pilot";

export const metadata: Metadata = buildMetadata({
  title: "Pricing for Fire Protection Lead Plans",
  description: isFreePilot()
    ? "PermitTorch is free during the pilot: every market, the email digest and CSV export, with no card needed."
    : "Plans for fire protection contractors: Starter $49/mo, Pro $129/mo with daily updates and CSV export, Territory $249/mo for up to 5 markets. Cancel anytime.",
  path: "/pricing",
});

export default function PricingPage() {
  if (isFreePilot()) {
    // No prices, trial terms or billing FAQ: none of them apply while the pilot is free.
    return (
      <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
        <TrackOnMount event="pricing_viewed" props={{}} />
        <h1 className="text-center text-4xl font-bold tracking-tight">Free during the pilot</h1>
        <p className="mx-auto mt-4 max-w-xl text-center text-neutral-600">
          PermitTorch is free while we run the pilot. Create an account to see leads in every market we
          cover. No card is needed.
        </p>
        <div className="mt-14">
          <FreePilotOffer />
        </div>
      </div>
    );
  }
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <TrackOnMount event="pricing_viewed" props={{}} />
      <h1 className="text-center text-4xl font-bold tracking-tight">
        Simple pricing. Real leads.
      </h1>
      <p className="mx-auto mt-4 max-w-xl text-center text-neutral-600">
        Every plan is month to month and starts with a 7-day free trial when you subscribe
        at checkout (card required; cancel anytime before it ends).
      </p>
      <div className="mt-14">
        <PricingTiers />
      </div>

      <section className="mx-auto mt-24 max-w-2xl">
        <h2 className="text-center text-2xl font-bold tracking-tight">Pricing questions</h2>
        <FaqAccordion items={PRICING_FAQ} className="mt-8" />
      </section>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(faqPageJsonLd(PRICING_FAQ))} />
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(pricingProductJsonLd())} />
    </div>
  );
}
