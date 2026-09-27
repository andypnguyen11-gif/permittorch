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
    a.identifyUser("uid-1", "a@b.test");
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

  it("identifies by Firebase uid and resets on sign-out", async () => {
    vi.stubEnv("NEXT_PUBLIC_POSTHOG_KEY", "phc_test");
    const a = await load();
    a.initAnalytics();
    await settle();
    a.identifyUser("uid-1", "a@b.test");
    a.identifyUser("uid-2");
    a.resetAnalyticsUser();
    expect(posthog.identify).toHaveBeenNthCalledWith(1, "uid-1", { email: "a@b.test" });
    expect(posthog.identify).toHaveBeenNthCalledWith(2, "uid-2", undefined);
    expect(posthog.reset).toHaveBeenCalledTimes(1);
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
    expect(posthog.identify).toHaveBeenCalledWith("uid-1", undefined);
    expect(posthog.init.mock.invocationCallOrder[0]).toBeLessThan(posthog.capture.mock.invocationCallOrder[0]);
  });
});
