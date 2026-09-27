import type { Page } from "@playwright/test";

/** Data rows of the leads table (the header row lives in <thead>). */
export const leadRows = (page: Page) => page.locator("main table tbody tr");

/** Open a filter-bar select (base-ui popup, not a native <select>) and pick an option. */
export async function pickFilter(page: Page, label: string, option: string): Promise<void> {
  await page.getByLabel(label, { exact: true }).click();
  await page.getByRole("option", { name: option, exact: true }).click();
}

/** Signal weights render as "+25" / "−20" (U+2212). */
export function parseWeight(text: string): number {
  const t = text.trim().replace("−", "-");
  const n = Number.parseInt(t, 10);
  if (Number.isNaN(n)) throw new Error(`Unparseable signal weight: ${text}`);
  return n;
}
