// FINAL FORM (WS0). Do not edit in any workstream.
// Shared next-firebase-auth-edge options + the LOCKED public-route list.
// Server-only: the service-account private key must never reach the browser.
import "server-only";

const PUBLIC_PATH_PATTERNS: RegExp[] = [
  /^\/$/,
  /^\/pricing$/,
  /^\/how-it-works$/,
  /^\/fire-protection-leads(\/.*)?$/,
  /^\/fire-sprinkler-leads$/,
  /^\/fire-alarm-leads$/,
  /^\/locations(\/.*)?$/,
  /^\/blog(\/.*)?$/,
  /^\/terms$/,
  /^\/privacy$/,
  /^\/login(\/.*)?$/,
  /^\/signup(\/.*)?$/,
  /^\/api(\/.*)?$/,
];

export function isPublicPath(pathname: string): boolean {
  return PUBLIC_PATH_PATTERNS.some((pattern) => pattern.test(pathname));
}

/** Pages a signed-in user should be redirected away from. */
export function isAuthPage(pathname: string): boolean {
  return pathname === "/login" || pathname === "/signup";
}

export const AUTH_COOKIE_NAME = "AuthToken";
export const APP_HOME = "/app/leads";
export const LOGIN_PATH = "/login";

function env(name: string): string {
  return process.env[name] ?? "";
}

// Options shared by middleware.ts (authMiddleware) and server components (getTokens).
export const authConfig = {
  apiKey: env("NEXT_PUBLIC_FIREBASE_API_KEY"),
  cookieName: AUTH_COOKIE_NAME,
  cookieSignatureKeys: [
    env("AUTH_COOKIE_SIGNATURE_KEY_CURRENT"),
    env("AUTH_COOKIE_SIGNATURE_KEY_PREVIOUS"),
  ].filter((key) => key.length > 0),
  cookieSerializeOptions: {
    path: "/",
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    maxAge: 12 * 60 * 60 * 24, // 12 days
  },
  serviceAccount: {
    projectId: env("FIREBASE_PROJECT_ID"),
    clientEmail: env("FIREBASE_CLIENT_EMAIL"),
    // Railway/Vercel store the key with literal "\n" sequences.
    privateKey: env("FIREBASE_PRIVATE_KEY").replace(/\\n/g, "\n"),
  },
};
