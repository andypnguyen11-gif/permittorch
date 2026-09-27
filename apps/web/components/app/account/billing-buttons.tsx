"use client";
import { useState } from "react";
import { ArrowUpRight, CreditCard, Loader2 } from "lucide-react";
import type { Market, PlanTier } from "@permittorch/types";
import { toast } from "sonner";
import { ApiError, createBillingPortal, createCheckout } from "@/lib/api";
import { track } from "@/lib/analytics";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import {
  PLAN_LABELS, PLAN_TIERS, fitSelection, isValidSelection, marketLimit, toggleMarket,
} from "./plan-selection";

const isMocked = () => process.env.NEXT_PUBLIC_API_MOCK === "1";

function BillingButton({ children, onClick, variant, disabled = false }: {
  children: React.ReactNode;
  onClick: () => Promise<void>;
  variant: "outline" | "default";
  disabled?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const mocked = isMocked();
  const button = (
    <Button
      size="lg"
      variant={variant}
      disabled={mocked || busy || disabled}
      onClick={async () => {
        setBusy(true);
        try { await onClick(); } catch (err) { reportMutationError(err, "Billing is unavailable right now. Please try again."); }
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

const go = (url: string) => { if (url && url !== "#") window.location.assign(url); };

/**
 * Billing actions for an organization with a live Stripe subscription. A second
 * Checkout is refused by the API (409) while a subscription is live, so plan
 * changes go through the Stripe customer portal. `plan` is null when the
 * subscription exists but grants no plan (unpaid, paused, incomplete): the
 * portal is then the only way to fix payment, so no upgrade is offered.
 */
export function BillingButtons({ plan }: { plan: PlanTier | null }) {
  const getToken = useApiToken();
  const upgrade = plan === null ? null : nextPlan(plan);
  const openPortal = async () => go((await createBillingPortal(await getToken())).url);

  return (
    <div className="space-y-3">
      {plan === null && (
        <p className="text-sm text-stone-600">
          Your subscription needs attention. Update your payment details in the billing portal to restore access.
        </p>
      )}
      <div className="flex flex-wrap gap-3">
        <BillingButton variant="outline" onClick={openPortal}>
          <CreditCard aria-hidden /> Manage billing
        </BillingButton>
        {upgrade && (
          <BillingButton variant="default" onClick={openPortal}>
            <ArrowUpRight aria-hidden /> Upgrade to {PLAN_LABELS[upgrade]}
          </BillingButton>
        )}
      </div>
    </div>
  );
}

/** A picker entry: `hasData: false` marks a market with no leads yet (shown, but not purchasable). */
export type CheckoutMarket = Market & { hasData?: boolean };

/**
 * First subscription: choose a plan and the markets it covers, then open
 * Stripe Checkout. Starter/Pro cover exactly one market; Territory up to five.
 */
export function CheckoutPicker({ markets, initialPlan }: { markets: CheckoutMarket[]; initialPlan: PlanTier }) {
  const getToken = useApiToken();
  const [plan, setPlan] = useState<PlanTier>(initialPlan);
  const [selected, setSelected] = useState<string[]>([]);
  const limit = marketLimit(plan);
  const valid = isValidSelection(plan, selected);

  const choosePlan = (next: PlanTier) => {
    setPlan(next);
    setSelected((current) => fitSelection(next, current));
  };

  return (
    <div className="space-y-4">
      <fieldset>
        <legend className="mb-2 text-sm font-medium text-stone-700">Plan</legend>
        <div className="flex flex-wrap gap-2">
          {PLAN_TIERS.map((tier) => (
            <label key={tier}
              className={cn(
                "flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-1.5 text-sm has-[:focus-visible]:ring-3 has-[:focus-visible]:ring-ring/50",
                plan === tier ? "border-orange-400 bg-orange-50 text-orange-700" : "border-border text-stone-700 hover:border-stone-300",
              )}>
              <input type="radio" name="checkout-plan" value={tier} checked={plan === tier}
                onChange={() => choosePlan(tier)} className="sr-only" />
              {PLAN_LABELS[tier]}
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset>
        <legend className="mb-1 text-sm font-medium text-stone-700">Markets</legend>
        <p className="mb-2 text-xs text-stone-500">
          {limit === 1
            ? `${PLAN_LABELS[plan]} covers one market.`
            : `Territory covers up to ${limit} markets (${selected.length} selected).`}
        </p>
        {markets.length === 0 ? (
          <p className="text-sm text-stone-500">No markets are available yet.</p>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2">
            {markets.map((m) => {
              const checked = selected.includes(m.slug);
              const noData = m.hasData === false;
              const full = limit > 1 && !checked && selected.length >= limit;
              const disabled = noData || full;
              return (
                <li key={m.slug}>
                  <label className={cn(
                    "flex items-center gap-2 rounded-lg border px-3 py-2 text-sm",
                    disabled ? "cursor-not-allowed opacity-50" : "cursor-pointer hover:border-stone-300",
                    checked ? "border-orange-400 bg-orange-50/60" : "border-border",
                  )}>
                    <input type={limit === 1 ? "radio" : "checkbox"} name="checkout-market" value={m.slug}
                      checked={checked} disabled={disabled}
                      aria-describedby={noData ? `no-data-${m.slug}` : undefined}
                      onChange={() => setSelected((current) => toggleMarket(plan, current, m.slug))}
                      className="accent-orange-500" />
                    {m.name}
                    {noData && (
                      <span id={`no-data-${m.slug}`} className="ml-auto text-xs text-stone-500">No leads yet</span>
                    )}
                  </label>
                </li>
              );
            })}
          </ul>
        )}
      </fieldset>

      <BillingButton variant="default" disabled={!valid}
        onClick={async () => {
          let url: string;
          try {
            ({ url } = await createCheckout(plan, selected, await getToken()));
          } catch (err) {
            if (err instanceof ApiError && err.status === 409) {
              // A subscription (or a checkout in flight) already exists: never a second checkout.
              toast("You already have a subscription — opening the billing portal.");
              go((await createBillingPortal(await getToken())).url);
              return;
            }
            if (err instanceof ApiError && err.status === 400) {
              toast.error(err.message);
              return;
            }
            throw err;
          }
          track("checkout_started", { plan });
          go(url);
        }}>
        <ArrowUpRight aria-hidden /> Subscribe to {PLAN_LABELS[plan]}
      </BillingButton>
    </div>
  );
}
