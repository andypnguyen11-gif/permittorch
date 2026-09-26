import Link from "next/link";
import { BrandMark } from "@/components/app/brand";

/** Centered card layout shared by /login and /signup. */
export function AuthShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative flex min-h-dvh flex-col items-center justify-center overflow-hidden bg-stone-50 px-4 py-12">
      <div
        aria-hidden
        className="pointer-events-none absolute inset-x-0 top-0 h-80 bg-[radial-gradient(60%_100%_at_50%_0%,rgb(249_115_22/0.12),transparent)]"
      />
      <Link href="/" aria-label="PermitTorch home" className="relative mb-8 rounded-lg outline-none focus-visible:ring-3 focus-visible:ring-ring/50">
        <BrandMark />
      </Link>
      <main className="relative w-full max-w-sm">{children}</main>
      <p className="relative mt-8 text-center text-xs text-stone-400">
        Scored fire-protection permit leads, updated daily.
      </p>
    </div>
  );
}
