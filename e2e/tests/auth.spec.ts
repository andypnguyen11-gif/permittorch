import { expect, test, type Page } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { requireFirebaseSignup, requireUsers } from "../helpers/env";
import { deleteFirebaseUserByEmail } from "../helpers/firebase-admin";

const PASSWORD = "E2e-Sup3r-Secret!42";

async function signUp(page: Page, path: string, email: string) {
  await page.goto(path);
  await expect(page.getByRole("heading", { level: 1, name: "Create your PermitTorch account" })).toBeVisible();
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Create account", exact: true }).click();
}

test.describe("sign-up (creates a real Firebase account)", () => {
  requireFirebaseSignup();
  const created: string[] = [];
  test.afterAll(async () => {
    for (const email of created) await deleteFirebaseUserByEmail(email);
  });

  test("email/password sign-up lands on /app/leads with a working session", async ({ page }) => {
    const email = `e2e-signup-${Date.now()}@permittorch.dev`;
    created.push(email);
    await signUp(page, "/signup", email);
    await page.waitForURL("**/app/leads", { timeout: 30_000 });
    await expect(page.getByRole("heading", { level: 1, name: "Leads" })).toBeVisible();
    // A brand-new account has no subscription, so no market data is visible.
    await expect(page.getByText("No leads match these filters")).toBeVisible();
  });

  test("sign-up from a pricing CTA carries the plan to the account checkout picker", async ({ page }) => {
    const email = `e2e-signup-plan-${Date.now()}@permittorch.dev`;
    created.push(email);
    await signUp(page, "/signup?plan=TERRITORY", email);
    await page.waitForURL("**/app/account?plan=TERRITORY", { timeout: 30_000 });
    await expect(page.getByRole("radio", { name: "Territory" })).toBeChecked();
  });
});

test.describe("sign-in", () => {
  requireUsers("entitled");

  test("wrong password shows a non-enumerating error and stays on /login", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel("Email", { exact: true }).fill(USERS.entitled.email);
    await page.getByLabel("Password", { exact: true }).fill(`${USERS.entitled.password}-wrong`);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page.locator("form").getByRole("alert")).toHaveText("Incorrect email or password.");
    expect(new URL(page.url()).pathname).toBe("/login");
  });

  test("valid credentials land on /app/leads; sign-out returns to /login and re-locks /app", async ({ page }) => {
    await signIn(page, USERS.entitled);
    await page.getByRole("button", { name: "Account menu" }).click();
    await page.getByRole("menuitem", { name: "Sign out" }).click();
    await page.waitForURL("**/login");
    await page.goto("/app/leads");
    await page.waitForURL(/\/login\?redirect=%2Fapp%2Fleads/);
  });

  test("a bounced deep link returns to its page after sign-in", async ({ page }) => {
    await page.goto("/app/saved");
    await page.waitForURL(/\/login\?redirect=%2Fapp%2Fsaved/);
    await page.getByLabel("Email", { exact: true }).fill(USERS.entitled.email);
    await page.getByLabel("Password", { exact: true }).fill(USERS.entitled.password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await page.waitForURL("**/app/saved", { timeout: 30_000 });
    await expect(page.getByRole("heading", { level: 1, name: "Saved leads" })).toBeVisible();
  });
});
