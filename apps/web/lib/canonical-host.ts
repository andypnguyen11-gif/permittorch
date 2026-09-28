// One public hostname. Session cookies are per host and the API allows a single browser
// origin (WEB_ORIGIN), so www must never serve the app itself: it redirects to the apex,
// which is also the host used by canonical links and the sitemap (SITE_URL).
//
// Imported by next.config.ts, so this module must stay free of "@/" alias imports.
import { SITE_URL } from "./seo";

export const CANONICAL_HOST = new URL(SITE_URL).host;
export const WWW_HOST = `www.${CANONICAL_HOST}`;

export interface HostRedirect {
  source: string;
  has: { type: "host"; value: string }[];
  destination: string;
  permanent: boolean;
}

/** Next.js treats a `has` value as an anchored regular expression, so dots are escaped. */
function escapeForHostPattern(host: string): string {
  return host.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** next.config.ts `redirects()`: every path on www goes to the same path on the apex. */
export function canonicalHostRedirects(): HostRedirect[] {
  return [
    {
      source: "/:path*",
      has: [{ type: "host", value: escapeForHostPattern(WWW_HOST) }],
      destination: `${SITE_URL}/:path*`,
      permanent: true,
    },
  ];
}
