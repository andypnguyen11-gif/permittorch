import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import type { PlanTier } from "@permittorch/types";
import { cn } from "@/lib/utils";
import { SITE_NAME, SITE_URL } from "@/lib/seo";

export interface PricingTier {
  plan: PlanTier;
  name: string;
  price: number;          // USD per month
  blurb: string;
  /** Optional lead-in shown above the feature list, e.g. "Everything in Pro, plus:". */
  featuresIntro?: string;
  features: string[];
  highlighted: boolean;
  cta: string;
}

export const PRICING_TIERS: PricingTier[] = [
  {
    plan: "STARTER",
    name: "Starter",
    price: 49,
    blurb: "For a solo owner keeping an eye on one market.",
    features: ["1 market", "Weekly email digest", "Basic filtering", "Limited historical records"],
    highlighted: false,
    cta: "Choose Starter",
  },
  {
    plan: "PRO",
    name: "Pro",
    price: 129,
    blurb: "For contractors actively chasing new work every week.",
    features: [
      "1 market", "Daily updates", "Full lead scoring", "Saved opportunities",
      "Daily email digest", "CSV export", "30–90 days of history",
    ],
    highlighted: true,
    cta: "Start Free with Pro",
  },
  {
    plan: "TERRITORY",
    name: "Territory",
    price: 249,
    blurb: "For teams covering multiple metros.",
    featuresIntro: "Everything in Pro, plus:",
    features: [
      "Up to 5 markets", "Multiple users", "Advanced filters", "Daily alerts",
      "CSV export across all your markets", "Full historical records", "Priority support",
    ],
    highlighted: false,
    cta: "Choose Territory",
  },
];

export function signupHrefForPlan(plan: PlanTier): string {
  return `/signup?plan=${plan}`;
}

/** schema.org Product with one monthly USD Offer per tier (rendered on /pricing). */
export function pricingProductJsonLd() {
  return {
    "@context": "https://schema.org",
    "@type": "Product",
    name: `${SITE_NAME} subscription`,
    description: "Scored, explainable fire protection leads from public permit and inspection records.",
    brand: { "@type": "Brand", name: SITE_NAME },
    url: `${SITE_URL}/pricing`,
    offers: PRICING_TIERS.map((t) => ({
      "@type": "Offer",
      name: t.name,
      description: t.blurb,
      price: t.price.toFixed(2),
      priceCurrency: "USD",
      availability: "https://schema.org/InStock",
      url: `${SITE_URL}${signupHrefForPlan(t.plan)}`,
      priceSpecification: {
        "@type": "UnitPriceSpecification",
        price: t.price.toFixed(2),
        priceCurrency: "USD",
        unitCode: "MON",
        referenceQuantity: { "@type": "QuantitativeValue", value: 1, unitCode: "MON" },
      },
    })),
  };
}

function Check() {
  return (
    <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"
      className="mt-0.5 h-4 w-4 shrink-0 text-orange-500">
      <path fillRule="evenodd" clipRule="evenodd"
        d="M16.7 5.3a1 1 0 0 1 0 1.4l-7 7a1 1 0 0 1-1.4 0l-3-3a1 1 0 1 1 1.4-1.4L9 11.6l6.3-6.3a1 1 0 0 1 1.4 0Z" />
    </svg>
  );
}

export function PricingTiers() {
  return (
    <div className="grid gap-6 md:grid-cols-3">
      {PRICING_TIERS.map((t) => (
        <div key={t.name}
          className={
            t.highlighted
              ? "relative rounded-2xl border-2 border-orange-500 bg-white p-8 shadow-lg"
              : "rounded-2xl border border-neutral-200 bg-white p-8"
          }>
          {t.highlighted && (
            <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-orange-500 px-3 py-0.5 text-xs font-semibold text-white">
              Recommended
            </span>
          )}
          <h2 className="text-lg font-semibold">{t.name}</h2>
          <p className="mt-1 text-sm text-neutral-500">{t.blurb}</p>
          <p className="mt-5">
            <span className="text-4xl font-bold tracking-tight">${t.price}</span>
            <span className="text-neutral-500">/month</span>
          </p>
          {t.featuresIntro && (
            <p className="mt-6 text-sm font-semibold text-neutral-900">{t.featuresIntro}</p>
          )}
          <ul className={t.featuresIntro ? "mt-3 space-y-2.5" : "mt-6 space-y-2.5"}>
            {t.features.map((f) => (
              <li key={f} className="flex gap-2 text-sm text-neutral-700"><Check />{f}</li>
            ))}
          </ul>
          <Link href={signupHrefForPlan(t.plan)} className={cn(
            buttonVariants({ variant: t.highlighted ? "default" : "outline" }),
            "mt-8 w-full",
            t.highlighted && "bg-orange-500 text-white hover:bg-orange-600",
          )}>
            {t.cta}
          </Link>
        </div>
      ))}
    </div>
  );
}
