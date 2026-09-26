import Link from "next/link";
import type { Market } from "@permittorch/types";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";

export interface CategoryLanderContent {
  path: string;
  metaTitle: string;
  metaDescription: string;
  headline: string;
  subline: string;
  painHeading: string;
  painPoints: { title: string; body: string }[];
  howHeading: string;
  howParagraphs: string[];
  ctaHeading: string;
  ctaBody: string;
}

export const CATEGORY_LANDERS = {
  "fire-protection": {
    path: "/fire-protection-leads",
    metaTitle: "Fire Protection Leads From Live Permit Data — PermitTorch",
    metaDescription:
      "Scored fire protection leads pulled daily from public permit and inspection records: sprinkler, alarm, suppression, kitchen systems, and failed inspections.",
    headline: "Fire protection leads from live permit data.",
    subline:
      "Every fire-related opportunity in your market — sprinkler, alarm, suppression, kitchen systems, inspections — pulled from public records, scored, and explained.",
    painHeading: "Finding fire work is a part-time job you didn't sign up for",
    painPoints: [
      {
        title: "You hear about projects after they're bid",
        body: "By the time a job reaches a bid board or a GC's shortlist, the price war has started. The profitable window was weeks earlier, when the permit was filed.",
      },
      {
        title: "Permit portals weren't built for you",
        body: "Every city runs a different portal with different search, different fields, and different update schedules. Checking them all by hand costs hours a week.",
      },
      {
        title: "Most permits are noise",
        body: "Hundreds of new records a day, and a handful are fire work. Skimming plumbing and fence permits to find one sprinkler job is a bad use of anyone's time.",
      },
    ],
    howHeading: "How PermitTorch finds fire protection work",
    howParagraphs: [
      "PermitTorch monitors every active permit and inspection source in your market on a daily cycle and runs each new record through fire-specific classification rules. Sprinkler scope, alarm scope, suppression systems, kitchen hood systems, fire inspections, and code violations each get their own category — everything else is filtered out.",
      "Each fire-related record is then scored 0–100 from named signals: new commercial construction, detected fire scope, filing recency, project value, square footage, and whether a fire contractor is already listed on the permit. You see every signal behind every score.",
      "The result is a ranked feed with the address, permit number, estimated value, a one-line reason it matters, and a link to the official government record — fresh every morning, with an honest last-updated time on every source.",
    ],
    ctaHeading: "Stop refreshing permit portals.",
    ctaBody: "Start free and see the fire work in your market — scored, sorted, and explained.",
  },
  "fire-sprinkler": {
    path: "/fire-sprinkler-leads",
    metaTitle: "Fire Sprinkler Leads Before the Bid Hits the Street — PermitTorch",
    metaDescription:
      "Find fire sprinkler projects at the permit stage: new commercial builds, tenant improvements, and sprinkler-scope permits with no fire contractor listed yet.",
    headline: "Fire sprinkler leads before the bid hits the street.",
    subline:
      "New commercial builds and tenant improvements need sprinkler work early. PermitTorch spots the permits with sprinkler scope while the job is still up for grabs.",
    painHeading: "Sprinkler work gets decided early — earlier than you're hearing about it",
    painPoints: [
      {
        title: "The GC already has a number",
        body: "When a sprinkler package shows up on a bid board, three shops have usually priced it. Winning from there means cutting margin, not adding value.",
      },
      {
        title: "New construction hides in plain sight",
        body: "Every new commercial building over the code threshold needs sprinklers. Those projects are sitting in the permit record weeks before anyone calls a sprinkler contractor.",
      },
      {
        title: "TI work is scattered and constant",
        body: "Tenant improvements — head relocations, coverage changes, occupancy shifts — file year-round in small batches. Nobody has time to catch them all manually.",
      },
    ],
    howHeading: "How PermitTorch finds sprinkler work",
    howParagraphs: [
      "PermitTorch classifies permits into a dedicated fire-sprinkler category using scope language in the permit type and description — sprinkler systems, riser work, head counts, NFPA 13 references — plus new commercial construction that will require coverage.",
      "Sprinkler leads score highest when the signals stack: a new commercial project, sprinkler scope detected, filed in the last 48 hours, seven-figure project value, and no fire contractor listed on the permit yet. That last one matters most — it means the package likely hasn't been awarded.",
      "You get the address, the permit, the estimated value, and the reason it scored the way it did, with a link to the official record so you can verify scope before you pick up the phone.",
    ],
    ctaHeading: "Be the first sprinkler contractor to call.",
    ctaBody: "Start free and see this week's sprinkler-scope permits in your market.",
  },
  "fire-alarm": {
    path: "/fire-alarm-leads",
    metaTitle: "Fire Alarm Leads Straight From the Permit Record — PermitTorch",
    metaDescription:
      "Find fire alarm projects early: alarm-scope permits, tenant upfits, occupancy changes, and failed inspections that need a fire alarm contractor.",
    headline: "Fire alarm leads straight from the permit record.",
    subline:
      "Alarm work rides on tenant upfits, occupancy changes, and inspection failures. PermitTorch catches all three the day they hit the public record.",
    painHeading: "Alarm opportunities are frequent, small, and easy to miss",
    painPoints: [
      {
        title: "Upfits move fast",
        body: "A tenant improvement goes from permit to occupied in weeks. If you find the alarm scope a month late, someone else's panel is already on the wall.",
      },
      {
        title: "Failed inspections don't advertise",
        body: "A failed fire inspection or a violation notice is a ready-to-buy customer — but it's buried in inspection records nobody reads systematically.",
      },
      {
        title: "Monitoring contracts follow the install",
        body: "Every install you miss is also the recurring monitoring revenue you miss. The cost of a missed alarm lead compounds for years.",
      },
    ],
    howHeading: "How PermitTorch finds alarm work",
    howParagraphs: [
      "PermitTorch classifies permits into a dedicated fire-alarm category from scope language in permit types and descriptions — fire alarm systems, panel replacements, notification devices, NFPA 72 references — across every source in your market.",
      "It also watches the records that generate alarm work indirectly: commercial tenant improvements and occupancy changes that trigger alarm requirements, and failed fire inspections or violation corrections where the owner has to act now.",
      "Every alarm lead is scored 0–100 with visible signals — recency, project value, scope, whether a fire contractor is already listed — so your first calls of the morning go to the jobs most worth winning.",
    ],
    ctaHeading: "Catch the upfits and the failed inspections.",
    ctaBody: "Start free and see this week's alarm opportunities in your market.",
  },
} satisfies Record<string, CategoryLanderContent>;

