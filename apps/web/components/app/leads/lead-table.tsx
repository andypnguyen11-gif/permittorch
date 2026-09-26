import Link from "next/link";
import { ChevronRight, SearchX } from "lucide-react";
import type { LeadSummary } from "@permittorch/types";
import { Badge } from "@/components/ui/badge";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { ScoreBadge } from "@/components/app/score-badge";
import { CategoryIcon } from "@/components/app/category-chip";
import { formatDate, formatRelative, formatValueShort } from "@/components/app/format";
import { cn } from "@/lib/utils";

export function EmptyState({ icon: Icon = SearchX, title, description, action }: {
  icon?: typeof SearchX; title: string; description?: React.ReactNode; action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-stone-300 bg-white px-6 py-16 text-center">
      <span className="mb-1 flex size-11 items-center justify-center rounded-full bg-stone-100">
        <Icon className="size-5 text-stone-400" aria-hidden />
      </span>
      <p className="font-semibold text-stone-800">{title}</p>
      {description && <p className="max-w-sm text-sm text-stone-500">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function LeadTable({ leads, className, emptyState }: {
  leads: LeadSummary[];
  /** Applied to the scroll container — pass a max-height to get a sticky header. */
  className?: string;
  emptyState?: React.ReactNode;
}) {
  if (leads.length === 0) {
    return emptyState ?? (
      <EmptyState
        title="No leads match these filters"
        description="Try widening the score band or age range, or clearing your search."
      />
    );
  }

  return (
    <div
      className={cn(
        // The shadcn Table wraps itself in an overflow container; make it visible so
        // THIS element is the scroll container and the header can stick to it.
        "overflow-auto rounded-xl border border-border bg-white shadow-xs [&_[data-slot=table-container]]:overflow-visible",
        className,
      )}
    >
      <Table className="min-w-[960px]">
        <TableHeader className="sticky top-0 z-10 bg-stone-50/95 backdrop-blur supports-backdrop-filter:bg-stone-50/80">
          <TableRow className="hover:bg-transparent [&>th]:h-10 [&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            <TableHead>Lead</TableHead>
            <TableHead className="text-center">Score</TableHead>
            <TableHead>Location</TableHead>
            <TableHead>Filed</TableHead>
            <TableHead className="text-right">Value</TableHead>
            <TableHead>Why this matters</TableHead>
            <TableHead><span className="sr-only">Open</span></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {leads.map((lead) => (
            <TableRow key={lead.id} className="group relative hover:bg-orange-50/40 [&>td]:px-4 [&>td]:py-3">
              <TableCell className="min-w-72 max-w-96 whitespace-normal">
                <div className="flex items-center gap-3">
                  <CategoryIcon category={lead.category} />
                  <div className="min-w-0">
                    {/* Stretched link makes the whole row clickable without nested anchors. */}
                    <Link
                      href={`/app/leads/${lead.id}`}
                      className="line-clamp-2 font-medium text-stone-900 outline-none after:absolute after:inset-0 after:rounded-md group-hover:text-orange-600 focus-visible:after:ring-2 focus-visible:after:ring-ring/60 focus-visible:after:ring-inset"
                    >
                      {lead.title}
                    </Link>
                    <div className="mt-0.5 flex items-center gap-2 text-xs text-stone-500">
                      {lead.permitType && <span className="truncate">{lead.permitType}</span>}
                      {lead.isNew && (
                        <Badge className="h-4.5 bg-orange-100 px-1.5 text-[10px] text-orange-700">New</Badge>
                      )}
                    </div>
                  </div>
                </div>
              </TableCell>
              <TableCell className="text-center"><ScoreBadge score={lead.score} showLabel /></TableCell>
              <TableCell className="text-sm">
                <div className="text-stone-800">{lead.address ?? "Address unavailable"}</div>
                <div className="text-xs text-stone-500">{lead.city}, {lead.state}</div>
              </TableCell>
              <TableCell className="text-sm">
                <div className="text-stone-800">{formatDate(lead.filedDate)}</div>
                {lead.filedDate && (
                  <div className="text-xs text-stone-500">{formatRelative(lead.filedDate)}</div>
                )}
              </TableCell>
              <TableCell className="text-right text-sm font-semibold tabular-nums text-stone-800">
                {formatValueShort(lead.estimatedValue)}
              </TableCell>
              <TableCell className="max-w-72 min-w-56 text-sm whitespace-normal text-stone-600">
                <p className="line-clamp-2">{lead.reason}</p>
              </TableCell>
              <TableCell className="w-8 text-stone-300 group-hover:text-orange-500">
                <ChevronRight className="size-4" aria-hidden />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
