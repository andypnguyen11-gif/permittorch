import type { ContractorStatus } from "@permittorch/types";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

// Says what the record shows and nothing more: a fire-protection contractor is
// named on this permit. Never "awarded", "won", or "unassigned". The other
// statuses and null (not yet assessed) carry no badge.
export const FIRE_CONTRACTOR_BADGE_TEXT = "Fire contractor on permit";

export function ContractorStatusBadge({ status, className }: {
  status: ContractorStatus | null; className?: string;
}) {
  if (status !== "FIRE_CONTRACTOR_NAMED") return null;
  return (
    <Badge className={cn("bg-stone-100 text-stone-600 ring-1 ring-inset ring-stone-200", className)}>
      {FIRE_CONTRACTOR_BADGE_TEXT}
    </Badge>
  );
}
