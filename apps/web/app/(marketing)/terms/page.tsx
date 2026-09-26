import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";

export const metadata: Metadata = buildMetadata({
  title: "Terms of Service",
  description: "The terms that govern your use of PermitTorch, including how we source public permit data and what we do and do not guarantee.",
  path: "/terms",
});

const SECTIONS: { heading: string; body: string[] }[] = [
  {
    heading: "1. The service",
    body: [
      "PermitTorch is a lead-intelligence service for fire-protection contractors. We monitor publicly available building permit and inspection records published by government jurisdictions, classify fire-related activity, and present it as scored opportunities.",
    ],
  },
  {
    heading: "2. Where the data comes from",
    body: [
      "All permit and inspection data in PermitTorch is collected from publicly available government records. For every record we retain the source URL, the issuing jurisdiction, and the time we retrieved it.",
      "PermitTorch does not create, alter, or certify government records. We organize and score what jurisdictions publish.",
    ],
  },
  {
    heading: "3. No guarantee of accuracy",
    body: [
      "PermitTorch does not guarantee the accuracy, completeness, or timeliness of any record. Jurisdictions may publish records late, amend them, or remove them. Lead scores are our own estimates of sales relevance — they are not a representation about any project, property, or party.",
      "You are responsible for independently verifying any opportunity before acting on it, including confirming permit status directly with the issuing jurisdiction.",
    ],
  },
  {
    heading: "4. Accounts and subscriptions",
    body: [
      "Paid plans are billed monthly in advance through Stripe. You may cancel at any time from your billing portal; access continues through the end of the paid period. Fees are non-refundable except where required by law.",
      "Your subscription entitles the users on your organization to the markets on your plan. Sharing exported data outside your organization, reselling it, or republishing it in bulk is not permitted.",
    ],
  },
  {
    heading: "5. Acceptable use",
    body: [
      "Do not scrape, crawl, or bulk-export the service beyond the export features your plan provides; do not use the service to harass property owners or misrepresent your relationship to a project; do not attempt to access markets or records outside your entitlement.",
    ],
  },
  {
    heading: "6. Limitation of liability",
    body: [
      "The service is provided as-is. To the maximum extent permitted by law, PermitTorch is not liable for lost profits, lost bids, or decisions made in reliance on the data. Our total liability is limited to the amount you paid us in the twelve months before the claim.",
    ],
  },
  {
    heading: "7. Changes",
    body: [
      "We may update these terms as the product evolves. Material changes will be announced by email to account holders before they take effect. Questions: support@permittorch.com.",
    ],
  },
];

export default function TermsPage() {
  return (
    <article className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight">Terms of Service</h1>
      <p className="mt-2 text-sm text-neutral-500">Last updated August 19, 2026</p>
      {SECTIONS.map((s) => (
        <section key={s.heading} className="mt-8">
          <h2 className="text-xl font-semibold">{s.heading}</h2>
          {s.body.map((p) => (
            <p key={p.slice(0, 32)} className="mt-3 leading-relaxed text-neutral-700">{p}</p>
          ))}
        </section>
      ))}
    </article>
  );
}
