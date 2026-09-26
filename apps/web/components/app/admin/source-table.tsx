"use client";
import { useState } from "react";
import { toast } from "sonner";
import type { AdminSource } from "@permittorch/types";
import { setSourceActive } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { formatRelative } from "@/components/app/format";
import { HEALTH_DOT, HEALTH_LABEL, HEALTH_TEXT } from "@/components/app/overview/source-health-panel";
import { Button } from "@/components/ui/button";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

export function SourceTable({ sources }: { sources: AdminSource[] }) {
  const getToken = useApiToken();
  const [rows, setRows] = useState(sources);
  const [busyId, setBusyId] = useState<string | null>(null);

  const toggle = async (source: AdminSource) => {
    const nextActive = !source.active;
    setBusyId(source.id);
    setRows((prev) => prev.map((s) => (s.id === source.id ? { ...s, active: nextActive } : s)));
    try {
      await setSourceActive(source.id, nextActive, await getToken());
      toast.success(`${source.name} ${nextActive ? "enabled" : "disabled"}`);
    } catch {
      setRows((prev) => prev.map((s) => (s.id === source.id ? { ...s, active: source.active } : s)));
      toast.error("Could not update source");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-white shadow-xs">
      <Table className="min-w-[720px]">
        <TableHeader className="bg-stone-50">
          <TableRow className="hover:bg-transparent [&>th]:h-10 [&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            <TableHead>Source</TableHead>
            <TableHead>Health</TableHead>
            <TableHead>Last successful run</TableHead>
            <TableHead className="text-right">Records (last run)</TableHead>
            <TableHead className="text-right"><span className="sr-only">Actions</span></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((source) => (
            <TableRow key={source.id} className={cn("[&>td]:px-4 [&>td]:py-3", !source.active && "bg-stone-50/60")}>
              <TableCell>
                <div className={cn("font-medium", source.active ? "text-stone-900" : "text-stone-400")}>{source.name}</div>
                <div className="text-xs text-stone-500">{source.city}, {source.state}{!source.active && " · Paused"}</div>
              </TableCell>
              <TableCell>
                <span className={cn("flex items-center gap-2 text-sm font-medium", HEALTH_TEXT[source.healthStatus])}>
                  <span className={cn("size-2 rounded-full", HEALTH_DOT[source.healthStatus])} aria-hidden />
                  {HEALTH_LABEL[source.healthStatus]}
                </span>
              </TableCell>
              <TableCell className="text-sm text-stone-600">
                {formatRelative(source.lastSuccessfulRunAt)}
              </TableCell>
              <TableCell className="text-right text-sm tabular-nums">
                {source.recordsLastRun.toLocaleString("en-US")}
              </TableCell>
              <TableCell className="text-right">
                <Button variant="outline" size="sm" disabled={busyId === source.id}
                  onClick={() => toggle(source)} aria-label={`${source.active ? "Disable" : "Enable"} ${source.name}`}>
                  {source.active ? "Disable" : "Enable"}
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
