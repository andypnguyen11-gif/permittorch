import type { LeadSummary } from "@permittorch/types";
import { SectionCard } from "@/components/app/section-card";
import { sampleScope } from "@/components/app/overview/stat-cards";

// counts[0] = oldest day, counts[days-1] = today
export function dailyCounts(leads: LeadSummary[], days = 30, now = Date.now()): number[] {
  const counts = new Array<number>(days).fill(0);
  for (const lead of leads) {
    if (!lead.filedDate) continue;
    const age = Math.floor((now - Date.parse(lead.filedDate)) / 86_400_000);
    if (age >= 0 && age < days) counts[days - 1 - age] += 1;
  }
  return counts;
}

const dayLabel = (daysAgo: number) =>
  daysAgo === 0 ? "Today" : daysAgo === 1 ? "Yesterday" : `${daysAgo} days ago`;

/** Filings per day among the fetched leads; `total` (API) decides how the sample is labeled. */
export function ActivitySparkline({ leads, total }: { leads: LeadSummary[]; total: number }) {
  const scope = sampleScope({ total, sampleSize: leads.length });
  const counts = dailyCounts(leads);
  const filings = counts.reduce((a, b) => a + b, 0);
  const w = 280;
  const h = 72;
  const max = Math.max(...counts, 1);
  const step = w / (counts.length - 1);
  const y = (c: number) => h - 4 - (c / max) * (h - 12);
  const points = counts.map((c, i) => `${(i * step).toFixed(1)},${y(c).toFixed(1)}`);

  return (
    <SectionCard title={<>Permit activity <span className="font-normal text-stone-400">(30 days)</span></>}>
      <p className="text-2xl font-bold tracking-tight tabular-nums">
        {filings}<span className="ml-1.5 text-sm font-normal text-stone-500">{filings === 1 ? "filing" : "filings"}</span>
      </p>
      <p data-testid="sparkline-scope" className="mb-3 text-xs text-stone-400">{scope}</p>
      <svg viewBox={`0 0 ${w} ${h}`} className="h-20 w-full overflow-visible" preserveAspectRatio="none"
        role="img" aria-label={`Fire-protection permit filings per day over the last 30 days, ${scope}: ${filings} total`}>
        <line x1="0" x2={w} y1={h - 4} y2={h - 4} stroke="currentColor" className="text-stone-200" strokeWidth="1" />
        <polygon points={`0,${h - 4} ${points.join(" ")} ${w},${h - 4}`} className="fill-orange-500/10" />
        <polyline points={points.join(" ")} fill="none" className="stroke-orange-500" strokeWidth="2"
          strokeLinejoin="round" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
        {/* Hover layer: one full-height hit target per day with a native tooltip. */}
        {counts.map((c, i) => (
          <rect key={i} x={i * step - step / 2} y="0" width={step} height={h} fill="transparent">
            <title>{`${dayLabel(counts.length - 1 - i)}: ${c} filing${c === 1 ? "" : "s"}`}</title>
          </rect>
        ))}
      </svg>
      <div className="mt-1 flex justify-between text-[11px] text-stone-400">
        <span>30 days ago</span><span>Today</span>
      </div>
    </SectionCard>
  );
}
