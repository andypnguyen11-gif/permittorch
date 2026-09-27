// Client-side session helpers shared by the account menu, the session-recovery
// shell, and every mutation caller.
import { signOut } from "firebase/auth";
import { toast } from "sonner";
import { ApiError } from "@/lib/api";
import { resetAnalyticsUser } from "@/lib/analytics";
import { firebaseAuth } from "@/lib/firebase/client";

/**
 * Clears the httpOnly session cookie FIRST (so the middleware stops treating
 * the browser as signed in), then the Firebase client session, then does a
 * full-page replace to /login. Replacing (not router.push) drops the dashboard
 * from history and from the client router cache.
 */
export async function signOutAndRedirect(): Promise<void> {
  const res = await fetch("/api/logout");
  if (!res.ok) throw new Error(`Sign-out failed with status ${res.status}`);
  await signOut(firebaseAuth);
  resetAnalyticsUser();
  window.location.replace("/login");
}

/** Thrown by useApiToken() when there is no signed-in Firebase user to mint a token. */
export class SessionExpiredError extends Error {
  constructor() {
    super("Your session expired");
    this.name = "SessionExpiredError";
  }
}

export function isSessionError(err: unknown): boolean {
  return err instanceof SessionExpiredError || (err instanceof ApiError && err.status === 401);
}

let expiring: Promise<void> | null = null;

/** Tells the user their session expired and signs them out (once, even if several calls fail together). */
export function expireSession(): Promise<void> {
  expiring ??= (async () => {
    toast.error("Your session expired. Please sign in again.");
    try {
      await signOutAndRedirect();
    } catch {
      toast.error("Could not sign out. Please try again.");
    } finally {
      expiring = null;
    }
  })();
  return expiring;
}

/** Mutation error policy: a lost session signs the user out; anything else shows `message`. */
export function reportMutationError(err: unknown, message: string): void {
  if (isSessionError(err)) void expireSession();
  else toast.error(message);
}
