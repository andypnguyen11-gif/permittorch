import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const posthog = { init: vi.fn(), capture: vi.fn(), identify: vi.fn(), reset: vi.fn() };
vi.mock("posthog-js", () => ({ default: posthog }));

// lib/analytics keeps module-level init state: import a fresh copy per test.
const load = () => import("@/lib/analytics");
// posthog-js is imported lazily inside initAnalytics(); let that import settle.
const settle = () => vi.dynamicImportSettled();

beforeEach(() => {
  vi.resetModules();
  vi.clearAllMocks();
});
afterEach(() => vi.unstubAllEnvs());

describe("analytics wrapper without a PostHog key", () => {
  it("never initialises PostHog and drops every call", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "");
    const a = await load();
    a.initAnalytics();
    a.track("lead_saved", { leadId: "x" });
    a.identifyUser("uid-1");
    a.resetAnalyticsUser();
    await settle();
    expect(posthog.init).not.toHaveBeenCalled();
    expect(posthog.capture).not.toHaveBeenCalled();
    expect(posthog.identify).not.toHaveBeenCalled();
    expect(posthog.reset).not.toHaveBeenCalled();
  });

  it("is a no-op before initAnalytics even when a key is configured", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    const a = await load();
    a.track("pricing_viewed", {});
    expect(posthog.capture).not.toHaveBeenCalled();
  });
});

describe("analytics wrapper with a PostHog key", () => {
  it("initialises once with the default host and forwards events unchanged", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    const a = await load();
    a.initAnalytics();
    a.initAnalytics();
    await settle();
    expect(posthog.init).toHaveBeenCalledTimes(1);
    expect(posthog.init).toHaveBeenCalledWith("phc_test",
      expect.objectContaining({ api_host: "https://us.i.posthog.com", capture_pageview: true }));

    a.track("lead_saved", { leadId: "x" });
    a.track("checkout_started", { plan: "PRO" });
    expect(posthog.capture).toHaveBeenNthCalledWith(1, "lead_saved", { leadId: "x" });
    expect(posthog.capture).toHaveBeenNthCalledWith(2, "checkout_started", { plan: "PRO" });
  });

  it("honours NEXT_PUBLIC_POSTHOG_HOST", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_HOST", "https://eu.i.posthog.com");
    const a = await load();
    a.initAnalytics();
    await settle();
    expect(posthog.init).toHaveBeenCalledWith("phc_test", expect.objectContaining({ api_host: "https://eu.i.posthog.com" }));
  });

  it("identifies by internal id only (no email, name or properties) and resets on sign-out", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    const a = await load();
    a.initAnalytics();
    await settle();
    a.identifyUser("uid-1");
    a.identifyUser("uid-2");
    a.resetAnalyticsUser();
    expect(posthog.identify).toHaveBeenCalledTimes(2);
    expect(posthog.identify.mock.calls[0]).toEqual(["uid-1"]);
    expect(posthog.identify.mock.calls[1]).toEqual(["uid-2"]);
    expect(posthog.reset).toHaveBeenCalledTimes(1);
  });
});

describe("PostHog privacy options", () => {
  it("disables autocapture and session recording and scrubs URLs before sending", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    const a = await load();
    a.initAnalytics();
    await settle();
    const options = posthog.init.mock.calls[0][1];
    expect(options).toMatchObject({
      autocapture: false, disable_session_recording: true, capture_pageview: true,
      capture_heatmaps: false, capture_dead_clicks: false,
    });
    expect(options.before_send).toBe(a.sanitizeEvent);
  });

  it.each([
    ["https://app.test/app/leads?q=123+Main+St&market=austin-tx", "https://app.test/app/leads?market=austin-tx"],
    ["/app/leads?q=smith&page=2#top", "/app/leads?page=2#top"],
    ["https://app.test/x?email=a%40b.c&token=abc&idToken=z&t=sig", "https://app.test/x"],
    ["https://app.test/pricing", "https://app.test/pricing"],
  ])("sanitizeUrl(%s) → %s", async (input, expected) => {
    const a = await load();
    expect(a.sanitizeUrl(input)).toBe(expected);
  });

  it("scrubs $current_url, $pathname, $referrer and person URL properties; leaves other props", async () => {
    const a = await load();
    const out = a.sanitizeEvent({
      uuid: "u", event: "$pageview",
      properties: {
        $current_url: "https://app.test/app/leads?q=secret", $pathname: "/app/leads?q=secret",
        $referrer: "https://app.test/app/leads?q=secret&market=x", leadId: "keep?q=me",
      },
      $set_once: { $initial_current_url: "https://app.test/?email=a@b.c" },
    });
    expect(out!.properties).toEqual({
      $current_url: "https://app.test/app/leads", $pathname: "/app/leads",
      $referrer: "https://app.test/app/leads?market=x", leadId: "keep?q=me",
    });
    expect(out!.$set_once).toEqual({ $initial_current_url: "https://app.test/" });
    expect(a.sanitizeEvent(null)).toBeNull();
  });
});

describe("analytics wrapper while posthog-js is loading", () => {
  it("queues calls made before the SDK arrives and flushes them in order", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    const a = await load();
    a.initAnalytics();
    a.track("pricing_viewed", {});
    a.identifyUser("uid-1");
    expect(posthog.capture).not.toHaveBeenCalled();
    await settle();
    expect(posthog.init).toHaveBeenCalledTimes(1);
    expect(posthog.capture).toHaveBeenCalledWith("pricing_viewed", {});
    expect(posthog.identify.mock.calls).toEqual([["uid-1"]]);
    expect(posthog.init.mock.invocationCallOrder[0]).toBeLessThan(posthog.capture.mock.invocationCallOrder[0]);
  });
});
