import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { TERMS_UPDATED, TERMS_VERSION } from "@/lib/terms";
import { LegalDocument, type LegalSection } from "@/components/marketing/legal-document";

export const metadata: Metadata = buildMetadata({
  title: "Privacy Policy",
  description: "What PermitTorch collects, how we use it, what we show about people named in public permit records, and how to ask us to remove your details.",
  path: "/privacy",
});

// Every sentence here describes what the product does. Change the product and this page
// together, and change TERMS_VERSION with them.
const SECTIONS: LegalSection[] = [
  {
    heading: "1. What we collect from customers",
    body: [
      "Account details — name, email, and company — collected when you create an account. Accounts use Firebase Authentication with email/password or Google sign-in.",
      "Billing information, processed by Stripe. We never store your card number; Stripe handles payment details directly.",
      "Sample-lead requests — name, work email, company, and market — when you request free sample leads from our marketing pages.",
      "Product usage analytics, collected through PostHog, to understand how the product is used and to improve it.",
      "A record of the version of our terms you agreed to, and when.",
    ],
  },
  {
    heading: "2. Permit records and the people named in them",
    body: [
      "PermitTorch shows building and fire permit records that government offices publish. A record can name the owner of the property, the applicant and the contractor. Some records also publish a phone number, an email address or a contractor license number for them.",
      "We take this information only from the government record. We do not look anyone up anywhere else, buy data about them, or add to what the record publishes.",
      "A lead shows the address of the property the permit is for. We do not show a separate mailing address for anyone named on it.",
      "Phone numbers, email addresses and license numbers are shown only to signed-in customers, for the markets on their plan, and in the files those customers export. They are never on a public page.",
      "We do not call, text or email the people named in permit records. Our customers are fire-protection contractors. Our terms require them to follow the laws on sales calls, texts and email, and to stop contacting anyone who asks.",
      "For every permit record we retain the source URL, the issuing jurisdiction, and the timestamp we retrieved it. We do not guarantee the accuracy or completeness of this data. Records may be delayed, amended, or corrected by the jurisdictions that publish them, and PermitTorch reflects those changes as we detect them.",
    ],
  },
  {
    heading: "3. If you are named in a permit record",
    body: [
      "You can ask us to remove your phone number, your email address, your name, or a whole record from PermitTorch. You do not have to give a reason, and it costs nothing.",
      "Email support@permittorch.com. Tell us the city, the permit number or the property address, and what you want removed. We use what you send only to find the record and to handle your request.",
      "We will act on your request within 30 days. If a law gives you a right to faster removal, for example because you are a judge or a law enforcement officer, tell us and we will act within the time that law sets.",
      "To keep it removed, we keep what you asked us to remove on a private list that our daily import checks. Customers never see that list.",
      "Removal has limits. The record stays on the website of the government office that published it, and that office has its own rules. We cannot take back a file that a customer downloaded before your request.",
      "If one of our customers contacts you and you want it to stop, tell them. You can also tell us who contacted you and when. We look into every complaint, and we can suspend or close the account of a customer who breaks our terms.",
    ],
  },
  {
    heading: "4. How we use customers’ information",
    body: [
      "We use the information we collect from customers to operate the service, send the digests and sample leads they request, process payments, and improve the product.",
      "We do not sell our customers’ personal information. Permit records are different: access to them is what our customers pay for, as section 2 explains.",
    ],
  },
  {
    heading: "5. Service providers",
    body: [
      "We share information only with the service providers that help us run PermitTorch, and only what each one needs to do its job: Firebase Authentication for sign-in (email/password or Google), Stripe for billing, Resend for transactional and digest email, PostHog for analytics, and our hosting providers.",
    ],
  },
  {
    heading: "6. Retention and deletion",
    body: [
      "We keep your account data for as long as your account is active. To delete your account and the personal data associated with it, email support@permittorch.com.",
    ],
  },
  {
    heading: "7. Contact",
    body: [
      "Questions about this policy: support@permittorch.com.",
    ],
  },
];

export default function PrivacyPage() {
  return <LegalDocument title="Privacy Policy" updated={TERMS_UPDATED} version={TERMS_VERSION} sections={SECTIONS} />;
}
