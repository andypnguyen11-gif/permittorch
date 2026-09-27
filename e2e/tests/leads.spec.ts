import { expect, test } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { requireUsers } from "../helpers/env";
import { leadRows, parseWeight, pickFilter } from "../helpers/leads";
import { AUSTIN_LEAD_COUNT, AUSTIN_LEADS, AUSTIN_SOURCE_NAME } from "../helpers/seed";

// Entitled user = seeded "Acme Fire Protection", Pro plan, austin-tx only (7 seeded leads).
test.describe("leads flow (entitled user)", () => {
  requireUsers("entitled");

  test.beforeEach(async ({ page }) => {
    await signIn(page, USERS.entitled);
    await expect(leadRows(page)).toHaveCount(AUSTIN_LEAD_COUNT);
  });

  test("feed lists every Austin lead with a total and an honest freshness line", async ({ page }) => {
    await expect(page.getByText(`${AUSTIN_LEAD_COUNT} opportunities`)).toBeVisible();
    for (const lead of Object.values(AUSTIN_LEADS)) {
      await expect(page.getByRole("link", { name: lead.title, exact: true })).toBeVisible();
    }
    const freshness = page.locator("main time").first();
    await expect(freshness).toHaveText(/^Updated /);
    await expect(freshness).toHaveAttribute("datetime", /^\d{4}-\d{2}-\d{2}T/);
  });

  test("category filter narrows the feed and is reflected in the URL", async ({ page }) => {
    await pickFilter(page, "Category", "Fire Alarm");
    await page.waitForURL(/category=FIRE_ALARM/);
    // Seeded Austin data has exactly one fire-alarm lead.
    await expect(leadRows(page)).toHaveCount(1);
    await expect(page.getByRole("link", { name: AUSTIN_LEADS.officeAlarm.title, exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: AUSTIN_LEADS.warehouse.title, exact: true })).toHaveCount(0);

    await page.getByRole("button", { name: "Clear filters" }).click();
    await expect(leadRows(page)).toHaveCount(AUSTIN_LEAD_COUNT);
  });

  test("search finds matching leads only", async ({ page }) => {
    const search = page.getByRole("searchbox", { name: "Search leads" });
    await search.fill("warehouse");
    await search.press("Enter");
    await page.waitForURL(/[?&]q=warehouse/);
    await expect(leadRows(page)).toHaveCount(1);
    await expect(page.getByRole("link", { name: AUSTIN_LEADS.warehouse.title, exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: AUSTIN_LEADS.kitchenHood.title, exact: true })).toHaveCount(0);
  });

  test("a search with no matches shows the empty state, not an error", async ({ page }) => {
    await page.goto("/app/leads?q=zzz-no-such-permit");
    await expect(page.getByText("No leads match these filters")).toBeVisible();
    await expect(leadRows(page)).toHaveCount(0);
  });

  test("lead detail explains every point of the score and links the source", async ({ page }) => {
    await page.getByRole("link", { name: AUSTIN_LEADS.warehouse.title, exact: true }).click();
    await page.waitForURL(`**/app/leads/${AUSTIN_LEADS.warehouse.id}`);
    await expect(page.getByRole("heading", { level: 1, name: AUSTIN_LEADS.warehouse.title })).toBeVisible();

    const why = page.getByRole("heading", { level: 2, name: /^Why this is a \d+$/ });
    await expect(why).toBeVisible();
    const score = Number((await why.textContent())!.match(/\d+$/)![0]);

    // Persisted LeadSignal rows: baseline, new-commercial-build (+25) and the sprinkler scope.
    const breakdown = page.locator("section", { has: why });
    await expect(breakdown.getByText("New commercial construction", { exact: true })).toBeVisible();
    await expect(breakdown.getByText("Baseline for a classified fire-protection permit")).toBeVisible();
    const weights = await breakdown.getByTestId("signal-weight").allTextContents();
    expect(weights).toContain("+25");
    expect(weights).toContain("+30");
    // Explainable by construction: the displayed score is clamp(Σ signal weights, 0, 100).
    const sum = weights.map(parseWeight).reduce((a, b) => a + b, 0);
    expect(score).toBe(Math.min(100, Math.max(0, sum)));

    const source = page.locator("section", { has: page.getByRole("heading", { level: 2, name: "Source", exact: true }) });
    await expect(source.getByText(AUSTIN_SOURCE_NAME)).toBeVisible();
    await expect(source.getByRole("link", { name: "View original record" })).toHaveAttribute("href", /^https:\/\//);
    await expect(source.getByText(/^Last checked /)).toBeVisible();
  });
});
