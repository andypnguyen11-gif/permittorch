// Resolves the API base URL. NEXT_PUBLIC_API_URL is inlined at build time, so a production
// bundle built without it would silently call http://localhost:5000 from every browser.
// Production refuses that: next.config.ts fails the build, and apiFetch throws.

export const DEV_API_URL = "http://localhost:5000";

export interface ApiUrlEnv {
  NEXT_PUBLIC_API_URL?: string;
  NEXT_PUBLIC_API_MOCK?: string;
  NODE_ENV?: string;
}

export const MISSING_API_URL_MESSAGE =
  "NEXT_PUBLIC_API_URL is required for production builds (set it as a build variable; NEXT_PUBLIC_API_MOCK=1 is the only exception)";

export function resolveApiBaseUrl(env: ApiUrlEnv): string {
  const configured = env.NEXT_PUBLIC_API_URL?.trim();
  if (configured) return configured;
  if (env.NODE_ENV === "production") throw new Error(MISSING_API_URL_MESSAGE);
  return DEV_API_URL;
}

/** Build-time guard for next.config.ts: a production build must know its API unless it is a mock build. */
export function assertApiUrlConfigured(env: ApiUrlEnv): void {
  if (env.NODE_ENV !== "production" || env.NEXT_PUBLIC_API_MOCK === "1") return;
  if (!env.NEXT_PUBLIC_API_URL?.trim()) throw new Error(MISSING_API_URL_MESSAGE);
}
