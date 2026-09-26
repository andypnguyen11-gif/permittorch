"use client";
import { useState } from "react";
import { Menu } from "lucide-react";
import type { AccountMe, PlanTier } from "@permittorch/types";
import { Sidebar } from "@/components/app/sidebar";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";

/** Below the lg breakpoint the sidebar collapses into this slide-over sheet. */
export function MobileNav({ role, plan }: { role: AccountMe["role"]; plan?: PlanTier | null }) {
  const [open, setOpen] = useState(false);
  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger
        aria-label="Open navigation"
        className="flex size-9 items-center justify-center rounded-lg text-stone-600 outline-none hover:bg-stone-100 focus-visible:ring-3 focus-visible:ring-ring/50 lg:hidden"
      >
        <Menu className="size-5" aria-hidden />
      </SheetTrigger>
      <SheetContent side="left" className="w-60 max-w-60 gap-0 p-0 sm:max-w-60">
        <SheetTitle className="sr-only">Navigation</SheetTitle>
        <Sidebar role={role} plan={plan} onNavigate={() => setOpen(false)} />
      </SheetContent>
    </Sheet>
  );
}
