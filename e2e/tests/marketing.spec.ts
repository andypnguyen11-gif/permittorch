import { expect, test } from "@playwright/test";
import { API_URL } from "../helpers/env";
import { AUSTIN_SOURCE_NAME, MARKET_PAGES } from "../helpers/seed";

// Public marketing surface against the real API + seeded database. No sign-in needed.
test.describe("marketing smoke", () => {
  test("home renders the hero, CTAs and a market picker limited to markets with data", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveTitle(/PermitTorch/);
    await expect(page.getByRole("heading", { level: 1, name: /find the permits worth chasing/i })).toBeVisible();
    await expect(page.getByRole("link", { name: "Find Leads in Your Market" })).toHaveAttribute("href", "/signup");
    await expect(page.getByRole("link", { name: "See Sample Leads" })).toBeVisible();

    const market = page.getByLabel("Market", { exact: true });
    await expect(market.locator("option", { hasText: "Austin, TX" })).toHaveCount(1);
    // Chicago is a registry market with no permits: it must never be offered.
    await expect(market.locator("option", { hasText: "Chicago, IL" })).toHaveCount(0);
  });

  test("pricing renders three tiers whose CTAs carry the plan into sign-up", async ({ page }) => {
    await page.goto("/pricing");
    await expect(page.getByRole("heading", { level: 1, name: "Simple pricing. Real leads." })).toBeVisible();
    for (const [tier, price, plan] of [
      ["Starter", "$49", "STARTER"], ["Pro", "$129", "PRO"], ["Territory", "$249", "TERRITORY"],
    ] as const) {
      await expect(page.getByRole("heading", { level: 2, name: tier, exact: true })).toBeVisible();
      await expect(page.getByText(price, { exact: true })).toBeVisible();
      await expect(page.locator(`a[href="/signup?plan=${plan}"]`).first()).toBeVisible();
    }
  });

  test("how-it-works renders", async ({ page }) => {
    await page.goto("/how-it-works");
    await expect(page.getByRole("heading", { level: 1, name: "How PermitTorch works" })).toBeVisible();
  });

  test("locations index lists only seeded markets with data", async ({ page }) => {
    await page.goto("/locations");
    await expect(page.getByRole("heading", { level: 1, name: "Markets we cover" })).toBeVisible();
    await expect(page.locator(`a[href="${MARKET_PAGES.austin}"]`).first()).toBeVisible();
    await expect(page.locator('a[href="/locations/illinois/chicago"]')).toHaveCount(0);
  });

  test("seeded Austin market page shows the API's real stats and an honest freshness line", async ({ page, request }) => {
    const res = await request.get(`${API_URL}/api/markets/austin-tx/stats`);
    expect(res.ok()).toBeTruthy();
    const stats = (await res.json()) as { totalLast30Days: number; lastUpdatedAt: string | null };
    expect(stats.lastUpdatedAt).not.toBeNull();

    await page.goto(MARKET_PAGES.austin);
    await expect(page.getByRole("heading", { level: 1, name: /Fire Protection Leads in Austin,\s*Texas/ })).toBeVisible();
    // The headline number is the API's count, not marketing copy.
    await expect(page.getByText(/PermitTorch identified/)).toContainText(
      new RegExp(`identified\\s*${stats.totalLast30Days}\\s*fire-related opportunities`));
    await expect(page.getByText(AUSTIN_SOURCE_NAME).first()).toBeVisible();

    // Freshness: the <time> carries the API timestamp and, once hydrated, reads "Updated N … ago".
    const freshness = page.locator(`time[datetime="${stats.lastUpdatedAt}"]`).first();
    await expect(freshness).toHaveText(/^Updated \d+ (minute|hour|day)s? ago$/);
    // The example leads are clearly labelled as illustrative, never passed off as real records.
    await expect(page.getByText(/Illustrative examples/)).toBeVisible();
  });

  test("a registry market without data has no page (no thin SEO pages)", async ({ page }) => {
    const res = await page.goto("/locations/illinois/chicago");
    expect(res?.status()).toBe(404);
  });

  for (const [path, heading] of [
    ["/fire-protection-leads", /fire protection leads from live permit data/i],
    ["/fire-sprinkler-leads", /fire sprinkler leads/i],
    ["/fire-alarm-leads", /fire alarm leads/i],
  ] as const) {
    test(`category lander ${path} renders`, async ({ page }) => {
      await page.goto(path);
      await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
    });
  }

  test("blog index links to posts that render", async ({ page }) => {
    await page.goto("/blog");
    await expect(page.getByRole("heading", { level: 1, name: "Resources" })).toBeVisible();
    const post = page.getByRole("heading", { level: 2, name: "How Fire Sprinkler Contractors Find Leads" });
    await expect(post).toBeVisible();
    await page.goto("/blog/how-fire-sprinkler-contractors-find-leads");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  });

  test("terms and privacy render", async ({ page }) => {
    await page.goto("/terms");
    await expect(page.getByRole("heading", { level: 1, name: "Terms of Service" })).toBeVisible();
    await page.goto("/privacy");
    await expect(page.getByRole("heading", { level: 1, name: "Privacy Policy" })).toBeVisible();
  });

  test("sitemap.xml lists static pages and only market pages with data", async ({ request }) => {
    const res = await request.get("/sitemap.xml");
    expect(res.status()).toBe(200);
    const xml = await res.text();
    for (const path of ["/pricing", "/how-it-works", "/locations", "/blog", MARKET_PAGES.austin]) {
      expect(xml).toContain(`${path}</loc>`);
    }
    expect(xml).not.toContain("/locations/illinois/chicago");
    expect(xml).not.toContain("/app");
  });

  test("robots.txt disallows the dashboard and points at the sitemap", async ({ request }) => {
    const res = await request.get("/robots.txt");
    expect(res.status()).toBe(200);
    const body = await res.text();
    expect(body).toMatch(/Disallow: \/app/);
    expect(body).toMatch(/Sitemap: .*\/sitemap\.xml/);
  });
});
