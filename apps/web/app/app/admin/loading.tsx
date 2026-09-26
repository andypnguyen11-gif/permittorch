import { Skeleton } from "@/components/ui/skeleton";

// Shared by every /app/admin/* page: a heading and a data table.
export default function AdminLoading() {
  return (
    <div className="space-y-5" aria-busy="true" aria-label="Loading admin data">
      <div className="space-y-2">
        <Skeleton className="h-8 w-44" />
        <Skeleton className="h-4 w-72 max-w-full" />
      </div>
      <div className="overflow-hidden rounded-xl border border-border bg-white">
        <div className="flex gap-6 border-b border-border bg-stone-50 px-4 py-3">
          {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-3 w-20" />)}
        </div>
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="flex items-center gap-6 border-b border-border px-4 py-3 last:border-0">
            <div className="w-48 space-y-1.5">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-24" />
            </div>
            <Skeleton className="h-4 w-16" />
            <Skeleton className="h-4 w-24" />
            <Skeleton className="ml-auto h-4 w-12" />
            <Skeleton className="h-8 w-16" />
          </div>
        ))}
      </div>
    </div>
  );
}
