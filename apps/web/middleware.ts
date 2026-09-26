import { NextResponse, type NextRequest } from "next/server";
import { authMiddleware, redirectToLogin } from "next-firebase-auth-edge";
import { APP_HOME, LOGIN_PATH, authConfig, isAuthPage, isPublicPath } from "@/lib/auth/config";

// FINAL FORM (WS0; WS5 added the missing-key guard).
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
      console.warn(
        "[auth] AUTH_COOKIE_SIGNATURE_KEY_CURRENT/PREVIOUS are not set — treating every request as signed out.",
      );
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
        return NextResponse.redirect(new URL(APP_HOME, request.url));
      }
      return NextResponse.next({ request: { headers } });
    },
    handleInvalidToken: async () => {
      if (isPublicPath(pathname)) return NextResponse.next();
      return redirectToLogin(request, { path: LOGIN_PATH, publicPaths: [] });
    },
    handleError: async () => {
      if (isPublicPath(pathname)) return NextResponse.next();
      return redirectToLogin(request, { path: LOGIN_PATH, publicPaths: [] });
    },
  });
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
