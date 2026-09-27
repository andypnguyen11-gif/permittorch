// Product analytics (PRD §49). Every call is a silent no-op until initAnalytics()
// has run in a browser with NEXT_PUBLIC_POSTHOG_KEY set — local dev, CI and
// tests have no key, so nothing is ever sent from them. posthog-js is loaded
// lazily, only when a key is configured, so it never weighs on first load
// (marketing pages are SEO surfaces); calls made while it loads are queued.
export type AnalyticsEvent =
  | "signup"
  | "pricing_viewed"
  | "lead_opened"
  | "lead_saved"
  | "search_performed"
  | "filter_changed"
  | "digest_enabled"
  | "checkout_started";

export type EventProps = {
  signup: Record<string, never>;
  pricing_viewed: Record<string, never>;
  lead_opened: { leadId: string; score: number; category: string };
  lead_saved: { leadId: string };
  search_performed: { query: string };
  filter_changed: { filter: string; value: string };
  digest_enabled: { frequency: "DAILY" | "WEEKLY" };
  checkout_started: { plan: "STARTER" | "PRO" | "TERRITORY" };
};

type PostHog = typeof import("posthog-js").default;

const DEFAULT_HOST = "https://us.i.posthog.com";
let client: PostHog | null = null;
let loading: Promise<void> | null = null;
let queue: Array<(ph: PostHog) => void> = [];

export function initAnalytics(): void {
  if (loading || typeof window === "undefined") return;
  const key = process.env.NEXT_PUBLIC_POSTHOG_KEY;
  if (!key) return;
  loading = import("posthog-js")
    .then(({ default: posthog }) => {
      posthog.init(key, {
        api_host: process.env.NEXT_PUBLIC_POSTHOG_HOST || DEFAULT_HOST,
        capture_pageview: true,
        capture_pageleave: true,
        person_profiles: "identified_only",
      });
      client = posthog;
      for (const call of queue) call(posthog);
    })
    .catch(() => {
      // Blocked by an ad blocker or offline: analytics must never break the app.
    })
    .finally(() => { queue = []; });
}

function withClient(call: (ph: PostHog) => void): void {
  if (client) call(client);
  else if (loading) queue.push(call);
}

/**
 * Ties later events to a user by their opaque internal id only. Never pass
 * email, name or any other personal data: PostHog receives the id and nothing else.
 */
export function identifyUser(userId: string): void {
  withClient((ph) => ph.identify(userId));
}

/** Forget the identified user (sign-out) so the next visitor on this browser is anonymous. */
export function resetAnalyticsUser(): void {
  withClient((ph) => ph.reset());
}

export function track<E extends AnalyticsEvent>(event: E, props: EventProps[E]): void {
  withClient((ph) => ph.capture(event, props));
}
