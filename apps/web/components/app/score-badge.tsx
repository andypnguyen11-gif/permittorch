import { Flame } from "lucide-react";
import { cn } from "@/lib/utils";
import { scoreBand, type ScoreBand } from "./format";

const BAND_CLASSES: Record<ScoreBand, string> = {
  hot: "bg-orange-500 text-white shadow-sm shadow-orange-500/30",
  strong: "bg-orange-100 text-orange-700 ring-1 ring-inset ring-orange-200",
  medium: "bg-amber-50 text-amber-700 ring-1 ring-inset ring-amber-200",
  muted: "bg-stone-100 text-stone-600 ring-1 ring-inset ring-stone-200",
};

export const BAND_LABELS: Record<ScoreBand, string> = {
  hot: "Hot", strong: "Very High", medium: "High", muted: "Monitor",
};

export function ScoreBadge({ score, showLabel = false }: { score: number; showLabel?: boolean }) {
  const band = scoreBand(score);
  return (
    <span className="inline-flex flex-col items-center gap-0.5">
      {/* aria-label on a generic span is not reliably announced; use real text. */}
      <span
        data-testid="score-badge"
        data-band={band}
        aria-hidden
        className={cn(
          "inline-flex h-8 min-w-10 items-center justify-center gap-0.5 rounded-lg px-2 text-sm font-semibold tabular-nums",
          BAND_CLASSES[band],
        )}
      >
        {band === "hot" && <Flame className="size-3.5" aria-hidden />}
        {score}
      </span>
      <span className="sr-only">{`Lead score ${score} of 100 (${BAND_LABELS[band]})`}</span>
      {showLabel && (
        <span className="text-[11px] font-medium text-muted-foreground" aria-hidden>
          {BAND_LABELS[band]}
        </span>
      )}
    </span>
  );
}