export function CategoryLander({ content, markets }: { content: CategoryLanderContent; markets: Market[] }) {
  return (
    <>
      <section className="mx-auto max-w-4xl px-4 pb-16 pt-24 text-center sm:px-6">
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">{content.headline}</h1>
        <p className="mx-auto mt-5 max-w-2xl text-lg leading-relaxed text-neutral-600">{content.subline}</p>
        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Link href="/signup"
            className={cn(buttonVariants({ size: "lg" }), "bg-orange-500 px-8 text-white hover:bg-orange-600")}>
            Find Leads in Your Market
          </Link>
          <Link href="#sample-leads" className={cn(buttonVariants({ size: "lg", variant: "outline" }), "px-8")}>
            See Sample Leads
          </Link>
        </div>
      </section>

      <section className="border-y border-neutral-200 bg-neutral-50">
        <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
          <h2 className="text-center text-2xl font-bold tracking-tight">{content.painHeading}</h2>
          <div className="mt-10 grid gap-6 md:grid-cols-3">
            {content.painPoints.map((p) => (
              <div key={p.title} className="rounded-xl border border-neutral-200 bg-white p-6">
                <h3 className="font-semibold">{p.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-neutral-600">{p.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
        <h2 className="text-2xl font-bold tracking-tight">{content.howHeading}</h2>
        {content.howParagraphs.map((p) => (
          <p key={p.slice(0, 32)} className="mt-4 leading-relaxed text-neutral-700">{p}</p>
        ))}
      </section>

      <section id="sample-leads" className="scroll-mt-20 border-y border-orange-100 bg-orange-50">
        <div className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
          <h2 className="text-center text-2xl font-bold tracking-tight">
            Get 5–10 sample leads from your market — free
          </h2>
          <div className="mt-8"><SampleLeadsForm markets={markets} /></div>
        </div>
      </section>

      <section className="mx-auto max-w-4xl px-4 py-16 text-center sm:px-6">
        <h2 className="text-2xl font-bold tracking-tight">{content.ctaHeading}</h2>
        <p className="mx-auto mt-3 max-w-md text-neutral-600">{content.ctaBody}</p>
        <Link href="/signup"
          className={cn(buttonVariants({ size: "lg" }), "mt-6 bg-orange-500 px-8 text-white hover:bg-orange-600")}>
          Start Free
        </Link>
      </section>
    </>
  );
}
