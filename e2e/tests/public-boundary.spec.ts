import { expect, test, type Page } from "@playwright/test";
import { API_URL } from "../helpers/env";
import { OTHER_MARKET_LEADS, seedId } from "../helpers/seed";

// Signed-out boundary of the real stack: middleware redirects, API authorization, and the
// public sample-lead form. None of this needs a Firebase account, so it always runs.

test.describe("signed-out dashboard routing", () => {
  for (const path of ["/app/leads", "/app/leads/x.json", `/app/leads/${seedId(101)}`, "/app/saved", "/app/admin/sources"]) {
    test(`${path} redirects to /login with a return path`, async ({ page }) => {
      await page.goto(path);
      await page.waitForURL(/\/login\?redirect=/);
      const redirect = new URL(page.url()).searchParams.get("redirect");
      expect(redirect).toBe(path);
      await expect(page.getByRole("heading", { level: 1, name: "Sign in to PermitTorch" })).toBeVisible();
    });
  }

  test("a dotted /app path is not served as a static file", async ({ request }) => {
    const res = await request.get("/app/leads/x.json", { maxRedirects: 0 });
    expect(res.status()).toBe(307);
    expect(res.headers()["location"]).toMatch(/\/login\?redirect=%2Fapp%2Fleads%2Fx\.json$/);
  });
});

test.describe("API authorization without a token", () => {
  const protectedCalls: Array<[string, string]> = [
    ["GET", "/api/leads"],
    ["GET", `/api/leads/${OTHER_MARKET_LEADS.sanAntonioSprinkler.id}`],
    ["GET", "/api/leads/export.csv"],
    ["GET", "/api/account/me"],
    ["GET", "/api/account/markets"],
    ["GET", "/api/saved-leads"],
    ["POST", "/api/saved-leads"],
    ["PUT", "/api/email-preferences"],
    ["POST", "/api/billing/checkout"],
    ["POST", "/api/billing/portal"],
    ["GET", "/api/admin/sources"],
    ["GET", "/api/admin/scraper-runs"],
  ];

  for (const [method, path] of protectedCalls) {
    test(`${method} ${path} → 401 { error }`, async ({ request }) => {
      const res = await request.fetch(`${API_URL}${path}`, {
        method, data: method === "GET" ? undefined : {},
      });
      expect(res.status()).toBe(401);
      expect(await res.json()).toEqual({ error: expect.any(String) });
    });
  }

  test("a forged bearer token is rejected with 401", async ({ request }) => {
    const res = await request.get(`${API_URL}/api/leads`, {
      headers: { Authorization: "Bearer not-a-real-firebase-token" },
    });
    expect(res.status()).toBe(401);
    expect(await res.json()).toEqual({ error: expect.any(String) });
  });

  test("the Stripe webhook refuses an unsigned event", async ({ request }) => {
    const res = await request.post(`${API_URL}/api/webhooks/stripe`, {
      data: { id: "evt_forged", type: "checkout.session.completed" },
    });
    expect(res.status()).toBe(400);
  });

  test("public market endpoints stay open", async ({ request }) => {
    const res = await request.get(`${API_URL}/api/markets`);
    expect(res.status()).toBe(200);
    const markets = (await res.json()) as Array<{ slug: string }>;
    expect(markets.map((m) => m.slug)).toEqual(expect.arrayContaining(["austin-tx", "san-antonio-tx", "fort-worth-tx"]));
  });
});

test.describe("sample-lead form (real API)", () => {
  // Resend's test inbox: accepted and discarded, so a real RESEND_API_KEY never emails a stranger.
  const email = `delivered+e2e-${Date.now()}@resend.dev`;

  const sampleForm = (page: Page) =>
    page.locator("form").filter({ has: page.getByRole("button", { name: "Send My Sample Leads" }) });

  async function submit(page: Page) {
    await page.goto("/");
    const form = sampleForm(page);
    await form.getByLabel("Name", { exact: true }).fill("E2E Tester");
    await form.getByLabel("Work email", { exact: true }).fill(email);
    await form.getByLabel("Company", { exact: true }).fill("E2E Fire Co");
    await form.getByLabel("Market", { exact: true }).selectOption({ label: "Austin, TX" });
    const response = page.waitForResponse(
      (r) => r.url() === `${API_URL}/api/sample-leads` && r.request().method() === "POST");
    await form.getByRole("button", { name: "Send My Sample Leads" }).click();
    const res = await response;
    expect(res.request().postDataJSON()).toEqual({
      name: "E2E Tester", email, company: "E2E Fire Co", marketSlug: "austin-tx",
    });
    return res;
  }

  test("submits for a seeded market, and an identical resubmit is idempotent", async ({ page, context }) => {
    const first = await submit(page);
    expect(first.status()).toBe(202);
    await expect(page.getByRole("heading", { name: "Request received — check your inbox." })).toBeVisible();

    // Same email + market again: the API's (email, market_slug) uniqueness makes this a no-op
    // success, not a 4xx/5xx, and the visitor sees the same confirmation.
    const again = await context.newPage(); // a fresh visit shows the empty form again
    const second = await submit(again);
    expect(second.status()).toBe(202);
    await expect(again.getByRole("heading", { name: "Request received — check your inbox." })).toBeVisible();
  });

  test("client-side validation blocks an incomplete request", async ({ page }) => {
    await page.goto("/#sample-leads");
    let posted = false;
    page.on("request", (r) => { if (r.url().endsWith("/api/sample-leads")) posted = true; });
    await page.getByRole("button", { name: "Send My Sample Leads" }).click();
    await expect(page.getByText("Enter your name.")).toBeVisible();
    await expect(page.getByText("Pick a market.")).toBeVisible();
    expect(posted).toBe(false);
  });
});
