"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import type { Removal, RemovalKind } from "@permittorch/types";
import { undoRemoval } from "@/lib/api";
import { reportMutationError } from "@/components/app/sign-out";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";

const KIND_LABEL: Record<RemovalKind, string> = {
  PHONE: "Phone", EMAIL: "Email", NAME: "Name", RECORD: "Record",
};

const day = (iso: string) =>
  new Date(iso).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric", timeZone: "UTC" });

export function RemovalTable({ removals }: { removals: Removal[] }) {
  const router = useRouter();
  const getToken = useApiToken();
  const [asking, setAsking] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const undo = async (removal: Removal) => {
    setBusy(true);
    try {
      await undoRemoval(removal.id, await getToken());
      toast.success("Taken off the list");
      setAsking(null);
      router.refresh();
    } catch (err) {
      reportMutationError(err, "Could not take it off the list");
    } finally {
      setBusy(false);
    }
  };

  if (removals.length === 0) {
    return <p className="rounded-xl border border-border bg-white px-5 py-8 text-center text-sm text-stone-500">Nothing has been removed yet.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-white shadow-xs">
      <Table className="min-w-[720px]">
        <TableHeader className="bg-stone-50">
          <TableRow className="hover:bg-transparent [&>th]:h-10 [&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            <TableHead>Removed</TableHead>
            <TableHead>Kind</TableHead>
            <TableHead>Note</TableHead>
            <TableHead>Date</TableHead>
            <TableHead className="text-right">Permits</TableHead>
            <TableHead className="text-right"><span className="sr-only">Actions</span></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {removals.map((removal) => (
            <TableRow key={removal.id} className="[&>td]:px-4 [&>td]:py-3 align-top">
              <TableCell>
                <div className="font-medium text-stone-900">{removal.value}</div>
                {removal.label && <div className="text-xs text-stone-500">{removal.label}</div>}
              </TableCell>
              <TableCell className="text-sm text-stone-600">{KIND_LABEL[removal.kind]}</TableCell>
              <TableCell className="text-sm text-stone-600">{removal.note}</TableCell>
              <TableCell className="text-sm text-stone-600">{day(removal.createdAt)}</TableCell>
              <TableCell className="text-right text-sm tabular-nums">{removal.recordsAffected}</TableCell>
              <TableCell className="text-right">
                {asking === removal.id ? (
                  <div className="space-y-2 text-left">
                    <p className="max-w-xs text-xs whitespace-normal text-stone-600">
                      Taking this off the list stops it applying to later imports. It does not put anything back.
                      A value returns only if a later scrape delivers that record again.
                    </p>
                    <div className="flex justify-end gap-2">
                      <Button variant="ghost" size="sm" disabled={busy} onClick={() => setAsking(null)}>Cancel</Button>
                      <Button variant="outline" size="sm" disabled={busy} onClick={() => undo(removal)}>Take off the list</Button>
                    </div>
                  </div>
                ) : (
                  <Button variant="outline" size="sm" disabled={busy} onClick={() => setAsking(removal.id)}
                    aria-label={`Take ${removal.value} off the list`}>Take off the list</Button>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
