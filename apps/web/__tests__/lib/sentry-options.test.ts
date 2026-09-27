import { afterEach, describe, expect, it, vi } from "vitest";
import type { ErrorEvent, Event } from "@sentry/nextjs";
import { scrubEvent, sentryOptions } from "@/sentry.options";

afterEach(() => vi.unstubAllEnvs());

describe("sentryOptions", () => {
  it("disables Sentry when NEXT_PUBLIC_SENTRY_DSN is empty (local dev and CI)", () => {
    vi.stubEnv("NEXT_PUBLIC_SENTRY_DSN", "");
    expect(sentryOptions()).toMatchObject({ enabled: false, dsn: undefined });
  });

  it("enables Sentry with the configured DSN and without default PII", () => {
    vi.stubEnv("NEXT_PUBLIC_SENTRY_DSN", "https://key@o1.ingest.sentry.io/1");
    expect(sentryOptions()).toMatchObject({
      enabled: true, dsn: "https://key@o1.ingest.sentry.io/1", sendDefaultPii: false,
    });
  });
});

describe("Sentry scrubbing", () => {
  it("wires the scrubber into beforeSend and beforeSendTransaction", () => {
    const options = sentryOptions();
    const event = { type: undefined, request: { headers: { Authorization: "Bearer x" } } } as unknown as ErrorEvent;
    expect(options.beforeSend(event).request?.headers).toEqual({});
    const tx = { type: "transaction", request: { cookies: { AuthToken: "c" } } } as unknown as Event;
    expect(options.beforeSendTransaction(tx).request?.cookies).toBeUndefined();
  });

  it("strips auth and cookie headers and request cookies, keeping harmless headers", () => {
    const out = scrubEvent({
      request: {
        headers: { Authorization: "Bearer eyJ", cookie: "AuthToken=abc", "Set-Cookie": "a=b", "user-agent": "UA" },
        cookies: { AuthToken: "abc" },
      },
    } as Event);
    expect(out.request?.headers).toEqual({ "user-agent": "UA" });
    expect(out.request?.cookies).toBeUndefined();
  });

  it.each([
    ["k=sub&id=1&t=abcdef", true],
    ["idToken=x", true],
    ["token=x", true],
    ["q=warehouse&market=austin-tx", false],
    ["tab=2", false],
  ])("drops credential query strings (%s → dropped: %s)", (query, dropped) => {
    const out = scrubEvent({ request: { query_string: query, url: `https://api.test/api/x?${query}` } } as Event);
    expect(out.request?.query_string).toBe(dropped ? undefined : query);
    expect(out.request?.url).toBe(dropped ? "https://api.test/api/x" : `https://api.test/api/x?${query}`);
  });

  it("scrubs credential URLs in breadcrumbs", () => {
    const out = scrubEvent({
      breadcrumbs: [
        { category: "fetch", data: { url: "https://api.test/api/email/unsubscribe?k=sub&id=1&t=sig" } },
        { category: "navigation", data: { from: "/login?token=abc", to: "/app/leads?q=x" } },
      ],
    } as Event);
    expect(out.breadcrumbs?.[0].data?.url).toBe("https://api.test/api/email/unsubscribe");
    expect(out.breadcrumbs?.[1].data).toEqual({ from: "/login", to: "/app/leads?q=x" });
  });

  it("handles object-form query strings", () => {
    const out = scrubEvent({ request: { query_string: { t: "sig" } } } as unknown as Event);
    expect(out.request?.query_string).toBeUndefined();
  });
});
