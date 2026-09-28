import { expect, test } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { requireUsers } from "../helpers/env";
import { AUSTIN_SOURCE_NAME, SAN_ANTONIO_SOURCE_NAME } from "../helpers/seed";

// The API enforces SuperAdmin on /api/admin/* (401 without a token: public-boundary.spec.ts;
// 403 for members: xUnit). These specs cover the dashboard's server-side gate and navigation.
test.describe("admin area", () => {
  test.describe("SuperAdmin", () => {
    requireUsers("superadmin");

    test("sees source health for the seeded registry", async ({ page }) => {
      await signIn(page, USERS.superadmin);
      await page.getByRole("link", { name: "Sources", exact: true }).click();
      await page.waitForURL("**/app/admin/sources");
      await expect(page.getByRole("heading", { level: 1, name: "Sources" })).toBeVisible();
      await expect(page.getByRole("list", { name: "Health summary" })).toBeVisible();
      await expect(page.getByText(AUSTIN_SOURCE_NAME, { exact: true })).toBeVisible();
      await expect(page.getByText(SAN_ANTONIO_SOURCE_NAME, { exact: true })).toBeVisible();
      // All 44 registry sources are listed, whatever their current health.
      await expect(page.locator("main table tbody tr")).toHaveCount(44);
    });

    test("every admin page renders", async ({ page }) => {
      await signIn(page, USERS.superadmin);
      for (const [path, heading] of [
        ["/app/admin/runs", "Scraper runs"], ["/app/admin/users", "Users"],
        ["/app/admin/subscriptions", "Subscriptions"],
      ] as const) {
        await page.goto(path);
        await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
      }
    });
  });

  test.describe("member", () => {
    requireUsers("entitled");

    test("has no admin navigation and is redirected away from admin pages", async ({ page }) => {
      await signIn(page, USERS.entitled); // Role: Member
      await expect(page.getByRole("link", { name: "Runs", exact: true })).toHaveCount(0);

      for (const path of ["/app/admin/sources", "/app/admin/runs", "/app/admin/users", "/app/admin/subscriptions"]) {
        await page.goto(path);
        await page.waitForURL((url) => url.pathname === "/app");
        await expect(page.getByText(AUSTIN_SOURCE_NAME, { exact: true })).toHaveCount(0);
      }
    });
  });
});
