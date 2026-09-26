import { cn } from "@/lib/utils";
import { BADGE_CLASSES } from "@/components/marketing/cta";
import { SCORE_EXAMPLE } from "@/components/marketing/score-example";

/**
 * Static product preview for the homepage hero, in the dashboard's visual
 * style. Clearly captioned as illustrative: it is not a live record.
 */
export function HeroLeadPreview() {
  return (
    <figure className="w-full max-w-md justify-self-center md:justify-self-end">
      <div className="rounded-2xl border border-neutral-200 bg-white p-5 text-left shadow-lg shadow-orange-100">
        <div className="flex items-start gap-4">
          <span className={cn("flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-xl font-bold", BADGE_CLASSES)}
>
            <span className="sr-only">Score </span>{SCORE_EXAMPLE.total}
          </span>
          <div className="min-w-0">
            <p className="font-semibold text-neutral-900">Fire sprinkler system — new commercial build</p>
            <p className="mt-0.5 text-sm text-neutral-600">Austin, TX · Filed this week</p>
          </div>
        </div>
        <ul className="mt-4 divide-y divide-neutral-100 border-t border-neutral-100">
          {SCORE_EXAMPLE.signals.map((s) => (
            <li key={s.type} className="flex items-center justify-between py-2 text-sm">
              <span className="text-neutral-700">{s.label}</span>
              <span className="font-semibold text-orange-700">+{s.points}</span>
            </li>
          ))}
        </ul>
        <p className="mt-3 text-xs text-neutral-500">Source: Austin Issued Construction Permits</p>
      </div>
      <figcaption className="mt-3 text-center text-xs text-neutral-500">
        Illustrative — not a live record
      </figcaption>
    </figure>
  );
}
