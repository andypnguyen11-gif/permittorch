import type { Metadata } from "next";
import { getSavedLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { SavedList } from "@/components/app/saved/saved-list";

export const metadata: Metadata = { title: "Saved leads" };

export default async function SavedPage() {
  const items = await getSavedLeads(await getApiToken()).catch((err) => handleApiError(err));
  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Saved leads</h1>
        <p className="text-sm text-stone-500">Simple tracking — saved or contacted. No CRM here by design.</p>
      </div>
      <SavedList initialItems={items} />
    </div>
  );
}
