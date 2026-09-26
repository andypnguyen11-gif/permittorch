import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";

export const metadata: Metadata = buildMetadata({
  title: "Privacy Policy — PermitTorch",
  description: "What PermitTorch collects, how we use it, and how public-record permit data fits in.",
  path: "/privacy",
});

const SECTIONS: { heading: string; body: string[] }[] = [
  {
    heading: "1. What we collect",
    body: [
      "Account details — name, email, and company — collected when you sign in through Firebase Authentication (Google).",
      "Billing information, processed by Stripe. We never store your card number; Stripe handles payment details directly.",
      "Sample-lead requests — name, work email, company, and market — when you request free sample leads from our marketing pages.",
      "Product usage analytics, collected through PostHog, to understand how the product is used and to improve it.",
    ],
  },
  {
    heading: "2. Permit data is public-record data",
    body: [
      "The leads shown in PermitTorch describe properties and permits published by government jurisdictions — they are not personal information about our users.",
      "For every permit record we retain the source URL, the issuing jurisdiction, and the timestamp we retrieved it. We do not guarantee the accuracy or completeness of this data, and you must verify any opportunity independently before acting on it.",
      "Records may be delayed, amended, or corrected by the jurisdictions that publish them, and PermitTorch reflects those changes as we detect them.",
    ],
  },
  {
    heading: "3. How we use your information",
    body: [
      "We use the information we collect to operate the service, send you the digests and sample leads you request, process payments, and improve the product.",
      "We do not sell your personal information.",
    ],
  },
  {
    heading: "4. Service providers",
    body: [
      "We share information only with the service providers that help us run PermitTorch, and only what each one needs to do its job: Firebase Authentication (Google) for sign-in, Stripe for billing, Resend for transactional and digest email, PostHog for analytics, and our hosting providers.",
    ],
  },
  {
    heading: "5. Retention and deletion",
    body: [
      "We keep your account data for as long as your account is active. To delete your account and the personal data associated with it, email support@permittorch.com.",
    ],
  },
  {
    heading: "6. Contact",
    body: [
      "Questions about this policy: support@permittorch.com.",
    ],
  },
];

export default function PrivacyPage() {
  return (
    <article className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight">Privacy Policy</h1>
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
