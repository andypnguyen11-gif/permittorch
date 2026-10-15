import { Fragment } from "react";
import type { PreSprinklerKind, PreSprinklerLead } from "@permittorch/types";
import { emailHref, formatDate, phoneHref } from "@/components/app/format";
import { EmptyState } from "@/components/app/leads/lead-table";
import { ScoreBadge } from "@/components/app/score-badge";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import { contactWho, groupPreSprinklerLeads } from "./pre-sprinkler-groups";

const KIND_CLASS: Record<PreSprinklerKind, string> = {
  NEW_HOME: "bg-orange-100 text-orange-700 ring-orange-200",
  ADU: "bg-sky-50 text-sky-700 ring-sky-200",
  ADDITION: "bg-stone-100 text-stone-700 ring-stone-200",
};

const COLUMNS = ["Kind", "Sprinkler", "Address", "Record", "Status", "Filed", "Size", "Contacts", "Score"];

function size(lead: PreSprinklerLead): string {
  return lead.sizeKnown && lead.squareFeet != null ? `${lead.squareFeet.toLocaleString("en-US")} sq ft` : "—";
}

function LeadRow({ lead, grouped }: { lead: PreSprinklerLead; grouped: boolean }) {
  return (
    <TableRow data-standing={lead.standing} className="align-top [&>td]:px-4 [&>td]:py-2.5">
      <TableCell className={cn(grouped && "pl-8")}>
        <span className={cn("inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset", KIND_CLASS[lead.kind])}>
          {lead.kindLabel}
        </span>
      </TableCell>
      <TableCell className="max-w-56 text-sm">
        <p className={cn("font-medium", lead.sprinklerCheckStatus === "NONE_FOUND" ? "text-green-700" : "text-stone-700")}
          title={lead.sprinklerTooltip ?? undefined}>
          {lead.sprinklerLabel}
        </p>
        <p className="text-xs text-stone-500">{lead.requirementLabel}</p>
        {lead.standing !== "OPEN" && lead.standing !== "SPRINKLER_FILED" && (
          <p className="text-xs font-medium text-amber-700">{lead.standingLabel}</p>
        )}
      </TableCell>
      <TableCell className="text-sm">
        <p>{lead.street ?? "—"}</p>
        <p className="text-xs text-stone-500">{[lead.city, lead.zip].filter(Boolean).join(" ")}</p>
      </TableCell>
      <TableCell className="text-sm">
        {lead.recordUrl
          ? <a href={lead.recordUrl} target="_blank" rel="noopener noreferrer" className="font-mono text-xs text-orange-700 underline-offset-2 hover:underline">{lead.permitNumber}</a>
          : <span className="font-mono text-xs">{lead.permitNumber}</span>}
        {lead.permitTypeLabel && <p className="text-xs text-stone-500">{lead.permitTypeLabel}</p>}
      </TableCell>
      <TableCell className="text-sm text-stone-700">{lead.status ?? "—"}</TableCell>
      <TableCell className="text-sm whitespace-nowrap text-stone-700">{formatDate(lead.filedOn)}</TableCell>
      <TableCell className="text-sm whitespace-nowrap tabular-nums text-stone-700">{size(lead)}</TableCell>
      <TableCell className="text-sm">
        {lead.contacts.length === 0 ? <span className="text-stone-400">—</span> : (
          <ul className="space-y-1.5">
            {lead.contacts.map((c, i) => {
              const tel = phoneHref(c.phone);
              const mail = emailHref(c.email);
              return (
                <li key={i}>
                  <span className="text-xs font-semibold text-stone-500 uppercase">{c.roleLabel ?? c.role.toLowerCase()}</span>{" "}
                  <span>{contactWho(c)}</span>
                  {c.phone && <span className="block text-xs">{tel ? <a href={tel} className="text-orange-700 hover:underline">{c.phone}</a> : c.phone}</span>}
                  {c.email && <span className="block text-xs">{mail ? <a href={mail} className="text-orange-700 hover:underline">{c.email}</a> : c.email}</span>}
                </li>
              );
            })}
          </ul>
        )}
      </TableCell>
      <TableCell className="text-right"><ScoreBadge score={lead.score} /></TableCell>
    </TableRow>
  );
}

export function PreSprinklerTable({ leads }: { leads: PreSprinklerLead[] }) {
  if (leads.length === 0) {
    return <EmptyState title="No leads match" description="Try another market, kind or standing, or widen the filing dates." />;
  }
  const groups = groupPreSprinklerLeads(leads);
  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-white shadow-xs">
      <Table className="min-w-[1100px]">
        <TableHeader className="bg-stone-50">
          <TableRow className="hover:bg-transparent [&>th]:h-10 [&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            {COLUMNS.map((c) => <TableHead key={c} className={c === "Score" ? "text-right" : undefined}>{c}</TableHead>)}
          </TableRow>
        </TableHeader>
        <TableBody>
          {groups.map((group) => (
            <Fragment key={group.key}>
              {group.heading && (
                <TableRow data-group-heading className="bg-orange-50/60 hover:bg-orange-50/60">
                  <TableCell colSpan={COLUMNS.length} className="px-4 py-2 text-sm font-semibold text-stone-800">
                    {group.heading}
                  </TableCell>
                </TableRow>
              )}
              {group.leads.map((lead) => <LeadRow key={lead.id} lead={lead} grouped={group.heading !== null} />)}
            </Fragment>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
