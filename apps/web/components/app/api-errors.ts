import { notFound } from "next/navigation";
import { ApiError } from "@/lib/api";
import { isUnauthorized, markSessionExpired } from "@/components/app/session-digest";

/**
 * Server-component error policy for API calls:
 * - 401: never redirect to /login from here. The session cookie may still be
 *   valid to the middleware (which would bounce /login straight back to /app →
 *   ERR_TOO_MANY_REDIRECTS). Instead the error is tagged and rethrown, and
 *   app/app/error.tsx renders the recoverable "couldn't verify your session"
 *   state whose button clears the cookie and signs out.
 * - 404 (opt-in, e.g. a lead outside the user's markets): the not-found page.
 * - Anything else bubbles to app/app/error.tsx.
 */
export function handleApiError(err: unknown, { notFoundOn404 = false } = {}): never {
  if (isUnauthorized(err)) throw markSessionExpired(err);
  if (notFoundOn404 && err instanceof ApiError && err.status === 404) notFound();
  throw err;
}
