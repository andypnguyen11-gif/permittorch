import Link from "next/link";
import { SearchX } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

export default function AppNotFound() {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-border bg-white px-6 py-16 text-center">
      <span className="flex size-11 items-center justify-center rounded-full bg-stone-100">
        <SearchX className="size-5 text-stone-400" aria-hidden />
      </span>
      <div>
        <p className="font-semibold text-stone-900">We couldn’t find that</p>
        <p className="mt-1 text-sm text-stone-500">
          This lead may be outside your markets, or it no longer exists.
        </p>
      </div>
      <Link href="/app/leads" className={buttonVariants({ className: "mt-2" })}>Back to leads</Link>
    </div>
  );
}
