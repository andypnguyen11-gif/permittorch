import type { Metadata } from "next";
import Link from "next/link";
import { MapPin, Sparkles } from "lucide-react";
import type { AccountMe, Market } from "@permittorch/types";
import { getAccountMarkets, getAccountMe, getAllMarketStats, getMarkets } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { BillingButtons, CheckoutPicker, type CheckoutMarket } from "@/components/app/account/billing-buttons";
import { CheckoutConfirming } from "@/components/app/account/checkout-confirming";
import { PLAN_LABELS, parsePlanTier } from "@/components/app/account/plan-selection";
import { orderMarkets } from "@/components/app/order-markets";
import { SectionCard } from "@/components/app/section-card";
import { Badge } from "@/components/ui/badge";

export const metadata: Metadata = { title: "Account" };

const ROLE_LABELS: Record<AccountMe["role"], string> = {
  MEMBER: "Member", ADMIN: "Admin", SUPER_ADMIN: "PermitTorch staff",
};

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 py-2.5 text-sm first:pt-0 last:pb-0">
      <dt className="text-stone-500">{label}</dt>
      <dd className="text-right font-medium text-stone-900">{children}</dd>
    </div>
  );
}

/**
 * Picker catalog: entitled markets first, then the public catalog; markets with leads come
 * before those without, which stay listed but disabled ("No leads yet") — the API refuses
 * checkout for a market without recent data anyway.
 */
async function pickerMarkets(entitled: Market[]): Promise<CheckoutMarket[]> {
  const [catalog, stats] = await Promise.all([getMarkets(), getAllMarketStats()])
    .catch((err) => handleApiError(err));
  const withData = new Set(
    stats.filter((s) => s.lastUpdatedAt !== null && s.totalLast30Days > 0).map((s) => s.slug));
  const ordered = orderMarkets(catalog, entitled).map((m) => ({ ...m, hasData: withData.has(m.slug) }));
  return [...ordered.filter((m) => m.hasData), ...ordered.filter((m) => !m.hasData)];
}

export default async function AccountPage({ searchParams }: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
} = {}) {
  const token = await getApiToken();
  const [me, markets] = await Promise.all([getAccountMe(token), getAccountMarkets(token)])
    .catch((err) => handleApiError(err));
  const params = await searchParams;
  // ?plan= arrives from /pricing → /signup; it only preselects the picker.
  const requestedPlan = parsePlanTier(params?.plan);
  const checkoutResult = params?.checkout === "success" || params?.checkout === "cancelled" ? params.checkout : null;
  // Back from a paid Checkout but the webhook has not attached the plan yet: confirm, never re-offer checkout.
  const confirming = checkoutResult === "success" && me.plan === null;
  // Any live Stripe subscription (even unpaid/paused/incomplete) is managed in the portal.
  const showPicker = !confirming && me.plan === null && !me.hasLiveSubscription;
  const checkoutMarkets = showPicker ? await pickerMarkets(markets) : [];

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Account</h1>

      <SectionCard title="Profile">
        <dl className="divide-y divide-border">
          <Row label="Email">{me.email}</Row>
          <Row label="Organization">{me.organizationName}</Row>
          <Row label="Role">{ROLE_LABELS[me.role]}</Row>
        </dl>
      </SectionCard>

      <SectionCard title="Plan & billing"
        action={
          <Badge className={me.plan ? "bg-orange-100 text-orange-700" : "bg-stone-100 text-stone-600"}>
            {me.plan ? `${PLAN_LABELS[me.plan]} plan` : "No active plan"}
          </Badge>
        }>
        <div className="space-y-5">
          <div>
            <p className="mb-2 text-sm font-medium text-stone-700">Markets on your plan</p>
            {markets.length === 0 ? (
              <p className="text-sm text-stone-500">No markets on this plan yet.</p>
            ) : (
              <ul className="flex flex-wrap gap-2">
                {markets.map((m) => (
                  <li key={m.slug}>
                    <Badge variant="outline" className="h-6 gap-1 px-2.5 text-stone-700">
                      <MapPin className="text-orange-500" aria-hidden />{m.name}
                    </Badge>
                  </li>
                ))}
              </ul>
            )}
          </div>
          {confirming ? (
            <CheckoutConfirming />
          ) : showPicker ? (
            <>
              {checkoutResult === "cancelled" && (
                <p role="status" className="rounded-lg border border-border bg-stone-50 px-3 py-2 text-sm text-stone-600">
                  Checkout was cancelled — you have not been charged. Pick a plan whenever you&apos;re ready.
                </p>
              )}
              <CheckoutPicker markets={checkoutMarkets} initialPlan={requestedPlan ?? "PRO"} />
            </>
          ) : (
            <BillingButtons plan={me.plan} />
          )}
        </div>
      </SectionCard>

      {me.plan !== "TERRITORY" && (
        <div className="flex gap-4 rounded-xl border border-orange-200 bg-gradient-to-br from-orange-50 to-white p-5">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-orange-500 text-white">
            <Sparkles className="size-5" aria-hidden />
          </span>
          <div className="text-sm">
            <p className="font-semibold text-stone-900">Unlock more winning opportunities</p>
            <p className="mt-1 text-stone-600">
              Territory covers up to 5 markets, multiple users, advanced filters, and daily alerts.{" "}
              <Link href="/pricing" className="font-medium text-orange-600 hover:underline">Compare plans</Link>
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
