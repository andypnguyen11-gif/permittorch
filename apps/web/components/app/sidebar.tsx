"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell, Bookmark, CreditCard, Database, Globe, LayoutGrid,
  ListChecks, PlayCircle, Sparkles, Users as UsersIcon, User, type LucideIcon,
} from "lucide-react";
import type { AccountMe, PlanTier } from "@permittorch/types";
import { BrandMark } from "@/components/app/brand";
import { cn } from "@/lib/utils";

const MAIN_NAV = [
  { href: "/app", label: "Overview", icon: LayoutGrid },
  { href: "/app/leads", label: "Leads", icon: ListChecks },
  { href: "/app/saved", label: "Saved", icon: Bookmark },
  { href: "/app/alerts", label: "Alerts", icon: Bell },
  { href: "/app/markets", label: "Markets", icon: Globe },
  { href: "/app/account", label: "Account", icon: User },
];

const ADMIN_NAV = [
  { href: "/app/admin/sources", label: "Sources", icon: Database },
  { href: "/app/admin/runs", label: "Runs", icon: PlayCircle },
  { href: "/app/admin/users", label: "Users", icon: UsersIcon },
  { href: "/app/admin/subscriptions", label: "Subscriptions", icon: CreditCard },
];

function NavLink({ href, label, icon: Icon, active, onNavigate }: {
  href: string; label: string; icon: LucideIcon; active: boolean; onNavigate?: () => void;
}) {
  return (
    <Link
      href={href}
      onClick={onNavigate}
      aria-current={active ? "page" : undefined}
      className={cn(
        "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
        active
          ? "bg-orange-50 text-orange-600"
          : "text-stone-600 hover:bg-stone-100 hover:text-stone-900",
      )}
    >
      <Icon className={cn("size-4", active ? "text-orange-500" : "text-stone-400")} aria-hidden />
      {label}
    </Link>
  );
}

export function Sidebar({ role, plan, onNavigate }: {
  role: AccountMe["role"];
  plan?: PlanTier | null;
  onNavigate?: () => void;
}) {
  const pathname = usePathname();
  const isActive = (href: string) =>
    href === "/app" ? pathname === "/app" : pathname === href || pathname.startsWith(`${href}/`);
  const showUpsell = plan !== undefined && plan !== "TERRITORY";

  return (
    <aside className="flex h-full w-60 shrink-0 flex-col border-r border-border bg-white">
      <Link
        href="/app"
        onClick={onNavigate}
        aria-label="PermitTorch"
        className="mx-3 mt-4 mb-6 rounded-lg px-2 py-1.5 outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        <BrandMark />
      </Link>
      <nav aria-label="Main" className="flex-1 space-y-1 overflow-y-auto px-3">
        {MAIN_NAV.map((item) => (
          <NavLink key={item.href} {...item} active={isActive(item.href)} onNavigate={onNavigate} />
        ))}
        {role === "SUPER_ADMIN" && (
          <div className="pt-6">
            <p className="px-3 pb-2 text-[11px] font-semibold uppercase tracking-wider text-stone-400">
              Admin
            </p>
            <div className="space-y-1">
              {ADMIN_NAV.map((item) => (
                <NavLink key={item.href} {...item} active={isActive(item.href)} onNavigate={onNavigate} />
              ))}
            </div>
          </div>
        )}
      </nav>
      {showUpsell && (
        <div className="m-3 rounded-xl border border-orange-100 bg-gradient-to-b from-orange-50 to-white p-4">
          <p className="flex items-center gap-1.5 text-sm font-semibold text-stone-900">
            <Sparkles className="size-4 text-orange-500" aria-hidden />
            Unlock more markets
          </p>
          <p className="mt-1 text-xs text-stone-500">
            More markets, more users, and advanced filters.
          </p>
          <Link
            href="/app/account"
            onClick={onNavigate}
            className="mt-3 flex h-8 items-center justify-center rounded-lg bg-orange-500 text-sm font-medium text-white transition-colors hover:bg-orange-600 outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            Upgrade plan
          </Link>
        </div>
      )}
    </aside>
  );
}
