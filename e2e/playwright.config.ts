import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

// Load the repo-root .env (git-ignored) so the E2E_* / SUPERADMIN_* / STRIPE_* gates see the
// same values the API was started with. Variables already set in the shell win.
const rootEnv = fileURLToPath(new URL("../.env", import.meta.url));
if (existsSync(rootEnv)) process.loadEnvFile(rootEnv);

const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";

// The suite runs against an already-running stack (see README.md): Postgres seeded,
// API on :5050 and the web app on :3000 with the API mock OFF. globalSetup fails fast
// with a clear message when either server is down instead of starting them itself.
export default defineConfig({
  testDir: "./tests",
  globalSetup: "./global-setup.ts",
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  workers: 1, // serial: specs share one seeded database and one rate-limit window
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [["github"], ["list"]] : [["list"]],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
