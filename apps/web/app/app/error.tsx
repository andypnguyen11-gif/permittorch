"use client";
import { useEffect } from "react";
import { TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function AppError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div role="alert" className="flex flex-col items-center gap-3 rounded-xl border border-border bg-white px-6 py-16 text-center">
      <span className="flex size-11 items-center justify-center rounded-full bg-red-50">
        <TriangleAlert className="size-5 text-red-500" aria-hidden />
      </span>
      <div>
        <p className="font-semibold text-stone-900">Something went wrong loading this page</p>
        <p className="mt-1 text-sm text-stone-500">
          The data service may be temporarily unavailable. Nothing you saved was lost.
        </p>
      </div>
      <Button onClick={reset} className="mt-2">Try again</Button>
    </div>
  );
}
