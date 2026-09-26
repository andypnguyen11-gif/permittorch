import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, Lock, MapPin } from "lucide-react";
import { getAccountMarkets, getLeads, getMarkets } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { orderMarkets } from "@/components/app/order-markets";
import { EmptyState } from "@/components/app/leads/lead-table";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Markets" };

export default async function MarketsPage() {
  const token = await getApiToken();
  const [allMarkets, entitled] = await Promise.all([getMarkets(), getAccountMarkets(token)])
    .catch((err) => handleApiError(err));
  const entitledSlugs = new Set(entitled.map((m) => m.slug));
  const markets = orderMarkets(allMarkets, entitled);

  // Per-market lead count via the leads endpoint (total only, so pageSize 1).
  const counts = new Map<string, number>(
    await Promise.all(
      entitled.map(async (m): Promise<[string, number]> => {
        const res = await getLeads({ market: m.slug, pageSize: 1 }, token);
        return [m.slug, res.total];
      }),
    ).catch((err) => handleApiError(err)),
  );

  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Markets</h1>
        <p className="text-sm text-stone-500">Markets on your plan, plus others you can unlock.</p>
      </div>
      {markets.length === 0 ? (
        <EmptyState icon={MapPin} title="No markets available yet"
          description="Markets appear here as soon as we have live permit data for them." />
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {markets.map((market) => {
            const isEntitled = entitledSlugs.has(market.slug);
            return (
              <li key={market.slug}
                className={cn("flex flex-col rounded-xl border p-5 shadow-xs",
                  isEntitled ? "border-border bg-white" : "border-dashed border-stone-300 bg-stone-50/70")}>
                <div className="flex items-start justify-between gap-3">
                  <h2 className="flex items-center gap-2 font-semibold text-stone-900">
                    <MapPin className={cn("size-4", isEntitled ? "text-orange-500" : "text-stone-400")} aria-hidden />
                    {market.name}
                  </h2>
                  {isEntitled ? (
                    <Badge className="bg-green-100 text-green-700">Included</Badge>
                  ) : (
                    <Badge variant="outline" className="text-stone-500"><Lock aria-hidden /> Locked</Badge>
                  )}
                </div>
                {isEntitled ? (
                  <>
                    <p className="mt-4 text-3xl font-bold tracking-tight tabular-nums">
                      {counts.get(market.slug) ?? 0}
                      <span className="ml-1.5 text-sm font-normal text-stone-500">
                        {counts.get(market.slug) === 1 ? "Lead" : "Leads"}
                      </span>
                    </p>
                    <Link href={`/app/leads?market=${encodeURIComponent(market.slug)}`}
                      className={buttonVariants({ variant: "outline", className: "mt-4 self-start" })}>
                      View leads <ArrowRight aria-hidden />
                    </Link>
                  </>
                ) : (
                  <>
                    <p className="mt-4 text-sm text-stone-500">
                      Upgrade your plan to see fire-protection leads in {market.city}.
                    </p>
                    <Link href="/app/account" className={buttonVariants({ className: "mt-4 self-start" })}>
                      Upgrade to unlock
                    </Link>
                  </>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
