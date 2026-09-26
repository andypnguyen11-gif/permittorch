import Link from "next/link";
import { FlameMark } from "@/components/marketing/site-nav";

const COLUMNS = [
  {
    heading: "Product",
    links: [
      { label: "Fire Protection Leads", href: "/fire-protection-leads" },
      { label: "Fire Sprinkler Leads", href: "/fire-sprinkler-leads" },
      { label: "Fire Alarm Leads", href: "/fire-alarm-leads" },
      { label: "How It Works", href: "/how-it-works" },
      { label: "Pricing", href: "/pricing" },
    ],
  },
  {
    heading: "Company",
    links: [
      { label: "Markets", href: "/locations" },
      { label: "Blog", href: "/blog" },
      { label: "Login", href: "/login" },
      { label: "Start Free", href: "/signup" },
    ],
  },
  {
    heading: "Legal",
    links: [
      { label: "Terms of Service", href: "/terms" },
      { label: "Privacy Policy", href: "/privacy" },
    ],
  },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-neutral-200 bg-neutral-50">
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-14 sm:px-6 md:grid-cols-4">
        <div>
          <div className="flex items-center gap-2 text-lg font-bold">
            <FlameMark className="h-5 w-5 text-orange-500" />
            <span>Permit<span className="text-orange-500">Torch</span></span>
          </div>
          <p className="mt-3 max-w-xs text-sm leading-relaxed text-neutral-500">
            Permit intelligence for fire-protection contractors.
          </p>
        </div>
        {COLUMNS.map((col) => (
          <div key={col.heading}>
            <h3 className="text-sm font-semibold text-neutral-900">{col.heading}</h3>
            <ul className="mt-3 space-y-2">
              {col.links.map((l) => (
                <li key={l.href}>
                  <Link href={l.href} className="text-sm text-neutral-500 hover:text-neutral-900">
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t border-neutral-200">
        <p className="mx-auto max-w-6xl px-4 py-6 text-xs leading-relaxed text-neutral-400 sm:px-6">
          &copy; 2026 PermitTorch. Lead data is derived from publicly available government permit and
          inspection records. PermitTorch does not guarantee accuracy or completeness — verify every
          opportunity independently. Records may be delayed or corrected by the issuing jurisdiction.
        </p>
      </div>
    </footer>
  );
}
