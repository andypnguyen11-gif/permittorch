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
  };
}
