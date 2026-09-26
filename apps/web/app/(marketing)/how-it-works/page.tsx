import Link from "next/link";
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { ctaClasses } from "@/components/marketing/cta";
import { HOW_IT_WORKS_STEPS } from "@/components/marketing/how-it-works-steps";
import { SCORE_EXAMPLE } from "@/components/marketing/score-example";

export const metadata: Metadata = buildMetadata({
  title: "How It Works — From Public Records to Scored Fire Leads",
  description:
    "How PermitTorch turns public permit and inspection records into scored, explainable fire-protection leads: monitor, classify, score, deliver.",
  path: "/how-it-works",
});

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
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-orange-100 font-bold text-orange-700">
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
          <p className="text-xs font-medium uppercase tracking-wide text-neutral-500">
            Illustrative example — not a live lead
          </p>
          <div className="mt-3 flex items-center gap-4">
            <span className="flex h-14 w-14 items-center justify-center rounded-xl bg-orange-700 text-2xl font-bold text-white">
              {SCORE_EXAMPLE.total}
            </span>
            <div>
              <h3 className="font-semibold">Why this is a {SCORE_EXAMPLE.total}</h3>
              <p className="text-sm text-neutral-500">{SCORE_EXAMPLE.headline}</p>
            </div>
          </div>
          <ul className="mt-6 divide-y divide-neutral-100">
            {SCORE_EXAMPLE.signals.map((s) => (
              <li key={s.type} className="flex items-center justify-between py-2.5 text-sm">
                <span className="text-neutral-700">{s.label}</span>
                <span className="font-semibold text-orange-700">+{s.points}</span>
              </li>
            ))}
          </ul>
        </div>
        <p className="mt-4 text-sm text-neutral-500">
          Every classified fire-protection permit starts from a 30-point baseline; signals add
          or subtract from there and the total is capped to 0–100. The rules are fixed and
          configurable — the same permit always scores the same way. Negative signals (old
          permits, closed permits) subtract points just as visibly.
        </p>
      </section>

      <section className="mt-20 rounded-2xl bg-orange-50 p-10 text-center">
        <h2 className="text-2xl font-bold tracking-tight">See it on your own market</h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">
          Start free and watch tomorrow&apos;s permits show up scored and sorted.
        </p>
        <Link href="/signup"
          className={ctaClasses("lg", "mt-6")}>
          Find Leads in Your Market
        </Link>
      </section>
    </div>
  );
}
