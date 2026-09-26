export interface BlogSection { heading?: string; body: string[] }
export interface BlogPost {
  slug: string;
  title: string;
  description: string;
  publishedAt: string; // ISO date
  sections: BlogSection[];
}

export const blogPosts: BlogPost[] = [
  {
    slug: "how-fire-sprinkler-contractors-find-leads",
    title: "How Fire Sprinkler Contractors Find Leads",
    description:
      "The four ways sprinkler contractors find work today, why most of them start too late, and how permit records move you to the front of the line.",
    publishedAt: "2026-08-19",
    sections: [
      {
        body: [
          "Most fire sprinkler contractors find work the same four ways: relationships with general contractors, bid boards, referrals, and repeat customers. All four can keep a shop busy. None of them puts you first in line — and in sprinkler work, the order of the line decides your margin.",
        ],
      },
      {
        heading: "The problem with waiting for the bid",
        body: [
          "By the time a sprinkler package appears on a bid board, the general contractor has usually already collected numbers from the shops they know. If you find the job there, you are the fourth bid — invited to lose, or to win on price alone. The same is true of relationship work: it is only as wide as your rolodex, and every GC you know also knows your competitors.",
          "The profitable moment is earlier, before the package is priced, when you can shape scope and be the contractor the GC calls instead of the one who calls last.",
        ],
      },
      {
        heading: "Permits are the earlier signal",
        body: [
          "Before ground breaks on almost any commercial project, someone files a building permit. That permit is a public record, and it is remarkably specific: address, project description, estimated value, filing date, and often the parties involved.",
          "For a sprinkler contractor, three permit patterns matter most. New commercial construction above the code threshold will need a sprinkler system — full stop. Tenant improvements frequently trigger head relocations, coverage changes, or full retrofits. And any permit whose description mentions sprinkler scope directly is a project actively shopping for your trade.",
        ],
      },
      {
        heading: "What to look for in the record",
        body: [
          "Recency first: a permit filed this week is a conversation; a permit filed four months ago is a spreadsheet entry. Then valuation and square footage, which sort the seven-figure builds from the closet remodels. Finally — and this is the one most people miss — look at who is listed on the permit. If no fire contractor appears yet, the package likely has not been awarded. That is your call to make.",
        ],
      },
      {
        heading: "Do it by hand, or let software watch",
        body: [
          "You can build this habit manually: bookmark every permit portal in your market, check them weekly, and keep a spreadsheet. It works, and plenty of shops do it — it just costs hours every week and misses everything filed between checks.",
          "Or let software do the watching. PermitTorch monitors the permit sources in your market daily, filters to fire-related work, and scores each opportunity by value, scope, timing, and whether a fire contractor is already attached. Either way, the contractors who read permits first get the first conversation — and the first conversation wins more work than the fourth bid ever will.",
        ],
      },
    ],
  },
  {
    slug: "using-building-permits-for-lead-generation",
    title: "Using Building Permits for Lead Generation",
    description:
      "Building permits are the most underused lead source in the trades: public, factual, and early. Here is how to turn them into a repeatable pipeline.",
    publishedAt: "2026-08-12",
    sections: [
      {
        body: [
          "Every commercial construction project in America leaves a paper trail, and it starts with a building permit. Permits are public records — anyone can read them — yet most contractors never do. That makes them the most underused lead source in the trades: factual, early, and free of the resale problem that plagues purchased lead lists.",
        ],
      },
      {
        heading: "What a permit actually tells you",
        body: [
          "A typical commercial permit record includes the project address, a permit type, a scope description, an estimated valuation, filing and issue dates, current status, and frequently the owner, applicant, and any contractors already attached. Read together, those fields answer the questions a salesperson actually has: What is being built? How big is it? How new is this? And has anyone in my trade already won it?",
        ],
      },
      {
        heading: "Why permits beat purchased lead lists",
        body: [
          "A purchased lead has usually been sold to five of your competitors before it reaches you, and it decays fast. A permit is different: it is a primary source. Nobody edited it for marketing, nobody resold it, and it appears at the earliest public moment of the project — often weeks before bid boards or word of mouth catch up. The contractor who reads it first has real first-mover advantage.",
        ],
      },
      {
        heading: "The catch: it is a grind",
        body: [
          "Here is why almost nobody does this. Every jurisdiction runs its own portal with its own search, its own field names, and its own update schedule. A single metro area can span a dozen sources. Checking them all weekly is hours of clicking, and most of what you will read is irrelevant to your trade — fences, water heaters, reroofs.",
        ],
      },
      {
        heading: "Make it repeatable",
        body: [
          "Whether you do it by hand or with software, the workflow is the same. Filter to your trade with keywords in the permit type and description. Sort by filing date — recency is everything, because the first call usually gets the meeting. Weight by valuation and square footage so the big jobs surface. And flag permits where no contractor in your trade is listed yet, because those projects have not awarded your scope.",
          "That is exactly the pipeline PermitTorch automates for fire protection: every source in your market checked daily, filtered to fire work, scored 0–100 with reasons you can read. However you run it, start reading permits — your next customer already filed one.",
        ],
      },
    ],
  },
  {
    slug: "how-to-find-commercial-fire-protection-projects",
    title: "How to Find Commercial Fire Protection Projects",
    description:
      "The five public-record signals that generate commercial fire protection work — new builds, tenant improvements, restaurants, warehouses, and failed inspections.",
    publishedAt: "2026-08-05",
    sections: [
      {
        body: [
          "Commercial fire protection work does not appear out of nowhere. It is generated by a handful of predictable events — and nearly every one of them shows up in public records before any bid is issued. If you know which signals to watch, you can build a project pipeline out of information anyone can access.",
        ],
      },
      {
        heading: "Signal 1: New commercial construction",
        body: [
          "Any new commercial building over the code threshold needs sprinklers, alarms, or both. New-construction permits carry the clearest budget signals in the public record: estimated valuation and square footage. A seven-figure new build filed this week is the single strongest fire protection lead that exists.",
        ],
      },
      {
        heading: "Signal 2: Tenant improvements and occupancy changes",
        body: [
          "TI permits are smaller but constant. A new tenant means reconfigured walls, which means relocated sprinkler heads and modified alarm coverage — and an occupancy change can trigger entirely new system requirements. Because TIs file year-round in every submarket, they are the steadiest source of mid-size fire work.",
        ],
      },
      {
        heading: "Signal 3: Restaurants and commercial kitchens",
        body: [
          "Every new restaurant, ghost kitchen, and cafeteria needs a kitchen hood suppression system, and health-and-fire review makes these projects easy to spot in permit descriptions. Restaurant build-outs also move fast, so recency matters more here than anywhere else.",
        ],
      },
      {
        heading: "Signal 4: Failed inspections and violations",
        body: [
          "A failed fire inspection or a code violation is a customer who has to buy — the fire marshal said so. These records are the most neglected signal in the industry because they sit in inspection systems separate from building permits. The contractor who monitors them gets warm calls with a deadline attached.",
        ],
      },
      {
        heading: "Prioritize, then call",
        body: [
          "Not every signal deserves a phone call. Rank what you find: project value and square footage set the size of the prize, filing recency sets the urgency, and a permit with no fire contractor listed means the scope is probably still open. Work the list top-down every morning.",
          "That ranking is precisely what PermitTorch computes — a 0–100 score per opportunity, with every contributing signal spelled out. But scored or not, the projects are already sitting in the public record. The only question is which contractor reads it first.",
        ],
      },
    ],
  },
];

export function getBlogPost(slug: string): BlogPost | undefined {
  return blogPosts.find((p) => p.slug === slug);
}
