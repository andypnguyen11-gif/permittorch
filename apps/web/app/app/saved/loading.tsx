import { Skeleton } from "@/components/ui/skeleton";

export default function SavedLoading() {
  return (
    <div className="space-y-5" aria-busy="true" aria-label="Loading saved leads">
      <div className="space-y-2">
        <Skeleton className="h-8 w-44" />
        <Skeleton className="h-3 w-72" />
      </div>
      <Skeleton className="h-10 w-72" />
      <div className="divide-y divide-border rounded-xl border border-border bg-white">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 p-4">
            <Skeleton className="h-8 w-10 rounded-lg" />
            <div className="flex-1 space-y-1.5">
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="h-3 w-1/2" />
            </div>
            <Skeleton className="h-7 w-32" />
          </div>
        ))}
      </div>
    </div>
  );
}
