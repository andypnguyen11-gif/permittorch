// Exchanges a Firebase ID token for the httpOnly session cookie that
// middleware.ts (next-firebase-auth-edge) checks on every /app request.
export async function startSession(idToken: string): Promise<void> {
  const res = await fetch("/api/login", {
    method: "POST",
    headers: { Authorization: `Bearer ${idToken}` },
  });
  if (!res.ok) throw Object.assign(new Error("Session request failed"), { code: "session/failed" });
}

const DEFAULT_TARGET = "/app/leads";

/**
 * Where to go after sign-in: the middleware appends ?redirect=<path> when it
 * bounces an unauthenticated /app request. Only same-site /app paths are honored.
 */
export function postSignInTarget(): string {
  if (typeof window === "undefined") return DEFAULT_TARGET;
  const target = new URLSearchParams(window.location.search).get("redirect");
  if (target && /^\/app(\/|$|\?)/.test(target) && !target.startsWith("//")) return target;
  return DEFAULT_TARGET;
}
