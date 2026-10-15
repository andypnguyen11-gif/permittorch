"use client";
import { useState } from "react";
import { Download } from "lucide-react";
import { toast } from "sonner";
import { exportPreSprinklerCsv, type PreSprinklerQuery } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { Button } from "@/components/ui/button";

const FILE_NAME = "permittorch-pre-sprinkler-leads.csv";

/** Downloads the leads the filters on screen match. Like the list, the API serves OPEN leads
 * unless another standing is chosen. */
export function PreSprinklerExportButton({ query }: { query: PreSprinklerQuery }) {
  const getToken = useApiToken();
  const [pending, setPending] = useState(false);

  const download = async () => {
    setPending(true);
    try {
      const { blob, truncated } = await exportPreSprinklerCsv(query, await getToken());
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = FILE_NAME;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      if (truncated) toast("Export holds the first 5,000 leads. Narrow the filters to export the rest.");
      else toast.success("Export downloaded");
    } catch (err) {
      reportMutationError(err, "Could not export leads");
    } finally {
      setPending(false);
    }
  };

  return (
    <Button variant="outline" onClick={download} disabled={pending} className="h-9 px-3">
      <Download aria-hidden />
      {pending ? "Exporting…" : "Download CSV"}
    </Button>
  );
}
