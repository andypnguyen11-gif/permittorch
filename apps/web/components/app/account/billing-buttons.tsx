"use client";
import { useState } from "react";
import { ArrowUpRight, CreditCard, Loader2 } from "lucide-react";
import { toast } from "sonner";
import type { PlanTier } from "@permittorch/types";
import { createBillingPortal, createCheckout } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";

const isMocked = () => process.env.NEXT_PUBLIC_API_MOCK === "1";

function BillingButton({ children, onClick, variant }: {
  children: React.ReactNode;
  onClick: () => Promise<void>;
  variant: "outline" | "default";
}) {
  const [busy, setBusy] = useState(false);
  const mocked = isMocked();
  const button = (
    <Button
      size="lg"
      variant={variant}
      disabled={mocked || busy}
      onClick={async () => {
        setBusy(true);
        try { await onClick(); } catch { toast.error("Billing is unavailable right now. Please try again."); }
        finally { setBusy(false); }
      }}
    >
      {busy && <Loader2 className="animate-spin" aria-hidden />}
      {children}
    </Button>
  );
  if (!mocked) return button;
  return (
    <Tooltip>
      {/* span wrapper: disabled buttons don't emit pointer events */}
      <TooltipTrigger render={<span tabIndex={0} className="rounded-lg outline-none focus-visible:ring-3 focus-visible:ring-ring/50" />}>
        {button}
      </TooltipTrigger>
      <TooltipContent>Billing is disabled in mock mode — available after API integration.</TooltipContent>
    </Tooltip>
  );
}

export function nextPlan(plan: PlanTier | null): PlanTier | null {
  if (plan === "TERRITORY") return null;
  return plan === "PRO" ? "TERRITORY" : "PRO";
}

const LABEL: Record<PlanTier, string> = { STARTER: "Starter", PRO: "Pro", TERRITORY: "Territory" };

export function BillingButtons({ plan }: { plan: PlanTier | null }) {
  const getToken = useApiToken();
  const go = (url: string) => { if (url && url !== "#") window.location.assign(url); };
  const upgrade = nextPlan(plan);

  return (
    <div className="flex flex-wrap gap-3">
      {plan !== null && (
        <BillingButton variant="outline"
          onClick={async () => go((await createBillingPortal(await getToken())).url)}>
          <CreditCard aria-hidden /> Manage billing
        </BillingButton>
      )}
      {upgrade && (
        <BillingButton variant="default"
          onClick={async () => go((await createCheckout(upgrade, await getToken())).url)}>
          <ArrowUpRight aria-hidden />
          {plan === null ? `Subscribe to ${LABEL[upgrade]}` : `Upgrade to ${LABEL[upgrade]}`}
        </BillingButton>
      )}
    </div>
  );
}
