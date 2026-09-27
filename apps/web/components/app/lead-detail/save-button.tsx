"use client";
import { useState } from "react";
import { Bookmark, BookmarkCheck } from "lucide-react";
import { toast } from "sonner";
import { saveLead, unsaveLead } from "@/lib/api";
import { track } from "@/lib/analytics";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { Button } from "@/components/ui/button";

const OPTIMISTIC = "optimistic";

export function SaveButton({ leadId, savedId }: { leadId: string; savedId: string | null }) {
  const getToken = useApiToken();
  const [currentSavedId, setCurrentSavedId] = useState<string | null>(savedId);
  const [pending, setPending] = useState(false);
  const saved = currentSavedId !== null;

  // Plain state (not startTransition): updates inside an async transition are
  // held back until it settles, which would defeat the optimistic flip.
  const toggle = async () => {
    const previous = currentSavedId;
    // Optimistic flip first; reconcile with the API result after.
    setCurrentSavedId(previous !== null ? null : OPTIMISTIC);
    setPending(true);
    try {
      if (previous !== null) {
        await unsaveLead(previous, await getToken());
        toast("Lead removed from saved");
      } else {
        const item = await saveLead(leadId, await getToken());
        setCurrentSavedId(item.id);
        track("lead_saved", { leadId });
        toast.success("Lead saved");
      }
    } catch (err) {
      setCurrentSavedId(previous);
      reportMutationError(err, "Could not update saved leads");
    } finally {
      setPending(false);
    }
  };

  return (
    <Button
      size="lg"
      onClick={toggle}
      disabled={pending}
      aria-pressed={saved}
      variant={saved ? "outline" : "default"}
      className={saved ? "h-9 border-orange-300 px-3 text-orange-600 hover:bg-orange-50 hover:text-orange-700" : "h-9 px-3"}
    >
      {saved ? <BookmarkCheck aria-hidden /> : <Bookmark aria-hidden />}
      {saved ? "Saved" : "Save lead"}
    </Button>
  );
}
