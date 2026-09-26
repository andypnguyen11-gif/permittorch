import type { FireCategory } from "@permittorch/types";
import {
  BellRing, ClipboardCheck, CookingPot, Droplets, Flame, ShieldCheck, TriangleAlert,
  type LucideIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

export const CATEGORY_LABELS: Record<FireCategory, string> = {
  FIRE_SPRINKLER: "Fire Sprinkler",
  FIRE_ALARM: "Fire Alarm",
  FIRE_SUPPRESSION: "Fire Suppression",
  KITCHEN_SUPPRESSION: "Kitchen Suppression",
  FIRE_INSPECTION: "Fire Inspection",
  VIOLATION_CORRECTION: "Violation / Correction",
  GENERAL_FIRE_PROTECTION: "General Fire Protection",
};

export const CATEGORY_ICONS: Record<FireCategory, LucideIcon> = {
  FIRE_SPRINKLER: Droplets,
  FIRE_ALARM: BellRing,
  FIRE_SUPPRESSION: ShieldCheck,
  KITCHEN_SUPPRESSION: CookingPot,
  FIRE_INSPECTION: ClipboardCheck,
  VIOLATION_CORRECTION: TriangleAlert,
  GENERAL_FIRE_PROTECTION: Flame,
};

/** Square icon tile used at the start of lead rows (mockup style). */
export function CategoryIcon({ category, className }: { category: FireCategory; className?: string }) {
  const Icon = CATEGORY_ICONS[category];
  return (
    <span
      className={cn(
        "flex size-9 shrink-0 items-center justify-center rounded-lg bg-orange-50 text-orange-600 ring-1 ring-inset ring-orange-100",
        className,
      )}
      title={CATEGORY_LABELS[category]}
    >
      <Icon className="size-4" aria-hidden />
    </span>
  );
}

export function CategoryChip({ category }: { category: FireCategory }) {
  return (
    <Badge variant="outline" className="border-orange-200 bg-orange-50 text-orange-700">
      {CATEGORY_LABELS[category]}
    </Badge>
  );
}
