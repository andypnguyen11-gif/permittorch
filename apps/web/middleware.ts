import { NextResponse, type NextRequest } from "next/server";
import { authMiddleware, redirectToLogin } from "next-firebase-auth-edge";
import { APP_HOME, LOGIN_PATH, authConfig, isAuthPage, isPublicPath } from "@/lib/auth/config";

// FINAL FORM (WS0). Do not edit in any workstream.
// Public routes are locked in the master contracts doc (see lib/auth/config.ts);
// everything else (the /app dashboard surface) requires a valid Firebase session
// cookie. /api/login and /api/logout are served by authMiddleware itself.
export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

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
  ],
};
