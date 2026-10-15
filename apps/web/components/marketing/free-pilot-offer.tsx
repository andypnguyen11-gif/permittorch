import Link from "next/link";
import { cn } from "@/lib/utils";
import { ICON_ACCENT, ctaClasses } from "@/components/marketing/cta";

const INCLUDED = [
  "Every market we cover",
  "Daily or weekly email digest for the markets you pick",
  "Full lead scoring with the reasons behind each score",
  "Saved opportunities",
  "CSV export",
];

function Check() {
  return (
    <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"
      className={cn("mt-0.5 h-4 w-4 shrink-0", ICON_ACCENT)}>
      <path fillRule="evenodd" clipRule="evenodd"
        d="M16.7 5.3a1 1 0 0 1 0 1.4l-7 7a1 1 0 0 1-1.4 0l-3-3a1 1 0 1 1 1.4-1.4L9 11.6l6.3-6.3a1 1 0 0 1 1.4 0Z" />
    </svg>
  );
}

/** What /pricing shows while PermitTorch is free during the pilot (NEXT_PUBLIC_FREE_PILOT). */
export function FreePilotOffer() {
  return (
    <div className="mx-auto max-w-md rounded-2xl border-2 border-orange-500 bg-white p-8 shadow-lg">
      <h2 className="text-lg font-semibold">Pilot access</h2>
      <p className="mt-1 text-sm text-neutral-500">Everything is included while the pilot runs.</p>
      <p className="mt-5">
        <span className="text-4xl font-bold tracking-tight">$0</span>
        <span className="text-neutral-500"> during the pilot</span>
      </p>
      <ul className="mt-6 space-y-2.5">
        {INCLUDED.map((f) => (
          <li key={f} className="flex gap-2 text-sm text-neutral-700"><Check />{f}</li>
        ))}
      </ul>
      <Link href="/signup" className={ctaClasses("default", "mt-8 w-full")}>
        Create a free account
      </Link>
    </div>
  );
}
