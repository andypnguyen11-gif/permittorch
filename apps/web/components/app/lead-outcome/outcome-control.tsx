"use client";
import { useRef, useState } from "react";
import { Check, Tag } from "lucide-react";
import type { LeadOutcome } from "@permittorch/types";
import { clearLeadOutcome, setLeadOutcome } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { cn } from "@/lib/utils";
import { OUTCOME_LABELS, OUTCOMES } from "./outcome-labels";

/**
 * The signed-in user's own tag on a lead, as a label that opens a menu. Changes are optimistic and
 * sent one at a time in click order, so the last choice is the one the server keeps. A failure
 * reverts to the last value the server confirmed, unless a later choice is still on its way.
 * Sits above a table row's stretched link (z-[1], under the z-10 sticky header).
 */
export function OutcomeControl({ leadId, outcome, className }: {
  leadId: string; outcome: LeadOutcome | null; className?: string;
}) {
  const getToken = useApiToken();
  const [shown, setShown] = useState<LeadOutcome | null>(outcome);
  const confirmed = useRef<LeadOutcome | null>(outcome);
  const queue = useRef<Promise<void>>(Promise.resolve());
  const latest = useRef(0);

  const choose = (next: LeadOutcome | null) => {
    const request = ++latest.current;
    setShown(next);
    queue.current = queue.current.then(async () => {
      try {
        const token = await getToken();
        if (next === null) await clearLeadOutcome(leadId, token);
        else await setLeadOutcome(leadId, next, token);
        confirmed.current = next;
      } catch (err) {
        if (request === latest.current) {
          setShown(confirmed.current);
          reportMutationError(err, "Could not update the lead tag");
        }
      }
    });
  };

  return (
    <div className={cn("relative z-[1] inline-flex", className)} onClick={(e) => e.stopPropagation()}>
      <DropdownMenu>
        <DropdownMenuTrigger
          aria-label="Lead outcome"
          className={cn(
            "inline-flex h-6 items-center gap-1 rounded-full px-2 text-[11px] font-medium whitespace-nowrap outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
            shown === null && "border border-dashed border-stone-300 text-stone-500 hover:border-stone-400 hover:text-stone-700",
            shown === "WORTH_CONTACTING" && "bg-emerald-50 text-emerald-700 ring-1 ring-inset ring-emerald-200",
            shown !== null && shown !== "WORTH_CONTACTING" && "bg-stone-100 text-stone-600 ring-1 ring-inset ring-stone-200",
          )}
        >
          {shown === null ? <><Tag className="size-3" aria-hidden />Tag</> : OUTCOME_LABELS[shown]}
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start" className="w-44">
          {OUTCOMES.map((o) => (
            <DropdownMenuItem key={o} onClick={() => choose(shown === o ? null : o)}>
              <Check className={cn("size-3.5", shown === o ? "opacity-100" : "opacity-0")} aria-hidden />
              {OUTCOME_LABELS[o]}
            </DropdownMenuItem>
          ))}
          {shown !== null && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => choose(null)}>Clear</DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
