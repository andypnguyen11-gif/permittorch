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
  await expect(page.getByRole("heading", { level: 1, name: "Leads" })).toBeVisible();
}
