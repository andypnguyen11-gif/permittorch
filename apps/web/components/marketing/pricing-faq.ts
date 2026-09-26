import type { FaqEntry } from "@/components/marketing/faq-accordion";

export const PRICING_FAQ: FaqEntry[] = [
  {
    q: "How does billing work?",
    a: "Plans are billed monthly through Stripe. You can upgrade or downgrade at any time, and Stripe prorates the difference automatically. Need a different market? Contact support and we'll move your subscription. No setup fees, no annual contracts.",
  },
  {
    q: "Is there a free trial?",
    a: "Every paid plan starts with a 7-day free trial when you subscribe at checkout (card required; cancel anytime before it ends). You can also request free sample leads from your market without creating an account.",
  },
  {
    q: "What counts as a market?",
    a: "A market is a metro area PermitTorch actively covers — for example Austin, TX. Starter and Pro include one market of your choice; Territory covers up to five. We only sell markets where we have live data coverage, and we add new ones as coverage comes online.",
  },
  {
    q: "Can I cancel anytime?",
    a: "Yes. Cancel in two clicks from your billing portal — no phone call, no retention script. You keep full access through the end of your current billing period, and there are no cancellation fees.",
  },
];
