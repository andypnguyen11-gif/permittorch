"use client";
import { useEffect } from "react";
import "./globals.css";

// Last-resort boundary: replaces the root layout when it (or anything above
// app/app/error.tsx) fails, so it renders its own <html> and <body>.
export default function GlobalError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <html lang="en">
      <body className="flex min-h-dvh flex-col items-center justify-center gap-6 bg-stone-50 p-4 text-center text-stone-900 antialiased">
        <p className="text-lg font-bold tracking-tight">
          Permit<span className="text-orange-500">Torch</span>
        </p>
        <div role="alert" className="w-full max-w-md rounded-xl border border-stone-200 bg-white px-6 py-12">
          <p className="font-semibold">Something went wrong</p>
          <p className="mt-1 text-sm text-stone-500">
            PermitTorch hit an unexpected error. Nothing you saved was lost.
          </p>
          <button
            type="button"
            onClick={reset}
            className="mt-5 inline-flex h-9 items-center rounded-lg bg-orange-500 px-4 text-sm font-medium text-white outline-none hover:bg-orange-600 focus-visible:ring-3 focus-visible:ring-orange-500/40"
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
