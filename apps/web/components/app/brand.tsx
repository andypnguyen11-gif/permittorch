import { Flame } from "lucide-react";
import { cn } from "@/lib/utils";

export function BrandMark({ className }: { className?: string }) {
  return (
    <span className={cn("flex items-center gap-2", className)}>
      <span className="flex size-8 items-center justify-center rounded-lg bg-orange-500 shadow-sm shadow-orange-500/30">
        <Flame className="size-4.5 text-white" aria-hidden />
      </span>
      <span className="text-lg font-bold tracking-tight">
        Permit<span className="text-orange-500">Torch</span>
      </span>
    </span>
  );
}
