import { Skeleton } from "@/components/ui/skeleton";

export default function LeadsLoading() {
  return (
    <div className="space-y-5" aria-busy="true" aria-label="Loading leads">
      <div className="space-y-2">
        <Skeleton className="h-8 w-32" />
        <Skeleton className="h-3 w-40" />
      </div>
      <div className="flex flex-wrap gap-2">
        {["w-56", "w-32", "w-44", "w-40"].map((w) => (
          <Skeleton key={w} className={`h-9 ${w}`} />
        ))}
      </div>
      <div className="overflow-hidden rounded-xl border border-border bg-white">
        <div className="h-10 border-b border-border bg-stone-50" />
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 border-b border-border px-4 py-3 last:border-0">
            <Skeleton className="size-9 rounded-lg" />
            <div className="flex-1 space-y-1.5">
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="h-3 w-1/5" />
            </div>
            <Skeleton className="h-8 w-10 rounded-lg" />
            <Skeleton className="hidden h-4 w-32 md:block" />
            <Skeleton className="hidden h-4 w-20 md:block" />
            <Skeleton className="hidden h-4 w-48 lg:block" />
          </div>
        ))}
      </div>
    </div>
  );
}
