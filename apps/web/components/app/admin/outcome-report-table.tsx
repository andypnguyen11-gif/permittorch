import type { LeadOutcomeReport, OutcomeBand } from "@permittorch/types";
import { BAND_LABELS } from "@/components/app/score-badge";
import { scoreBand } from "@/components/app/format";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

function shareCell(band: OutcomeBand): string {
  if (band.total === 0) return "—";
  if (band.worthContactingShare === null) return "Too few answers to read";
  return `${Math.round(band.worthContactingShare * 100)}%`;
}

/** How users tagged leads, one row per ten-point band of the score each lead had when tagged. */
export function OutcomeReportTable({ report }: { report: LeadOutcomeReport }) {
  return (
    <div className="overflow-auto rounded-xl border border-border bg-white shadow-xs">
      <Table className="min-w-[760px]">
        <TableHeader>
          <TableRow className="[&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            <TableHead>Score band</TableHead>
            <TableHead>Badge</TableHead>
            <TableHead className="text-right">Worth contacting</TableHead>
            <TableHead className="text-right">Already awarded</TableHead>
            <TableHead className="text-right">Wrong trade</TableHead>
            <TableHead className="text-right">Too late</TableHead>
            <TableHead className="text-right">Total</TableHead>
            <TableHead className="text-right">Share worth contacting</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {report.bands.map((b) => (
            <TableRow key={b.low} className="[&>td]:px-4 [&>td]:py-2.5 [&>td]:text-sm">
              <TableCell className="font-medium text-stone-900">{`${b.low}–${b.high}`}</TableCell>
              <TableCell className="text-stone-500">{BAND_LABELS[scoreBand(b.low)]}</TableCell>
              <TableCell className="text-right tabular-nums">{b.worthContacting}</TableCell>
              <TableCell className="text-right tabular-nums">{b.alreadyAwarded}</TableCell>
              <TableCell className="text-right tabular-nums">{b.wrongTrade}</TableCell>
              <TableCell className="text-right tabular-nums">{b.tooLate}</TableCell>
              <TableCell className="text-right font-semibold tabular-nums">{b.total}</TableCell>
              <TableCell className={b.worthContactingShare === null ? "text-right text-xs text-stone-400" : "text-right font-semibold tabular-nums"}>
                {shareCell(b)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
