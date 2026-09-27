import { expect, test } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { requireUsers } from "../helpers/env";
import { AUSTIN_LEADS } from "../helpers/seed";

const LEAD = AUSTIN_LEADS.warehouse;

test.describe("saved leads (entitled user)", () => {
  requireUsers("entitled");

  test("save a lead, see it in /app/saved, mark it contacted (persisted), then remove it", async ({ page }) => {
    await signIn(page, USERS.entitled);
    await page.goto(`/app/leads/${LEAD.id}`);
    await expect(page.getByRole("heading", { level: 1, name: LEAD.title })).toBeVisible();

    // Re-runnable without reseeding: a leftover save from an earlier run is removed first.
    const saved = page.getByRole("button", { name: "Saved", exact: true });
    if (await saved.isVisible()) {
      await saved.click();
      await expect(page.getByRole("button", { name: "Save lead", exact: true })).toBeVisible();
    }
    const save = page.waitForResponse((r) => r.url().endsWith("/api/saved-leads") && r.request().method() === "POST");
    await page.getByRole("button", { name: "Save lead", exact: true }).click();
    expect((await save).status()).toBeLessThan(300);
    await expect(saved).toHaveAttribute("aria-pressed", "true");

    await page.goto("/app/saved");
    const item = page.locator("main li", { has: page.getByRole("link", { name: LEAD.title, exact: true }) });
    await expect(item).toBeVisible();
    await expect(item.getByText("Saved", { exact: true })).toBeVisible();

    const patch = page.waitForResponse((r) => /\/api\/saved-leads\/[0-9a-f-]{36}$/.test(r.url())
      && r.request().method() === "PATCH");
    await item.getByRole("button", { name: "Mark contacted" }).click();
    expect((await patch).status()).toBe(200);
    await page.reload();
    await expect(item.getByText("Contacted", { exact: true })).toBeVisible(); // persisted by the API
    await expect(item.getByRole("button", { name: "Mark saved" })).toBeVisible();

    await item.getByRole("button", { name: "Remove" }).click();
    await expect(item).toHaveCount(0);
    await page.reload();
    await expect(page.getByRole("link", { name: LEAD.title, exact: true })).toHaveCount(0);
  });
});
