// Server components only.
import { cookies } from "next/headers";
import { getTokens } from "next-firebase-auth-edge";
import { authConfig } from "@/lib/auth/config";
import { sessionExpiredError } from "@/components/app/session-digest";

export async function getApiToken(): Promise<string> {
  // Read cookies first, even in mock mode: it opts every /app page into dynamic
  // rendering, so mock data (and "Updated N minutes ago") is never frozen at build time.
  const cookieStore = await cookies();
  if (process.env.NEXT_PUBLIC_API_MOCK === "1") return "mock-token";
  const tokens = await getTokens(cookieStore, authConfig);
  // middleware already gates /app/:path*, but a missing/expired session here
  // (e.g. mid-request cookie expiry) must never call the API with an empty
  // bearer. It surfaces as the recoverable session state (see api-errors.ts),
  // not a redirect to /login that the middleware could bounce back.
  if (!tokens) throw sessionExpiredError();
  return tokens.token;
}
