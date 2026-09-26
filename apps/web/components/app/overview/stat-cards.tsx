import { DollarSign, Flame, Star, TrendingUp, type LucideIcon } from "lucide-react";
import type { LeadSummary } from "@permittorch/types";
import { formatValueShort } from "@/components/app/format";

export function computeOverviewStats(leads: LeadSummary[]) {
  const scores = leads.map((l) => l.score);
  return {
    newOpportunities: leads.filter((l) => l.isNew).length,
    hotLeads: leads.filter((l) => l.score >= 90).length,
    avgScore: scores.length
      ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
      : 0,
    totalValue: leads.reduce((a, l) => a + (l.estimatedValue ?? 0), 0),
  };
}

const CARDS: Array<{ key: keyof ReturnType<typeof computeOverviewStats>; label: string; hint: string; icon: LucideIcon; tone: string }> = [
  { key: "newOpportunities", label: "New opportunities", hint: "First detected in the last 72 hours", icon: TrendingUp, tone: "bg-orange-50 text-orange-500" },
  { key: "hotLeads", label: "Hot leads", hint: "Score 90 or higher", icon: Flame, tone: "bg-red-50 text-red-500" },
  { key: "avgScore", label: "Avg score", hint: "Across open leads in your markets", icon: Star, tone: "bg-amber-50 text-amber-500" },
  { key: "totalValue", label: "Total project value", hint: "Sum of reported permit values", icon: DollarSign, tone: "bg-green-50 text-green-600" },
];

export function StatCards({ leads }: { leads: LeadSummary[] }) {
  const stats = computeOverviewStats(leads);
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {CARDS.map(({ key, label, hint, icon: Icon, tone }) => (
        <div key={key} className="rounded-xl border border-border bg-white p-4 shadow-xs">
          <div className="flex items-center gap-2.5">
            <span className={`flex size-8 shrink-0 items-center justify-center rounded-full ${tone}`}>
              <Icon className="size-4" aria-hidden />
            </span>
            <p className="text-sm text-stone-500" title={hint}>{label}</p>
          </div>
          <p className="mt-3 text-2xl font-bold tracking-tight tabular-nums">
            {key === "totalValue" ? formatValueShort(stats.totalValue) : stats[key]}
          </p>
        </div>
      ))}
    </div>
  );
}
