import type { LeadOutcomeReport, Market, PermitAgeFilter } from "@permittorch/types";
import type { LeadOutcomeReportFilters } from "@/lib/api";
import { Button } from "@/components/ui/button";

const field = "h-9 rounded-lg border border-border bg-white px-2 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50";

const UNVERSIONED = "unversioned";
// A version the API could have written: "r<revision>-<six hex>", or the tags made before versions.
const VERSION_PATTERN = /^(unversioned|r\d{1,4}-[0-9a-f]{6})$/;

export const PERMIT_AGES: { value: PermitAgeFilter; label: string }[] = [
  { value: "ALL", label: "All permits" },
  { value: "RECENT", label: "Recent: 30 days old or less when tagged" },
  { value: "OLDER", label: "Older: more than 30 days old when tagged" },
];

type Param = string | string[] | undefined;

/** The filters from the page URL that are safe to send: a known market, a well-formed version
 *  and a known permit age. Anything else is dropped, so a hand-edited URL cannot reach the API as junk. */
export function reportFilters(
  params: { marketId?: Param; version?: Param; permitAge?: Param }, markets: Pick<Market, "id">[],
): LeadOutcomeReportFilters {
  const one = (p: Param) => (typeof p === "string" ? p : undefined);
  const filters: LeadOutcomeReportFilters = {};
  const marketId = markets.find((m) => m.id === one(params.marketId))?.id;
  if (marketId) filters.marketId = marketId;
  const version = one(params.version);
  if (version && VERSION_PATTERN.test(version)) filters.version = version;
  const permitAge = PERMIT_AGES.find((a) => a.value === one(params.permitAge))?.value;
  if (permitAge) filters.permitAge = permitAge;
  return filters;
}

function versionLabel(version: string, answers: number, current: string): string {
  const name = version === UNVERSIONED ? "Before versioning" : version === current ? `${version} (current)` : version;
  return `${name} · ${answers} ${answers === 1 ? "answer" : "answers"}`;
}

/** Market, scoring version and permit age for the lead tags report. A plain GET form: the page reads the URL. */
export function OutcomeReportFilters({ markets, marketId, report }: {
  markets: Market[]; marketId?: string; report: LeadOutcomeReport;
}) {
  // The current version and the one shown are always offered, even before anyone tagged under them.
  const versions = [...report.versions];
  for (const v of [report.version, report.currentVersion]) {
    if (!versions.some((x) => x.version === v)) versions.push({ version: v, answers: 0 });
  }
  versions.sort((a, b) => Number(b.version === report.currentVersion) - Number(a.version === report.currentVersion));

  return (
    <form method="get" aria-label="Filter lead tags" className="flex flex-wrap items-end gap-3 rounded-xl border border-border bg-white p-3">
      <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Market
        <select name="marketId" defaultValue={marketId ?? ""} className={field}>
          <option value="">All markets</option>
          {markets.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
        </select>
      </label>
      <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Scoring version
        <select name="version" defaultValue={report.version} className={field}>
          {versions.map((v) => (
            <option key={v.version} value={v.version}>{versionLabel(v.version, v.answers, report.currentVersion)}</option>
          ))}
        </select>
      </label>
      <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Permit age
        <select name="permitAge" defaultValue={report.permitAge} className={field}>
          {PERMIT_AGES.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
        </select>
      </label>
      <Button type="submit" variant="outline" className="h-9">Apply</Button>
    </form>
  );
}
