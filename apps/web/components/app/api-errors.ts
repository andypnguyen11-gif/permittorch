import { notFound, redirect } from "next/navigation";
import { ApiError } from "@/lib/api";

/**
 * Server-component error policy for API calls: an expired/invalid session (401)
 * goes back to /login; a 404 (e.g. a lead outside the user's entitled markets)
 * renders the not-found page. Anything else bubbles to app/app/error.tsx.
 */
export function handleApiError(err: unknown, { notFoundOn404 = false } = {}): never {
  if (err instanceof ApiError && err.status === 401) redirect("/login");
  if (notFoundOn404 && err instanceof ApiError && err.status === 404) notFound();
  throw err;
}
