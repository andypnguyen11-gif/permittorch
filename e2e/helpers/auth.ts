import { expect, type Page } from "@playwright/test";

export interface TestUser {
  email: string;
  password: string;
}

const password = process.env.E2E_USER_PASSWORD ?? "";

// Emails default to the seeder's (apps/api/Data/Seed/DevSeeder.cs) and .env.example values;
// scripts/create-users.mjs creates the Firebase accounts with these same addresses.
export const USERS = {
  entitled: { email: process.env.E2E_ENTITLED_EMAIL || "e2e-entitled@permittorch.dev", password },
  unentitled: { email: process.env.E2E_UNENTITLED_EMAIL || "e2e-unentitled@permittorch.dev", password },
  superadmin: { email: process.env.SUPERADMIN_EMAIL || "e2e-admin@permittorch.dev", password },
} satisfies Record<string, TestUser>;

/**
 * Signs in through the app's own /login form (components/app/auth/login-form.tsx):
 * Firebase email/password → POST /api/login session cookie → router.push("/app/leads").
 */
export async function signIn(page: Page, user: TestUser): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(user.email);
  // exact: the show/hide toggle is labelled "Show password".
  await page.getByLabel("Password", { exact: true }).fill(user.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  // Fail fast with the form's own error (wrong password, provider disabled…) instead of a
  // 30-second navigation timeout.
  const formError = page.locator("form").getByRole("alert");
  const outcome = await Promise.race([
    page.waitForURL("**/app/leads", { timeout: 30_000 }).then(() => "ok" as const),
    formError.waitFor({ state: "visible", timeout: 30_000 }).then(() => "error" as const),
  ]);
  if (outcome === "error") {
    throw new Error(`Sign-in as ${user.email} failed on /login: "${await formError.textContent()}"`);
  }
  await agreeToTermsIfAsked(page);
  await expect(page.getByRole("heading", { level: 1, name: "Leads" })).toBeVisible();
}

const termsGate = (page: Page) =>
  page.getByRole("heading", { level: 1, name: "Agree to the terms to continue" });

/**
 * Agrees on the agreement screen that a new account gets in place of the app
 * (components/app/terms-gate.tsx). The agreement is stored, so a user is asked once per
 * version of the terms.
 */
export async function agreeToTerms(page: Page): Promise<void> {
  await expect(termsGate(page)).toBeVisible();
  await page.getByRole("checkbox", { name: /I have read and agree to the Terms of Service/ }).check();
  await page.getByRole("button", { name: "Agree and continue" }).click();
  await expect(termsGate(page)).toBeHidden();
}

/** For a seeded user, who is asked only on the first sign-in after the terms change. */
export async function agreeToTermsIfAsked(page: Page): Promise<void> {
  const leads = page.getByRole("heading", { level: 1, name: "Leads", exact: true });
  await expect(termsGate(page).or(leads)).toBeVisible();
  if (await termsGate(page).isVisible()) await agreeToTerms(page);
}
