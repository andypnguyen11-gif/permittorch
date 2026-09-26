// Server components only.
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { getTokens } from "next-firebase-auth-edge";
import { authConfig } from "@/lib/auth/config";

export async function getApiToken(): Promise<string> {
  // Read cookies first, even in mock mode: it opts every /app page into dynamic
  // rendering, so mock data (and "Updated N minutes ago") is never frozen at build time.
  const cookieStore = await cookies();
  if (process.env.NEXT_PUBLIC_API_MOCK === "1") return "mock-token";
  const tokens = await getTokens(cookieStore, authConfig);
  // middleware already gates /app/:path*, but a missing/expired session here
  // (e.g. mid-request cookie expiry) must still bounce to /login rather than
  // call the API with an empty bearer token.
  if (!tokens) redirect("/login");
  return tokens.token;
}
