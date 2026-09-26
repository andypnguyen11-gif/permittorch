import Link from "next/link";
import { Clock, Mail } from "lucide-react";
import type { AccountMe } from "@permittorch/types";
import { SectionCard } from "@/components/app/section-card";
import { formatValueShort } from "@/components/app/format";

export function digestScheduleLabel(frequency: AccountMe["digestFrequency"]): string {
  if (frequency === "DAILY") return "Next digest tomorrow at 6:00 AM";
  if (frequency === "WEEKLY") return "Next digest Monday at 6:00 AM";
  return "Digest is off — turn it on in Alerts";
}

export function DigestPreview({ me, stats }: {
  me: AccountMe;
  stats: { newOpportunities: number; hotLeads: number; totalValue: number };
}) {
  const label = me.digestFrequency === "DAILY" ? "daily digest" : me.digestFrequency === "WEEKLY" ? "weekly digest" : "digest preview";
  return (
    <SectionCard
      title={<span className="flex items-center gap-2"><Mail className="size-4 text-stone-400" aria-hidden />Your email digest</span>}
      action={<Link href="/app/alerts" className="text-sm font-medium text-orange-600 hover:underline">Settings</Link>}>
      <div className="space-y-3 text-sm">
        <p className="text-stone-600">Here’s what your {label} would include right now.</p>
        <dl className="space-y-1.5">
          <div className="flex justify-between"><dt className="text-stone-500">New opportunities</dt><dd className="font-semibold tabular-nums">{stats.newOpportunities}</dd></div>
          <div className="flex justify-between"><dt className="text-stone-500">Hot leads</dt><dd className="font-semibold tabular-nums">{stats.hotLeads}</dd></div>
          <div className="flex justify-between"><dt className="text-stone-500">Total project value</dt><dd className="font-semibold tabular-nums">{formatValueShort(stats.totalValue)}</dd></div>
        </dl>
        <p className="flex items-center gap-1.5 border-t border-border pt-3 text-xs text-stone-400">
          <Clock className="size-3" aria-hidden />
          {digestScheduleLabel(me.digestFrequency)}
        </p>
      </div>
    </SectionCard>
  );
}
