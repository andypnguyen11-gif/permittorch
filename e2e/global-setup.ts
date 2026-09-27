import { API_URL } from "./helpers/env";

async function reachable(url: string): Promise<boolean> {
  try {
    const res = await fetch(url, { redirect: "manual", signal: AbortSignal.timeout(10_000) });
    return res.status < 500;
  } catch {
    return false;
  }
}

/**
 * The suite never boots servers itself: it runs against the stack described in
 * e2e/README.md. Fail fast, with the fix, when either half is down.
 */
export default async function globalSetup(): Promise<void> {
  const web = process.env.E2E_BASE_URL ?? "http://localhost:3000";
  const problems: string[] = [];
  if (!(await reachable(`${API_URL}/api/health`))) {
    problems.push(`API not reachable at ${API_URL}/api/health — start it with: set -a; source .env; set +a; ` +
      "dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile --urls http://localhost:5050");
  }
  if (!(await reachable(`${web}/login`))) {
    problems.push(`Web app not reachable at ${web} — start it with: pnpm --dir apps/web dev -p 3000 (API mock OFF)`);
  }
  if (problems.length) throw new Error(`E2E stack is not running:\n  - ${problems.join("\n  - ")}`);
}
