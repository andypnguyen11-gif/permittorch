import { Suspense } from "react";
import { unstable_rethrow } from "next/navigation";
import type { AccountMe, Market } from "@permittorch/types";
import { getAccountMarkets, getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { AnalyticsIdentity } from "@/components/app/analytics-identity";
import { BrandMark } from "@/components/app/brand";
import { isUnauthorized } from "@/components/app/session-digest";
import { SessionRecoveryCard, UnavailableCard } from "@/components/app/session-recovery";
import { Sidebar } from "@/components/app/sidebar";
import { TermsGate } from "@/components/app/terms-gate";
import { TopBar } from "@/components/app/top-bar";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

type Shell = { ok: true; me: AccountMe; markets: Market[] } | { ok: false; reason: "session" | "error" };

async function loadShell(): Promise<Shell> {
  try {
    const token = await getApiToken();
    // The market selector only offers markets the user is entitled to — the API
    // scopes leads by entitlement, so other markets would always come back empty.
    const [me, markets] = await Promise.all([getAccountMe(token), getAccountMarkets(token)]);
    return { ok: true, me, markets };
  } catch (err) {
    // Let Next.js control-flow errors (dynamic-rendering bailouts, redirects) through.
    unstable_rethrow(err);
    // A 401 must not redirect to /login: the middleware still sees a valid
    // session cookie and would send the browser straight back (redirect loop).
    return { ok: false, reason: isUnauthorized(err) ? "session" : "error" };
  }
}

function StateShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-8 bg-stone-50 p-4 text-stone-900">
      <BrandMark />
      <div className="w-full max-w-lg">{children}</div>
      <Toaster theme="light" richColors closeButton position="top-right" />
    </div>
  );
}

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const shell = await loadShell();

  if (!shell.ok) {
    return (
      <TooltipProvider>
        <StateShell>
          {shell.reason === "session" ? <SessionRecoveryCard /> : <UnavailableCard />}
        </StateShell>
      </TooltipProvider>
    );
  }

  const { me, markets } = shell;
  // No page is rendered before the user agrees: every one of them shows or leads to lead data.
  if (!me.termsAccepted) {
    return (
      <TooltipProvider>
        <StateShell><TermsGate /></StateShell>
      </TooltipProvider>
    );
  }

  return (
    <TooltipProvider>
      <AnalyticsIdentity userId={me.id} />
      <div className="flex h-dvh bg-stone-50 text-stone-900">
        <div className="hidden lg:flex">
          <Sidebar role={me.role} plan={me.plan} />
        </div>
        <div className="flex min-w-0 flex-1 flex-col">
          <Suspense fallback={<div className="h-16 shrink-0 border-b border-border bg-white" />}>
            <TopBar markets={markets} email={me.email} role={me.role} plan={me.plan} />
          </Suspense>
          <main id="main" className="flex-1 overflow-y-auto">
            <div className="mx-auto w-full max-w-7xl p-4 sm:p-6 lg:p-8">{children}</div>
          </main>
        </div>
        <Toaster theme="light" richColors closeButton position="top-right" />
      </div>
    </TooltipProvider>
  );
}
