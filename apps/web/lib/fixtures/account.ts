import type { AccountMe, Market } from "@permittorch/types";

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
  email: "john@davisfireprotection.com",
  role: "SUPER_ADMIN",
  organizationName: "Davis Fire Protection",
  plan: "PRO",
  digestFrequency: "DAILY",
};

// Markets this mock organization is entitled to. Kept here (not derived from
// WS3's ./markets) because entitlements come from the subscription, not the
// public market list — and so the dashboard has data before WS3's fixtures land.
// Slugs match the cities in ./leads so market filtering stays consistent.
export const mockAccountMarkets: Market[] = [
  { id: "mkt-001", name: "Houston, TX", city: "Houston", state: "TX", slug: "houston-tx" },
  { id: "mkt-002", name: "Dallas, TX", city: "Dallas", state: "TX", slug: "dallas-tx" },
];
