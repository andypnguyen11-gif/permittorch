import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

// .env.example is the deploy checklist: every variable in master §9 (plus the
// WS5 seeder/E2E identities) must be listed, and it must never hold real secrets.
const text = readFileSync(path.resolve(__dirname, "../../../../.env.example"), "utf8");
const assignments = new Map<string, string>();
for (const line of text.split("\n")) {
  const m = line.match(/^#?\s*([A-Z][A-Za-z0-9_]*)=(.*)$/);
  if (m) assignments.set(m[1], m[2].trim());
}

const REQUIRED = [
  "DATABASE_URL", "RUN_MIGRATIONS_ON_STARTUP", "APIFY_TOKEN", "APIFY_TASK_ID", "FIREBASE_PROJECT_ID",
  "STRIPE_SECRET_KEY", "STRIPE_WEBHOOK_SECRET", "STRIPE_PRICE_STARTER", "STRIPE_PRICE_PRO", "STRIPE_PRICE_TERRITORY",
  "RESEND_API_KEY", "EMAIL_FROM", "EMAIL_UNSUBSCRIBE_SECRET", "SENTRY_DSN", "NEXT_PUBLIC_SENTRY_DSN",
  "WEB_ORIGIN", "API_PUBLIC_URL", "ForwardedHeaders__ForwardLimit", "Pipeline__Enabled",
  "NEXT_PUBLIC_API_URL", "NEXT_PUBLIC_API_MOCK", "NEXT_PUBLIC_MOCK_ROLE",
  "NEXT_PUBLIC_FIREBASE_API_KEY", "NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN", "NEXT_PUBLIC_FIREBASE_PROJECT_ID",
  "NEXT_PUBLIC_FIREBASE_APP_ID", "FIREBASE_CLIENT_EMAIL", "FIREBASE_PRIVATE_KEY",
  "AUTH_COOKIE_SIGNATURE_KEY_CURRENT", "AUTH_COOKIE_SIGNATURE_KEY_PREVIOUS", "NEXT_PUBLIC_POSTHOG_KEY",
  "SEED_SAMPLE_DATA", "SEED_E2E_IDENTITIES", "SEED_SUPERADMIN_FIREBASE_UID", "SEED_SUPERADMIN_EMAIL",
  "SUPERADMIN_FIREBASE_UID", "SUPERADMIN_EMAIL", "E2E_ENTITLED_FIREBASE_UID", "E2E_ENTITLED_EMAIL",
  "E2E_UNENTITLED_FIREBASE_UID", "E2E_UNENTITLED_EMAIL", "E2E_USER_PASSWORD",
];

describe(".env.example", () => {
  it.each(REQUIRED)("lists %s", (name) => {
    expect(assignments.has(name)).toBe(true);
  });

  it("keeps optional overrides commented out", () => {
    for (const name of ["ForwardedHeaders__ForwardLimit", "Pipeline__Enabled", "NEXT_PUBLIC_API_MOCK", "NEXT_PUBLIC_MOCK_ROLE"])
      expect(text).toMatch(new RegExp(`^#\\s*${name}=`, "m"));
  });

  it("leaves the seeder opt-ins off so a copied .env never seeds samples or test identities", () => {
    for (const name of ["SEED_SAMPLE_DATA", "SEED_E2E_IDENTITIES", "SEED_SUPERADMIN_FIREBASE_UID"])
      expect(assignments.get(name)).toBe("");
  });

  it("contains no real secrets", () => {
    expect(text).not.toMatch(/\b(sk|rk)_(live|test)_[A-Za-z0-9]{8,}/);
    expect(text).not.toMatch(/whsec_[A-Za-z0-9]{8,}/);
    expect(text).not.toMatch(/\bre_[A-Za-z0-9]{12,}/);
    expect(text).not.toMatch(/apify_api_[A-Za-z0-9]+/);
    expect(text).not.toMatch(/BEGIN PRIVATE KEY/);
    expect(text).not.toMatch(/phc_[A-Za-z0-9]{8,}/);
    for (const name of ["STRIPE_SECRET_KEY", "STRIPE_WEBHOOK_SECRET", "RESEND_API_KEY", "EMAIL_UNSUBSCRIBE_SECRET",
      "FIREBASE_PRIVATE_KEY", "AUTH_COOKIE_SIGNATURE_KEY_CURRENT", "AUTH_COOKIE_SIGNATURE_KEY_PREVIOUS", "APIFY_TOKEN", "E2E_USER_PASSWORD"])
      expect(assignments.get(name)).toMatch(/^(|replace|\w+_replace)$/);
  });
});
