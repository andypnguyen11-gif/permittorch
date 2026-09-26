import type { ScraperRunSummary } from "@permittorch/types";
import { formatRelative } from "@/components/app/format";
import { EmptyState } from "@/components/app/leads/lead-table";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

export function formatDuration(seconds: number): string {
  const s = Math.round(seconds);
  if (s < 60) return `${s}s`;
  return `${Math.floor(s / 60)}m ${String(s % 60).padStart(2, "0")}s`;
}

export function RunsTable({ runs }: { runs: ScraperRunSummary[] }) {
  if (runs.length === 0) {
    return <EmptyState title="No runs recorded" description="Scraper runs appear here after the next ingestion pass." />;
  }
  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-white shadow-xs">
      <Table className="min-w-[760px]">
        <TableHeader className="bg-stone-50">
          <TableRow className="hover:bg-transparent [&>th]:h-10 [&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            <TableHead>Run</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Started</TableHead>
            <TableHead className="text-right">Imported</TableHead>
            <TableHead className="text-right">Duplicates</TableHead>
            <TableHead className="text-right">Failures</TableHead>
            <TableHead className="text-right">Duration</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {runs.map((run) => {
            const ok = run.status === "SUCCEEDED";
            const flagged = !ok || run.failures > 0;
            return (
              <TableRow key={run.id} data-flagged={flagged || undefined}
                className={cn("[&>td]:px-4 [&>td]:py-2.5", flagged && "bg-red-50/50 hover:bg-red-50")}>
                <TableCell className="font-mono text-xs text-stone-600">{run.apifyRunId}</TableCell>
                <TableCell>
                  <span className={cn("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium",
                    ok ? "bg-green-100 text-green-700" : "bg-red-100 text-red-700")}>
                    {run.status}
                  </span>
                </TableCell>
                <TableCell className="text-sm text-stone-600">{formatRelative(run.startedAt)}</TableCell>
                <TableCell className="text-right text-sm tabular-nums">{run.recordsImported.toLocaleString("en-US")}</TableCell>
                <TableCell className="text-right text-sm tabular-nums">{run.duplicatesSkipped.toLocaleString("en-US")}</TableCell>
                <TableCell className={cn("text-right text-sm tabular-nums", run.failures > 0 && "font-semibold text-red-600")}>
                  {run.failures}
                </TableCell>
                <TableCell className="text-right text-sm tabular-nums">{formatDuration(run.durationSeconds)}</TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
