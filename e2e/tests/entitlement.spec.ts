import { expect, test } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { requireUsers } from "../helpers/env";
import { leadRows } from "../helpers/leads";
import { AUSTIN_LEAD_COUNT, AUSTIN_LEADS, OTHER_MARKET_LEADS } from "../helpers/seed";

// Entitlement is enforced by the API's queries; these specs prove the rendered app never shows
// data outside the user's markets (the API-level matrix is covered by xUnit integration tests).
test.describe("market entitlement", () => {
  test.describe("user without a subscription", () => {
    requireUsers("unentitled");

    test("sees an empty feed and locked markets, never seeded leads", async ({ page }) => {
      await signIn(page, USERS.unentitled);
      await expect(page.getByText("0 opportunities")).toBeVisible();
      await expect(page.getByText("No leads match these filters")).toBeVisible();
      await expect(page.getByText("Freshness unknown")).toBeVisible();
      await expect(leadRows(page)).toHaveCount(0);

      await page.goto("/app/markets");
      const austin = page.locator("main li", { has: page.getByRole("heading", { level: 2, name: "Austin" }) });
      await expect(austin.getByText("Locked")).toBeVisible();
      await expect(austin.getByText("Upgrade your plan to see fire-protection leads in Austin.")).toBeVisible();
      await expect(page.getByText("Included", { exact: true })).toHaveCount(0);
    });

    test("a direct link to a real lead renders not-found", async ({ page }) => {
      await signIn(page, USERS.unentitled);
      await page.goto(`/app/leads/${AUSTIN_LEADS.warehouse.id}`);
      await expect(page.getByText("We couldn’t find that")).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: AUSTIN_LEADS.warehouse.title })).toHaveCount(0);
    });
  });

  test.describe("user entitled to austin-tx only", () => {
    requireUsers("entitled");

    test("a San Antonio lead renders not-found (404, never a 403 that leaks existence)", async ({ page }) => {
      await signIn(page, USERS.entitled);
      await page.goto(`/app/leads/${OTHER_MARKET_LEADS.sanAntonioSprinkler.id}`);
      await expect(page.getByText("We couldn’t find that")).toBeVisible();
      await expect(page.getByText("This lead may be outside your markets, or it no longer exists.")).toBeVisible();
      await expect(page.getByText(OTHER_MARKET_LEADS.sanAntonioSprinkler.title)).toHaveCount(0);
    });

    test("the feed contains exactly the Austin rows, even when asking for another market", async ({ page }) => {
      await signIn(page, USERS.entitled);
      await expect(leadRows(page)).toHaveCount(AUSTIN_LEAD_COUNT);
      await expect(page.getByText(/San Antonio|Fort Worth/)).toHaveCount(0);

      // A hand-edited ?market= for a market outside the plan must not widen the query.
      await page.goto("/app/leads?market=san-antonio-tx");
      for (const lead of Object.values(OTHER_MARKET_LEADS)) {
        await expect(page.getByText(lead.title)).toHaveCount(0);
      }
      await page.goto("/app/leads?q=distribution%20center");
      await expect(leadRows(page)).toHaveCount(0);
    });

    test("markets page shows Austin included and other seeded markets locked", async ({ page }) => {
      await signIn(page, USERS.entitled);
      await page.goto("/app/markets");
      const card = (name: string) =>
        page.locator("main li", { has: page.getByRole("heading", { level: 2, name, exact: true }) });
      await expect(card("Austin").getByText("Included", { exact: true })).toBeVisible();
      await expect(card("San Antonio").getByText("Locked")).toBeVisible();
    });
  });
});
