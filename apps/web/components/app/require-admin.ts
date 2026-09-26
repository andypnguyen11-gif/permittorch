import { redirect } from "next/navigation";
import { getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";

// Server-side gate: web-side convenience only — the real enforcement is the
// API's SuperAdmin authorization on /api/admin/* (never trust UI hiding).
// API failures follow the shared policy: 401 → session-recovery screen, never a
// /login redirect loop.
export async function requireSuperAdmin(): Promise<string> {
  const token = await getApiToken();
  const me = await getAccountMe(token).catch((err) => handleApiError(err));
  if (me.role !== "SUPER_ADMIN") redirect("/app");
  return token;
}
