import type * as SentryModule from "@sentry/nextjs";
import { sentryOptions } from "./sentry.options";

// Browser error reporting. The SDK is only downloaded when NEXT_PUBLIC_SENTRY_DSN is
// set (it is inlined at build time, so builds without a DSN ship no Sentry client
// code on first load); errors thrown before it finishes loading are not reported.
let sentry: typeof SentryModule | null = null;

if (sentryOptions().enabled) {
  void import("@sentry/nextjs").then((Sentry) => {
    Sentry.init(sentryOptions());
    sentry = Sentry;
  });
}

export function onRouterTransitionStart(
  ...args: Parameters<typeof SentryModule.captureRouterTransitionStart>
): void {
  sentry?.captureRouterTransitionStart(...args);
}
