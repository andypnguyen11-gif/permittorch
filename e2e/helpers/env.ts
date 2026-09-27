import { test } from "@playwright/test";

export const API_URL = (
  process.env.E2E_API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5050"
).replace(/\/$/, "");

const USER_VARS = {
  entitled: ["E2E_ENTITLED_FIREBASE_UID", "E2E_USER_PASSWORD"],
  unentitled: ["E2E_UNENTITLED_FIREBASE_UID", "E2E_USER_PASSWORD"],
  superadmin: ["SUPERADMIN_FIREBASE_UID", "E2E_USER_PASSWORD"],
} as const;

export type UserKey = keyof typeof USER_VARS;

const missing = (names: readonly string[]) => names.filter((n) => !process.env[n]?.trim());

/**
 * Skips the current test/describe unless the seeded Firebase identities exist.
 * They need the Firebase console step (Email/Password enabled), `pnpm e2e:create-users`,
 * the printed UIDs + E2E_USER_PASSWORD in .env, and a re-run of the API seeder.
 */
export function requireUsers(...users: UserKey[]): void {
  const names = [...new Set(users.flatMap((u) => USER_VARS[u]))];
  const absent = missing(names);
  test.skip(
    absent.length > 0,
    `needs seeded Firebase test users — set ${absent.join(", ")} (see e2e/README.md)`,
  );
}

/** Sign-up creates a real Firebase account: only when Email/Password auth is enabled. */
export function requireFirebaseSignup(): void {
  test.skip(
    missing(["E2E_USER_PASSWORD"]).length > 0,
    "needs Firebase Email/Password sign-in enabled — set E2E_USER_PASSWORD once it is (see e2e/README.md)",
  );
}

/** True for a real Stripe test-mode key (sk_test_… or rk_test_…), not a placeholder. */
export function isStripeTestKey(key: string | undefined): boolean {
  return !!key && /^(sk|rk)_test_[A-Za-z0-9]{20,}$/.test(key) && !/replace|placeholder/i.test(key);
}

export function requireStripeTestKey(): void {
  test.skip(
    !isStripeTestKey(process.env.STRIPE_SECRET_KEY),
    "needs a real Stripe test-mode STRIPE_SECRET_KEY (rk_test_… / sk_test_…) in .env and in the running API",
  );
}
