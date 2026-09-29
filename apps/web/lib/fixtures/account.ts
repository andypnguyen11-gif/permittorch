import type { AccountMe, Market } from "@permittorch/types";
import { mockMarkets } from "./markets";

type Role = AccountMe["role"];
const ROLES: Role[] = ["MEMBER", "ADMIN", "SUPER_ADMIN"];

/**
 * Mock-mode role switch: NEXT_PUBLIC_MOCK_ROLE=MEMBER|ADMIN|SUPER_ADMIN
 * (default SUPER_ADMIN) so the member view can be checked without an API.
 * Read at call time, so a changed env (or a test stub) applies immediately.
 */
export function mockRole(): Role {
  const role = process.env.NEXT_PUBLIC_MOCK_ROLE?.trim().toUpperCase();
  return ROLES.includes(role as Role) ? (role as Role) : "SUPER_ADMIN";
}

/** The mock account as the current NEXT_PUBLIC_MOCK_ROLE sees it. */
export function mockAccountForRole(): AccountMe {
  return { ...mockAccountMe, role: mockRole() };
}

export const mockAccountMe: AccountMe = {
  id: "0b7c5c3e-2f4a-4e1d-9a61-5d2c6f1e8a10",
  email: "john@davisfireprotection.com",
  role: "SUPER_ADMIN",
  organizationName: "Davis Fire Protection",
  plan: "PRO",
  digestFrequency: "DAILY",
  hasLiveSubscription: true,
  termsAccepted: true,
};

// Markets this mock organization is entitled to: a subset of the single market
// fixture (./markets, WS3) — entitlements come from the subscription, so the
// org holds only some of the public markets. Slugs match the cities in ./leads
// so market filtering stays consistent.
export const MOCK_ENTITLED_SLUGS = ["houston-tx", "dallas-tx"] as const;

export const mockAccountMarkets: Market[] = MOCK_ENTITLED_SLUGS.map((slug) => {
  const market = mockMarkets.find((m) => m.slug === slug);
  if (!market) throw new Error(`Entitled mock market missing from ./markets: ${slug}`);
  return market;
});
