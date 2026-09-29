import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { TERMS_UPDATED, TERMS_VERSION } from "@/lib/terms";
import { LegalDocument, type LegalSection } from "@/components/marketing/legal-document";

export const metadata: Metadata = buildMetadata({
  title: "Terms of Service",
  description: "The terms that govern your use of PermitTorch, including how we source public permit data, what you must do before contacting anyone, and what we do and do not guarantee.",
  path: "/terms",
});

// A change to this wording is a new version of the terms: change TERMS_VERSION and the API's
// Terms.CurrentVersion with it, so that every user is asked to agree again.
const SECTIONS: LegalSection[] = [
  {
    heading: "1. The service",
    body: [
      "PermitTorch is a lead-intelligence service for fire-protection contractors. We monitor publicly available building permit and inspection records published by government jurisdictions, classify fire-related activity, and present it as scored opportunities.",
      "The service is for businesses. It is not for personal, family or household use.",
    ],
  },
  {
    heading: "2. Agreeing to these terms",
    body: [
      "You agree to these terms when you tick the box on the agreement screen and choose “Agree and continue”. They apply for as long as you use the service. We keep a record of the version you agreed to and the time.",
      "If you use PermitTorch for a company or another organization, you agree on its behalf and confirm that you have the authority to do so. “You” then means that organization and everyone who uses the service through its account.",
      "If you do not agree, do not use the service.",
    ],
  },
  {
    heading: "3. Where the data comes from",
    body: [
      "All permit and inspection data in PermitTorch is collected from publicly available government records. For every record we retain the source URL, the issuing jurisdiction, and the time we retrieved it.",
      "PermitTorch does not create, alter, or certify government records. We organize and score what jurisdictions publish.",
      "Some records name the owner, the applicant or the contractor on a permit, and some publish a phone number, an email address or a license number for them. We show these as the record publishes them. We do not look anyone up anywhere else.",
      "We do not check whether a phone number or an email address is current, whether it belongs to the person or company named, whether it is a business line or a personal one, or whether it is on a do-not-call list.",
    ],
  },
  {
    heading: "4. No guarantee of accuracy",
    body: [
      "PermitTorch does not guarantee the accuracy, completeness, or timeliness of any record. Jurisdictions may publish records late, amend them, or remove them. Lead scores are our own estimates of sales relevance — they are not a representation about any project, property, or party.",
      "You are responsible for independently verifying any opportunity before acting on it, including confirming permit status directly with the issuing jurisdiction.",
    ],
  },
  {
    heading: "5. Contacting people named in permit records",
    body: [
      "You decide whether and how to contact anyone you find through PermitTorch, and you alone are responsible for doing it lawfully. We do not contact anyone for you, and we do not advise you on the law.",
      "A lead gives you no permission to contact anyone. No person or company named in a permit record has agreed to be called, texted or emailed by you or by us. Being named in a public record is not consent.",
      "You must follow every law that applies to your calls, texts, emails and visits. In the United States these include the Telephone Consumer Protection Act, the Telemarketing Sales Rule, the National Do Not Call Registry, the CAN-SPAM Act, and state laws on telemarketing, text messages and do-not-call lists. Some state laws are stricter than the federal ones.",
      "In particular, you must:",
      {
        list: [
          "Treat every phone number as one that may be a personal or mobile number, unless you have checked for yourself that it is not.",
          "Not use an automatic dialing system, a prerecorded or artificial voice, or automated or bulk text messages to reach a number you got from PermitTorch, unless you have yourself obtained from that person the consent the law requires.",
          "Check the National Do Not Call Registry, and any state list that applies, before a sales call, and not call a number listed there unless the law allows it.",
          "Keep your own do-not-contact list. When a person asks you to stop, stop, and do not contact them again.",
          "Say truthfully who you are and why you are getting in touch. Do not say or suggest that you are from a government office, that a permit requires your services, or that PermitTorch or any jurisdiction recommends you.",
          "Make every sales email truthful in its sender details and its subject line, identify it as an advertisement, include your postal address, and offer a working way to opt out. Honor an opt-out within 10 business days.",
        ],
      },
      "If we tell you that a person has asked to be removed from PermitTorch, you must stop contacting that person and delete their details from every file you exported, within 10 days.",
    ],
  },
  {
    heading: "6. Uses that are not allowed",
    body: [
      "Use the data only to find and pursue work for your own business. Do not use it to harass, threaten or stalk anyone, to misrepresent your relationship to a project, or to build a profile of a person.",
      "PermitTorch is not a consumer reporting agency, and nothing in the service is a consumer report under the Fair Credit Reporting Act. Do not use the data, in whole or in part, as a factor in deciding anyone’s eligibility for credit, insurance, employment or housing, or for any other purpose that Act covers.",
      "Do not scrape, crawl, or bulk-export the service beyond the export features your plan provides, and do not attempt to access markets or records outside your entitlement.",
      "Do not share exported data outside your organization, resell it, or republish it.",
    ],
  },
  {
    heading: "7. Accounts and subscriptions",
    body: [
      "Paid plans are billed monthly in advance through Stripe. You may cancel at any time from your billing portal; access continues through the end of the paid period. Fees are non-refundable except where required by law.",
      "Your subscription entitles the users on your organization to the markets on your plan.",
    ],
  },
  {
    heading: "8. Complaints, suspension and closing an account",
    body: [
      "If we receive a complaint about how you contacted someone, or have reason to think you have broken section 5 or section 6, we may ask you about it, limit or switch off your exports, suspend your account, or close it. You must answer our questions within 5 business days.",
      "We may act without notice where we think it is needed to protect a person or to follow the law. We may give information about your use of the service to a regulator, a court or a law enforcement agency where the law requires or permits it.",
      "Fees already paid are not refunded when we close an account for breaking these terms, except where required by law.",
    ],
  },
  {
    heading: "9. Claims that arise from your use",
    body: [
      "You agree to indemnify, defend and hold harmless PermitTorch and its owners, employees and contractors against any claim, demand, fine, penalty, loss or cost, including reasonable attorneys’ fees, that arises from:",
      {
        list: [
          "how you, or anyone using your account, contacted any person or used the data;",
          "your breaking these terms; or",
          "your breaking any law.",
        ],
      },
      "This applies whether the claim is brought by a person you contacted, by a regulator, or by anyone else.",
    ],
  },
  {
    heading: "10. No warranty",
    body: [
      {
        notice: "THE SERVICE AND ALL DATA IN IT ARE PROVIDED “AS IS” AND “AS AVAILABLE”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, ACCURACY AND NON-INFRINGEMENT. WE DO NOT WARRANT THAT ANY PERSON OR COMPANY NAMED IN THE DATA MAY LAWFULLY BE CONTACTED.",
      },
    ],
  },
  {
    heading: "11. Limitation of liability",
    body: [
      {
        notice: "TO THE MAXIMUM EXTENT PERMITTED BY LAW, PERMITTORCH IS NOT LIABLE FOR LOST PROFITS, LOST BIDS, LOST DATA, OR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL OR PUNITIVE DAMAGES, OR FOR DECISIONS MADE IN RELIANCE ON THE DATA. OUR TOTAL LIABILITY FOR ALL CLAIMS IS LIMITED TO THE AMOUNT YOU PAID US IN THE TWELVE MONTHS BEFORE THE CLAIM.",
      },
    ],
  },
  {
    heading: "12. Changes",
    body: [
      "We may update these terms as the product and the law change. When a change is material we will email account holders, or ask you to agree to the new version when you next sign in, before it applies to you.",
    ],
  },
  {
    heading: "13. General",
    body: [
      "If a court finds part of these terms unenforceable, the rest still applies. If we do not enforce a term, we have not given it up. These terms and the Privacy Policy are the whole agreement between you and PermitTorch about the service.",
      "Sections 5, 6, 8, 9, 10 and 11 continue to apply after your account closes.",
    ],
  },
  {
    heading: "14. Contact",
    body: [
      "Questions about these terms: support@permittorch.com.",
    ],
  },
];

export default function TermsPage() {
  return <LegalDocument title="Terms of Service" updated={TERMS_UPDATED} version={TERMS_VERSION} sections={SECTIONS} />;
}
