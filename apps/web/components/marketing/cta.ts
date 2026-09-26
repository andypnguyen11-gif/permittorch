import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * Brand colour rules for marketing pages (WCAG AA on white):
 * - Primary CTAs, score badges, white-on-orange chips: orange-700 background.
 * - Orange text under 24px: text-orange-700 (ORANGE_TEXT).
 * - Bright orange-500 is for non-text accents only (icons, borders, decoration): ICON_ACCENT.
 * A unit test scans marketing sources for the low-contrast combinations.
 */
export const ORANGE_TEXT = "text-orange-700";
export const ICON_ACCENT = "text-orange-500";
export const BADGE_CLASSES = "bg-orange-700 text-white";

const PRIMARY =
  "bg-orange-700 text-white hover:bg-orange-800 focus-visible:ring-2 focus-visible:ring-orange-700 focus-visible:ring-offset-2";

export type CtaSize = "default" | "lg" | "hero";

/** Classes for a primary call to action (use on <Link> or <Button>). */
export function ctaClasses(size: CtaSize = "default", className?: string): string {
  return cn(
    buttonVariants({ size: size === "default" ? "default" : "lg" }),
    PRIMARY,
    size === "lg" && "px-8",
    size === "hero" && "h-11 px-6 text-base font-semibold",
    className,
  );
}
