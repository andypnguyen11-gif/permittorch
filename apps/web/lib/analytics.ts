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
  // Length only: search text can contain addresses, names or other personal data.
  search_performed: { queryLength: number };
  filter_changed: { filter: string; value: string };
  digest_enabled: { frequency: "DAILY" | "WEEKLY" };
  checkout_started: { plan: "STARTER" | "PRO" | "TERRITORY" };
};

type PostHog = typeof import("posthog-js").default;

const DEFAULT_HOST = "https://us.i.posthog.com";
type PostHogConfig = Parameters<PostHog["init"]>[1];
type CaptureResult = import("posthog-js").CaptureResult;

/** Query parameters that may carry search text, personal data or credentials — never sent. */
const SENSITIVE_PARAMS = new Set(["q", "email", "token", "t"]);
const isSensitiveParam = (name: string) => SENSITIVE_PARAMS.has(name.toLowerCase()) || /token/i.test(name);

/** Removes sensitive query parameters from an absolute or relative URL; other values pass through. */
export function sanitizeUrl(value: string): string {
  const q = value.indexOf("?");
  if (q === -1) return value;
  const hash = value.indexOf("#", q);
  const base = value.slice(0, q);
  const query = value.slice(q + 1, hash === -1 ? undefined : hash);
  const fragment = hash === -1 ? "" : value.slice(hash);
  const params = new URLSearchParams(query);
  for (const name of [...params.keys()]) if (isSensitiveParam(name)) params.delete(name);
  const rest = params.toString();
  return `${base}${rest ? `?${rest}` : ""}${fragment}`;
}

// $current_url, $pathname, $referrer and their $initial_/$session_entry_ variants.
const isUrlProperty = (key: string) => /url|pathname|referrer/i.test(key);

function sanitizeProperties<T extends Record<string, unknown> | undefined>(props: T): T {
  if (!props) return props;
  const out: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(props))
    out[key] = typeof value === "string" && isUrlProperty(key) ? sanitizeUrl(value) : value;
  return out as T;
}

/** PostHog `before_send`: scrubs URL-bearing properties on every event and person update. */
export function sanitizeEvent(event: CaptureResult | null): CaptureResult | null {
  if (!event) return event;
  return {
    ...event,
    properties: sanitizeProperties(event.properties),
    $set: sanitizeProperties(event.$set),
    $set_once: sanitizeProperties(event.$set_once),
  };
}

/**
 * PostHog options: explicit events and page views only. Autocapture, heatmaps, dead-click
 * capture and session recording would ship element text and screen contents (lead addresses,
 * contractor names, emails), so they stay off; URLs are scrubbed before sending.
 */
export function posthogOptions(): Partial<PostHogConfig> {
  return {
    api_host: process.env.NEXT_PUBLIC_POSTHOG_HOST || DEFAULT_HOST,
    autocapture: false,
    capture_heatmaps: false,
    capture_dead_clicks: false,
    disable_session_recording: true,
    capture_pageview: true,
    capture_pageleave: true,
    person_profiles: "identified_only",
    before_send: sanitizeEvent,
  };
}

let client: PostHog | null = null;
let loading: Promise<void> | null = null;
let queue: Array<(ph: PostHog) => void> = [];

export function initAnalytics(): void {
  if (loading || typeof window === "undefined") return;
  const key = process.env.NEXT_PUBLIC_POSTHOG_KEY;
  if (!key) return;
  loading = import("posthog-js")
    .then(({ default: posthog }) => {
      posthog.init(key, posthogOptions());
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
