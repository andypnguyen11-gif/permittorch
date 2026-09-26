import type { Metadata } from "next";
import { buildMetadata, jsonLd } from "@/lib/seo";
import { PricingTiers } from "@/components/marketing/pricing-tiers";
import {
  Accordion, AccordionContent, AccordionItem, AccordionTrigger,
} from "@/components/ui/accordion";

export const metadata: Metadata = buildMetadata({
  title: "Pricing — PermitTorch",
  description:
    "Plans for fire protection contractors: Starter $49/mo, Pro $129/mo with daily updates and CSV export, Territory $249/mo for up to 5 markets. Cancel anytime.",
  path: "/pricing",
});

export const PRICING_FAQ = [
  {
    q: "How does billing work?",
    a: "Plans are billed monthly through Stripe. You can upgrade, downgrade, or switch markets at any time, and Stripe prorates the difference automatically. No setup fees, no annual contracts.",
  },
  {
    q: "Is there a free trial?",
    a: "Yes. Every new account starts with a 7-day Pro trial in one market — you see real, fully scored leads before you're ever charged. You can also request free sample leads from any market without creating an account.",
  },
  {
    q: "What counts as a market?",
    a: "A market is a metro area PermitTorch actively covers — for example Houston, TX. Starter and Pro include one market of your choice; Territory covers up to five. We only sell markets where we have live data coverage, and we add new ones as coverage comes online.",
  },
  {
    q: "Can I cancel anytime?",
    a: "Yes. Cancel in two clicks from your billing portal — no phone call, no retention script. You keep full access through the end of your current billing period, and there are no cancellation fees.",
  },
];

export default function PricingPage() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="text-center text-4xl font-bold tracking-tight">
        Simple pricing. Real leads.
      </h1>
      <p className="mx-auto mt-4 max-w-xl text-center text-neutral-600">
        Every plan is month to month. Pick your market, start with a 7-day Pro trial,
        and cancel whenever you want.
      </p>
      <div className="mt-14">
        <PricingTiers />
      </div>

      <section className="mx-auto mt-24 max-w-2xl">
        <h2 className="text-center text-2xl font-bold tracking-tight">Pricing questions</h2>
        <Accordion className="mt-8">
          {PRICING_FAQ.map((f, i) => (
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
        mainEntity: PRICING_FAQ.map((f) => ({
          "@type": "Question",
          name: f.q,
          acceptedAnswer: { "@type": "Answer", text: f.a },
        })),
      })} />
    </div>
  );
}
