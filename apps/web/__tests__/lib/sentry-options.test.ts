import { afterEach, describe, expect, it, vi } from "vitest";
import { sentryOptions } from "@/sentry.options";

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
