import { signOut } from "firebase/auth";
import { firebaseAuth } from "@/lib/firebase/client";
import { parsePlanTier } from "@/components/app/account/plan-selection";

// Exchanges a Firebase ID token for the httpOnly session cookie that
// middleware.ts (next-firebase-auth-edge) checks on every /app request.
export async function startSession(idToken: string): Promise<void> {
  const res = await fetch("/api/login", {
    method: "POST",
    headers: { Authorization: `Bearer ${idToken}` },
  });
  if (!res.ok) throw Object.assign(new Error("Session request failed"), { code: "session/failed" });
}

/**
 * startSession, but if the cookie exchange fails the Firebase client session is
 * signed out too — otherwise the browser is half signed in (Firebase user, no
 * session cookie) and the dashboard would reject every request.
 */
export async function startSessionOrSignOut(idToken: string): Promise<void> {
  try {
    await startSession(idToken);
  } catch (err) {
    await signOut(firebaseAuth).catch(() => undefined);
    throw err;
  }
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

/**
 * Where to go after creating an account: a valid ?plan= (from /pricing's
 * /signup?plan=… links) lands on the account page with that plan preselected
 * in the checkout picker; otherwise the normal post-sign-in target.
 */
export function postSignupTarget(): string {
  if (typeof window === "undefined") return DEFAULT_TARGET;
  const plan = parsePlanTier(new URLSearchParams(window.location.search).get("plan"));
  if (plan) return `/app/account?plan=${plan}`;
  return postSignInTarget();
}
