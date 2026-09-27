import { API_URL } from "./helpers/env";

async function reachable(url: string): Promise<boolean> {
  try {
    const res = await fetch(url, { redirect: "manual", signal: AbortSignal.timeout(10_000) });
    return res.status < 500;
  } catch {
    return false;
  }
}

const SEED = "set -a; source .env; set +a; dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile -- seed";
const REFRESH = `${SEED} --refresh-samples`;

/**
 * The seeded-data specs (marketing Austin page, leads, entitlement, saved) need the sample
 * leads inside their 30-day window. Checked by default against a local API; set
 * E2E_SEED_CHECK=0 to skip (e.g. a deployed stack) or =1 to force it.
 */
function shouldCheckSeed(): boolean {
  const flag = process.env.E2E_SEED_CHECK;
  if (flag === "0" || flag === "1") return flag === "1";
  const host = new URL(API_URL).hostname;
  return host === "localhost" || host === "127.0.0.1";
}

async function seedProblem(): Promise<string | null> {
  const url = `${API_URL}/api/markets/austin-tx/stats`;
  let res: Response;
  try {
    res = await fetch(url, { signal: AbortSignal.timeout(10_000) });
  } catch {
    return `Could not read ${url}.`;
  }
  if (res.status === 404) {
    return `austin-tx is missing (${url} → 404): the registry is not seeded. Run from the repo root:\n      ${SEED}`;
  }
  if (!res.ok) return `${url} returned ${res.status}.`;
  const stats = (await res.json()) as { totalLast30Days: number; lastUpdatedAt: string | null };
  if (stats.lastUpdatedAt === null || stats.totalLast30Days === 0) {
    return "austin-tx has no leads in the last 30 days: the sample data is missing or stale. " +
      "Make sure .env has SEED_SAMPLE_DATA=true and SEED_E2E_IDENTITIES=true, then run from the repo root:\n" +
      `      ${SEED}\n      ${REFRESH}`;
  }
  return null;
}

/**
 * The suite never boots servers itself: it runs against the stack described in
 * e2e/README.md. Fail fast, with the fix, when either half is down or the seed is missing/stale.
 */
export default async function globalSetup(): Promise<void> {
  const web = process.env.E2E_BASE_URL ?? "http://localhost:3000";
  const problems: string[] = [];
  const apiUp = await reachable(`${API_URL}/api/health`);
  if (!apiUp) {
    problems.push(`API not reachable at ${API_URL}/api/health — start it with: set -a; source .env; set +a; ` +
      "dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile --urls http://localhost:5050");
  }
  if (!(await reachable(`${web}/login`))) {
    problems.push(`Web app not reachable at ${web} — start it with: pnpm --dir apps/web dev -p 3000 (API mock OFF)`);
  }
  if (apiUp && shouldCheckSeed()) {
    const problem = await seedProblem();
    if (problem) problems.push(problem);
  }
  if (problems.length) throw new Error(`E2E stack is not ready:\n  - ${problems.join("\n  - ")}`);
}
