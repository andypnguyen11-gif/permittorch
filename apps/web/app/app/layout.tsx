import { Suspense } from "react";
import { getAccountMarkets, getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { Sidebar } from "@/components/app/sidebar";
import { TopBar } from "@/components/app/top-bar";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const token = await getApiToken();
  // The market selector only offers markets the user is entitled to — the API
  // scopes leads by entitlement, so other markets would always come back empty.
  const [me, markets] = await Promise.all([getAccountMe(token), getAccountMarkets(token)]);

  return (
    <TooltipProvider>
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
