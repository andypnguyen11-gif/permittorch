import { expect, test } from "@playwright/test";
import { signIn, USERS } from "../helpers/auth";
import { API_URL, requireStripeTestKey, requireUsers } from "../helpers/env";

// Opens a real Stripe Checkout Session in TEST mode and stops at the hosted page:
// never fills card details or completes a payment. Webhook handling is exercised
// separately with `stripe trigger checkout.session.completed` (see README.md).
test.describe("billing checkout (unsubscribed user)", () => {
  requireUsers("unentitled");
  requireStripeTestKey();

  test("the plan picker posts { plan, marketSlugs } and redirects to Stripe Checkout", async ({ page }) => {
    await signIn(page, USERS.unentitled);
    // /pricing → /signup?plan=PRO lands here for new accounts; go straight to it.
    await page.goto("/app/account?plan=PRO");
    await expect(page.getByText("No active plan")).toBeVisible();
    await expect(page.getByRole("radio", { name: "Pro", exact: true })).toBeChecked();

    const subscribe = page.getByRole("button", { name: "Subscribe to Pro" });
    await expect(subscribe).toBeDisabled(); // Pro needs exactly one market
    await page.getByRole("radio", { name: "Austin", exact: true }).check();
    await expect(subscribe).toBeEnabled();

    const checkout = page.waitForRequest((r) => r.url() === `${API_URL}/api/billing/checkout` && r.method() === "POST");
    await subscribe.click();
    const request = await checkout;
    expect(request.postDataJSON()).toEqual({ plan: "PRO", marketSlugs: ["austin-tx"] });
    expect(request.headers()["authorization"]).toMatch(/^Bearer .+/);

    const response = await request.response();
    expect(response?.status()).toBe(200);
    const { url } = (await response!.json()) as { url: string };
    expect(url).toMatch(/^https:\/\/checkout\.stripe\.com\//);

    await page.waitForURL(/^https:\/\/checkout\.stripe\.com\//, { timeout: 30_000 });
    // Stop here: no card details, no payment.
  });
});
