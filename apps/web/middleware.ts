import * as Sentry from "@sentry/nextjs";
import { NextResponse, type NextRequest } from "next/server";
import { authMiddleware, redirectToLogin } from "next-firebase-auth-edge";
import { APP_HOME, LOGIN_PATH, authConfig, isAuthPage, isPublicPath } from "@/lib/auth/config";
import { parsePlanTier } from "@/components/app/account/plan-selection";

// FINAL FORM (WS0; WS5 added the missing-key guard and Sentry reporting in handleError).
// Public routes are locked in the master contracts doc (see lib/auth/config.ts);
// everything else (the /app dashboard surface) requires a valid Firebase session
// cookie. /api/login and /api/logout are served by authMiddleware itself.
let warnedMissingKeys = false;

export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // next-firebase-auth-edge throws on every request without signature keys
  // (a 500 even on public pages). A misconfigured deploy should degrade to
  // "nobody is signed in" instead: public pages render, /app bounces to /login.
  if (authConfig.cookieSignatureKeys.length === 0) {
    if (!warnedMissingKeys) {
      warnedMissingKeys = true;
      const message =
        "[auth] AUTH_COOKIE_SIGNATURE_KEY_CURRENT/PREVIOUS are not set — treating every request as signed out.";
      console.warn(message);
      // A misconfigured deploy must be visible in error tracking, not just the logs (no-op without a DSN).
      Sentry.captureMessage(message, "error");
    }
    if (isPublicPath(pathname)) return NextResponse.next();
    return redirectToLogin(request, { path: LOGIN_PATH, publicPaths: [] });
  }

  return authMiddleware(request, {
    loginPath: "/api/login",
    logoutPath: "/api/logout",
    ...authConfig,
    handleValidToken: async (_tokens, headers) => {
      if (isAuthPage(pathname)) {
        return NextResponse.redirect(new URL(signedInDestination(request), request.url));
      }
      return NextResponse.next({ request: { headers } });
    },
    handleInvalidToken: async () => {
      if (isPublicPath(pathname)) return NextResponse.next();
      return redirectToLogin(request, { path: LOGIN_PATH, publicPaths: [] });
    },
    handleError: async (error) => {
      // Token verification failures (bad keys, wrong project, clock skew) would otherwise
      // vanish into the redirect below. No-op while Sentry is disabled (no DSN).
      Sentry.captureException(error);
      if (isPublicPath(pathname)) return NextResponse.next();
      return redirectToLogin(request, { path: LOGIN_PATH, publicPaths: [] });
    },
  });
}

/**
 * Where a signed-in visitor to /login or /signup goes: a valid `?plan=` (from /pricing) continues
 * to the account page's plan picker with that plan preselected; anything else lands on the app.
 */
export function signedInDestination(request: NextRequest): string {
  const plan = parsePlanTier(request.nextUrl.searchParams.get("plan"));
  return plan ? `/app/account?plan=${plan}` : APP_HOME;
}

export const config = {
  matcher: [
    "/api/login",
    "/api/logout",
    // Run on every page except Next.js internals and static assets.
    "/((?!_next|favicon.ico|.*\\..*).*)",
    // The catch-all above excludes any path containing a dot, so dashboard
    // routes like /app/leads/abc.json would skip the session check. Force
    // the whole /app surface through the middleware explicitly.
    "/app/:path*",
  ],
};
