import { expect, test, type Page } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { requireUsers } from "../helpers/env";
import { AUSTIN_LEADS } from "../helpers/seed";

const LEAD = AUSTIN_LEADS.warehouse;

test.describe("lead tags (entitled user)", () => {
  requireUsers("entitled");

  const row = (page: Page) =>
    page.getByRole("row").filter({ has: page.getByRole("link", { name: LEAD.title, exact: true }) });

  test("tag a lead from the list without opening it, see the tag after a reload, then clear it", async ({ page }) => {
    await signIn(page, USERS.entitled);
    await page.goto(`/app/leads?market=austin-tx&q=${encodeURIComponent(LEAD.title)}`);
    const tag = row(page).getByRole("button", { name: "Lead outcome" });
    await expect(tag).toBeVisible();

    // Re-runnable without reseeding: a leftover tag from an earlier run is cleared first.
    if (!(await tag.textContent())?.includes("Tag")) {
      await tag.click();
      await page.getByRole("menuitem", { name: "Clear" }).click();
      await expect(tag).toHaveText(/Tag/);
    }

    const put = page.waitForResponse((r) => r.url().endsWith(`/api/leads/${LEAD.id}/outcome`)
      && r.request().method() === "PUT");
    await tag.click();
    await page.getByRole("menuitem", { name: "Wrong trade" }).click();
    expect((await put).status()).toBe(200);
    await expect(page).toHaveURL(/\/app\/leads\?/);   // choosing a tag did not open the lead
    await page.reload();
    await expect(row(page).getByRole("button", { name: "Lead outcome" })).toHaveText("Wrong trade");

    const del = page.waitForResponse((r) => r.url().endsWith(`/api/leads/${LEAD.id}/outcome`)
      && r.request().method() === "DELETE");
    await row(page).getByRole("button", { name: "Lead outcome" }).click();
    await page.getByRole("menuitem", { name: "Clear" }).click();
    expect((await del).status()).toBe(204);
    await page.reload();
    await expect(row(page).getByRole("button", { name: "Lead outcome" })).toHaveText(/Tag/);
  });
});
