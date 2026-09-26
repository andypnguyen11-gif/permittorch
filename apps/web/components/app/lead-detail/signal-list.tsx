import type { LeadSignal } from "@permittorch/types";
import { SectionCard } from "@/components/app/section-card";
import { cn } from "@/lib/utils";

const signed = (n: number) => (n >= 0 ? `+${n}` : `−${Math.abs(n)}`);

export function SignalList({ score, signals }: { score: number; signals: LeadSignal[] }) {
  const maxAbs = Math.max(1, ...signals.map((s) => Math.abs(s.weight)));
  return (
    <SectionCard title={`Why this is a ${score}`}>
      {signals.length === 0 ? (
        <p className="text-sm text-stone-500">No scoring signals were recorded for this lead.</p>
      ) : (
        <>
          <ul className="space-y-2.5">
            {signals.map((signal) => (
              <li key={signal.signalType + signal.description} className="space-y-1">
                <div className="flex items-center justify-between gap-4 text-sm">
                  <span className="text-stone-700">{signal.description}</span>
                  <span
                    data-testid="signal-weight"
                    className={cn(
                      "font-semibold tabular-nums",
                      signal.weight >= 0 ? "text-green-600" : "text-red-600",
                    )}
                  >
                    {signed(signal.weight)}
                  </span>
                </div>
                <div className="h-1 overflow-hidden rounded-full bg-stone-100" aria-hidden>
                  <div
                    className={cn("h-full rounded-full", signal.weight >= 0 ? "bg-green-500/70" : "bg-red-500/70")}
                    style={{ width: `${(Math.abs(signal.weight) / maxAbs) * 100}%` }}
                  />
                </div>
              </li>
            ))}
          </ul>
          <div className="mt-4 flex items-center justify-between border-t border-border pt-3 text-sm">
            <span className="font-medium text-stone-900">Lead score</span>
            <span data-testid="signal-total" className="font-bold tabular-nums text-stone-900">
              {signals.reduce((sum, s) => sum + s.weight, 0)}
            </span>
          </div>
        </>
      )}
      <p className="mt-3 text-xs text-stone-400">
        Every point traces to a detected signal — scores are deterministic, not AI-generated.
      </p>
    </SectionCard>
  );
}
