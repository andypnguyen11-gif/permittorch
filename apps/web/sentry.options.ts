import type { Event } from "@sentry/nextjs";

// Shared Sentry options for every runtime (browser, Node.js, edge). Reporting is
// disabled — and Sentry.init sends nothing — unless NEXT_PUBLIC_SENTRY_DSN is set,
// so local dev and CI stay silent.
export function sentryOptions() {
  const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;
  return {
    dsn: dsn || undefined,
    enabled: Boolean(dsn),
    tracesSampleRate: 0.1,
    sendDefaultPii: false,
    beforeSend: <T extends Event>(event: T) => scrubEvent(event),
    beforeSendTransaction: <T extends Event>(event: T) => scrubEvent(event),
  };
}

const SECRET_HEADERS = new Set(["authorization", "cookie", "set-cookie", "proxy-authorization"]);

/** True for a query string that carries a credential: `t=` (unsubscribe HMAC) or anything named *token*. */
export function hasCredentialQuery(query: string): boolean {
  return /(^|[?&])t=/i.test(query) || /token/i.test(query);
}

/** Drops a URL's query string when it carries a credential; other URLs pass through unchanged. */
export function scrubUrl(url: string): string {
  const q = url.indexOf("?");
  if (q === -1) return url;
  const hash = url.indexOf("#", q);
  const query = url.slice(q + 1, hash === -1 ? undefined : hash);
  return hasCredentialQuery(query) ? url.slice(0, q) + (hash === -1 ? "" : url.slice(hash)) : url;
}

/**
 * Last line of defence before anything leaves for Sentry (sendDefaultPii is already off):
 * removes auth/cookie headers, request cookies, and credential-bearing query strings from the
 * request and from breadcrumb URLs.
 */
export function scrubEvent<T extends Event>(event: T): T {
  const request = event.request;
  if (request) {
    if (request.headers) {
      request.headers = Object.fromEntries(
        Object.entries(request.headers).filter(([name]) => !SECRET_HEADERS.has(name.toLowerCase())));
    }
    delete request.cookies;
    if (request.query_string !== undefined) {
      const raw = typeof request.query_string === "string"
        ? request.query_string
        : new URLSearchParams(request.query_string as Record<string, string>).toString();
      if (hasCredentialQuery(raw)) delete request.query_string;
    }
    if (typeof request.url === "string") request.url = scrubUrl(request.url);
  }
  for (const crumb of event.breadcrumbs ?? []) {
    const data = crumb.data as Record<string, unknown> | undefined;
    if (!data) continue;
    for (const key of ["url", "from", "to"])
      if (typeof data[key] === "string") data[key] = scrubUrl(data[key] as string);
  }
  return event;
}
