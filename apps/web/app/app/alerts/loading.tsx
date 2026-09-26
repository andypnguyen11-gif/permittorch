import { Skeleton } from "@/components/ui/skeleton";

export default function AlertsLoading() {
  return (
    <div className="max-w-2xl space-y-6" aria-busy="true" aria-label="Loading alert settings">
      <div className="space-y-2">
        <Skeleton className="h-8 w-28" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </div>
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="flex items-start gap-4 rounded-xl border border-border bg-white p-4">
            <Skeleton className="size-9 rounded-lg" />
            <div className="flex-1 space-y-1.5">
              <Skeleton className="h-4 w-20" />
              <Skeleton className="h-3 w-72 max-w-full" />
            </div>
            <Skeleton className="mt-1 size-4 rounded-full" />
          </div>
        ))}
      </div>
      <Skeleton className="h-40 rounded-xl" />
    </div>
  );
}
