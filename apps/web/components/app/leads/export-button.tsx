"use client";
import { useState } from "react";
import { Download } from "lucide-react";
import { toast } from "sonner";
import { ApiError, exportLeadsCsv, type LeadsQuery } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { Button } from "@/components/ui/button";

const FILE_NAME = "permittorch-leads.csv";

function download(blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = FILE_NAME;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** Downloads the leads that match the filters on screen. The API decides who may export. */
export function ExportButton({ query }: { query: LeadsQuery }) {
  const getToken = useApiToken();
  const [pending, setPending] = useState(false);

  const exportLeads = async () => {
    setPending(true);
    try {
      const { blob, truncated } = await exportLeadsCsv(query, await getToken());
      download(blob);
      if (truncated) toast("Export holds the first 5,000 leads. Narrow the filters to export the rest.");
      else toast.success("Export downloaded");
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) {
        toast.error("CSV export is part of the Pro and Territory plans.");
      } else {
        reportMutationError(err, "Could not export leads");
      }
    } finally {
      setPending(false);
    }
  };

  return (
    <Button variant="outline" onClick={exportLeads} disabled={pending} className="h-9 px-3">
      <Download aria-hidden />
      {pending ? "Exporting…" : "Export CSV"}
    </Button>
  );
}
