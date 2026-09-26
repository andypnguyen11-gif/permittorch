import { CalendarClock, DollarSign, Flame, Star, type LucideIcon } from "lucide-react";
import type { LeadSummary } from "@permittorch/types";
import { formatValueShort } from "@/components/app/format";

export const HOT_SCORE = 90;
export const RECENT_DAYS = 3;

export interface OverviewStats {
  /** All leads in the user's (selected) markets — API total. */
  total: number;
  /** Leads scoring 90+ — API total. */
  hotLeads: number;
  /** Permits filed in the last 3 days (maxAgeDays filters filed date) — API total. */
  filedRecently: number;
  /** Mean score over the fetched page only (see sampleSize). */
  avgScore: number;
  /** Sum of reported values over the fetched page only (see sampleSize). */
  totalValue: number;
  /** How many leads avgScore/totalValue were computed over. */
  sampleSize: number;
}

/**
 * Counts come from API totals so they are exact for the whole market. The
 * average score and project value can only be computed over the leads we
 * fetched, so they carry `sampleSize` and must be labeled with sampleScope().
 */
export function computeOverviewStats({ leads, total, hotTotal, recentTotal }: {
  leads: LeadSummary[]; total: number; hotTotal: number; recentTotal: number;
}): OverviewStats {
  const scores = leads.map((l) => l.score);
  return {
    total,
    hotLeads: hotTotal,
    filedRecently: recentTotal,
    avgScore: scores.length ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length) : 0,
    totalValue: leads.reduce((a, l) => a + (l.estimatedValue ?? 0), 0),
    sampleSize: leads.length,
  };
}

/** Honest scope for page-derived numbers: partial samples say so. */
export function sampleScope({ total, sampleSize }: { total: number; sampleSize: number }): string {
  if (total > sampleSize) return `across your top ${sampleSize} leads`;
  return total === 1 ? "across your 1 lead" : `across all ${total} leads`;
}

export function StatCards({ stats }: { stats: OverviewStats }) {
  const scope = sampleScope(stats);
  const cards: Array<{ key: string; label: string; value: string; caption: string; icon: LucideIcon; tone: string }> = [
    { key: "recent", label: `Filed in the last ${RECENT_DAYS} days`, value: String(stats.filedRecently),
      caption: `of ${stats.total} ${stats.total === 1 ? "lead" : "leads"} in your markets`,
      icon: CalendarClock, tone: "bg-orange-50 text-orange-500" },
    { key: "hot", label: "Hot leads", value: String(stats.hotLeads),
      caption: `Score ${HOT_SCORE} or higher`, icon: Flame, tone: "bg-red-50 text-red-500" },
    { key: "avg", label: "Avg score", value: String(stats.avgScore),
      caption: scope, icon: Star, tone: "bg-amber-50 text-amber-500" },
    { key: "value", label: "Project value", value: formatValueShort(stats.totalValue),
      caption: `Reported values ${scope}`, icon: DollarSign, tone: "bg-green-50 text-green-600" },
  ];
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map(({ key, label, value, caption, icon: Icon, tone }) => (
        <div key={key} data-testid={`stat-${key}`} className="rounded-xl border border-border bg-white p-4 shadow-xs">
          <div className="flex items-center gap-2.5">
            <span className={`flex size-8 shrink-0 items-center justify-center rounded-full ${tone}`}>
              <Icon className="size-4" aria-hidden />
            </span>
            <p className="text-sm text-stone-500">{label}</p>
          </div>
          <p className="mt-3 text-2xl font-bold tracking-tight tabular-nums">{value}</p>
          <p className="mt-0.5 text-xs text-stone-400">{caption}</p>
        </div>
      ))}
    </div>
  );
}
