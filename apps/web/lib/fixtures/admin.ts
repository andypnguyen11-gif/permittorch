import type { AdminSource, Paged, ScraperRunSummary } from "@permittorch/types";
import { daysAgo, hoursAgo, minutesAgo } from "./time";

export const mockAdminSources: AdminSource[] = [
  { id: "src-001", name: "City of Houston ePermits", city: "Houston", state: "TX",
    active: true, healthStatus: "HEALTHY", lastSuccessfulRunAt: minutesAgo(6), recordsLastRun: 247 },
  { id: "src-002", name: "Harris County Permits", city: "Houston", state: "TX",
    active: true, healthStatus: "STALE", lastSuccessfulRunAt: daysAgo(4), recordsLastRun: 58 },
  { id: "src-003", name: "Houston Fire Marshal", city: "Houston", state: "TX",
    active: true, healthStatus: "WARNING", lastSuccessfulRunAt: hoursAgo(19), recordsLastRun: 0 },
  { id: "src-004", name: "City of Dallas Permits", city: "Dallas", state: "TX",
    active: true, healthStatus: "FAILED", lastSuccessfulRunAt: daysAgo(2), recordsLastRun: 0 },
  { id: "src-005", name: "Dallas Fire-Rescue Inspections", city: "Dallas", state: "TX",
    active: false, healthStatus: "DISABLED", lastSuccessfulRunAt: daysAgo(12), recordsLastRun: 0 },
];

// ScraperRunSummary has no sourceId field (locked type), so keep the mapping
// alongside the run for mock filtering only.
const runs: Array<ScraperRunSummary & { __sourceId: string }> = [
  { id: "run-001", apifyRunId: "aBc123Houston01", status: "SUCCEEDED", startedAt: hoursAgo(2),
    finishedAt: hoursAgo(1.9), recordsImported: 247, duplicatesSkipped: 31, failures: 0,
    durationSeconds: 312.4, __sourceId: "src-001" },
  { id: "run-002", apifyRunId: "aBc123Houston02", status: "SUCCEEDED", startedAt: hoursAgo(8),
    finishedAt: hoursAgo(7.9), recordsImported: 198, duplicatesSkipped: 54, failures: 0,
    durationSeconds: 288.9, __sourceId: "src-001" },
  { id: "run-003", apifyRunId: "hFm881Harris01", status: "SUCCEEDED", startedAt: daysAgo(4),
    finishedAt: daysAgo(3.99), recordsImported: 58, duplicatesSkipped: 12, failures: 0,
    durationSeconds: 141.2, __sourceId: "src-002" },
  { id: "run-004", apifyRunId: "hFm881Harris02", status: "FAILED", startedAt: daysAgo(1),
    finishedAt: daysAgo(0.99), recordsImported: 0, duplicatesSkipped: 0, failures: 1,
    durationSeconds: 42.7, __sourceId: "src-002" },
  { id: "run-005", apifyRunId: "qPt556Marshal1", status: "SUCCEEDED", startedAt: hoursAgo(19),
    finishedAt: hoursAgo(18.9), recordsImported: 0, duplicatesSkipped: 0, failures: 0,
    durationSeconds: 96.1, __sourceId: "src-003" },
  { id: "run-006", apifyRunId: "qPt556Marshal2", status: "SUCCEEDED", startedAt: daysAgo(2),
    finishedAt: daysAgo(1.99), recordsImported: 41, duplicatesSkipped: 8, failures: 0,
    durationSeconds: 104.6, __sourceId: "src-003" },
  { id: "run-007", apifyRunId: "zLw902Dallas01", status: "FAILED", startedAt: daysAgo(2),
    finishedAt: daysAgo(1.98), recordsImported: 0, duplicatesSkipped: 0, failures: 3,
    durationSeconds: 233.0, __sourceId: "src-004" },
  { id: "run-008", apifyRunId: "zLw902Dallas02", status: "SUCCEEDED", startedAt: daysAgo(3),
    finishedAt: daysAgo(2.99), recordsImported: 112, duplicatesSkipped: 26, failures: 0,
    durationSeconds: 197.3, __sourceId: "src-004" },
  { id: "run-009", apifyRunId: "zLw902Dallas03", status: "SUCCEEDED", startedAt: daysAgo(4),
    finishedAt: daysAgo(3.98), recordsImported: 96, duplicatesSkipped: 19, failures: 12,
    durationSeconds: 401.8, __sourceId: "src-004" },
  { id: "run-010", apifyRunId: "vKd774DFR0001", status: "SUCCEEDED", startedAt: daysAgo(12),
    finishedAt: daysAgo(11.99), recordsImported: 14, duplicatesSkipped: 2, failures: 0,
    durationSeconds: 88.5, __sourceId: "src-005" },
];

export function mockAdminRuns(
  params: { sourceId?: string; page?: number } = {},
): Paged<ScraperRunSummary> {
  const page = Math.max(1, params.page ?? 1);
  const pageSize = 25;
  const filtered = params.sourceId
    ? runs.filter((r) => r.__sourceId === params.sourceId)
    : runs;
  const items = filtered
    .slice((page - 1) * pageSize, page * pageSize)
    .map(({ __sourceId: _ignored, ...run }) => run);
  return { items, total: filtered.length, page, pageSize };
}
