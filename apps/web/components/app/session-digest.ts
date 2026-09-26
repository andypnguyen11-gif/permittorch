import { ApiError } from "@/lib/api";

/**
 * Digest attached to 401 errors thrown from /app server components. Next.js
 * keeps an error's own `digest` when it redacts server errors for the client,
 * so app/app/error.tsx can tell "session expired" apart from other failures.
 */
export const SESSION_EXPIRED_DIGEST = "PERMITTORCH_SESSION_EXPIRED";

export function isUnauthorized(err: unknown): err is ApiError {
  return err instanceof ApiError && err.status === 401;
}

export function markSessionExpired<T extends object>(err: T): T & { digest: string } {
  return Object.assign(err, { digest: SESSION_EXPIRED_DIGEST });
}

export function sessionExpiredError(): ApiError & { digest: string } {
  return markSessionExpired(new ApiError("Your session expired", 401));
}
