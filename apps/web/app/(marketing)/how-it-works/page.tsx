import Link from "next/link";
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { HOW_IT_WORKS_STEPS } from "@/components/marketing/how-it-works-steps";

export const metadata: Metadata = buildMetadata({
  title: "How PermitTorch Works — From Public Records to Scored Fire Leads",
  description:
    "How PermitTorch turns public permit and inspection records into scored, explainable fire-protection leads: monitor, classify, score, deliver.",
  path: "/how-it-works",
});

// Static illustrative example per PRD §16 — clearly labeled, not live data.
const SCORE_EXAMPLE = {
  total: 91,
  headline: "New commercial build — sprinkler scope, no fire contractor listed",
  signals: [
    { label: "New commercial project", points: 25 },
    { label: "Sprinkler scope detected", points: 20 },
    { label: "Filed within 48 hours", points: 15 },
    { label: "Project value over $1M", points: 15 },
    { label: "No fire contractor listed", points: 10 },
    { label: "Large commercial property", points: 6 },
  ],
};

export default function HowItWorksPage() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">How PermitTorch works</h1>
      <p className="mt-4 max-w-2xl text-lg text-neutral-600">
        Four steps between a city permit portal and your morning lead list. No black box at any of them.
      </p>

      <ol className="mt-14 space-y-12">
        {HOW_IT_WORKS_STEPS.map((s) => (
          <li key={s.step} className="flex gap-5">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-orange-100 font-bold text-orange-600">
              {s.step}
            </div>
            <div>
              <h2 className="text-xl font-semibold">{s.title}</h2>
              <p className="mt-2 leading-relaxed text-neutral-600">{s.detail}</p>
            </div>
          </li>
        ))}
      </ol>

      <section className="mt-20">
        <h2 className="text-2xl font-bold tracking-tight">Every score is explainable</h2>
        <p className="mt-3 max-w-2xl leading-relaxed text-neutral-600">
          A lead score is never a mystery number. It is the sum of named signals, and you see
          every one of them on every lead. Here is what that looks like:
        </p>

        <div className="mt-8 rounded-2xl border border-neutral-200 bg-white p-6 shadow-sm sm:p-8">
          <p className="text-xs font-medium uppercase tracking-wide text-neutral-400">
            Illustrative example — not a live lead
          </p>
          <div className="mt-3 flex items-center gap-4">
            <span className="flex h-14 w-14 items-center justify-center rounded-xl bg-orange-500 text-2xl font-bold text-white">
              {SCORE_EXAMPLE.total}
            </span>
            <div>
              <h3 className="font-semibold">Why this is a {SCORE_EXAMPLE.total}</h3>
              <p className="text-sm text-neutral-500">{SCORE_EXAMPLE.headline}</p>
            </div>
          </div>
          <ul className="mt-6 divide-y divide-neutral-100">
            {SCORE_EXAMPLE.signals.map((s) => (
              <li key={s.label} className="flex items-center justify-between py-2.5 text-sm">
                <span className="text-neutral-700">{s.label}</span>
                <span className="font-semibold text-orange-600">+{s.points}</span>
              </li>
            ))}
          </ul>
        </div>
        <p className="mt-4 text-sm text-neutral-500">
          Scores run 0–100 and are computed by fixed, configurable rules — the same permit
          always scores the same way. Negative signals (old permits, closed permits) subtract
          points just as visibly.
        </p>
      </section>

      <section className="mt-20 rounded-2xl bg-orange-50 p-10 text-center">
        <h2 className="text-2xl font-bold tracking-tight">See it on your own market</h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">
          Start free and watch tomorrow&apos;s permits show up scored and sorted.
        </p>
        <Link href="/signup"
          className={cn(buttonVariants({ size: "lg" }), "mt-6 bg-orange-500 px-8 text-white hover:bg-orange-600")}>
          Find Leads in Your Market
        </Link>
      </section>
    </div>
  );
}
