import type { Metadata } from "next";
import { CreditCard, ExternalLink } from "lucide-react";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { EmptyState } from "@/components/app/leads/lead-table";

export const metadata: Metadata = { title: "Admin · Subscriptions" };

export default async function AdminSubscriptionsPage() {
  await requireSuperAdmin();
  return (
    <div className="max-w-2xl space-y-5">
      <h1 className="text-2xl font-bold tracking-tight">Subscriptions</h1>
      <EmptyState
        icon={CreditCard}
        title="Managed in Stripe for the MVP"
        description="Plans, trials, refunds, and dunning are administered in the Stripe dashboard. Entitlements sync automatically through Stripe webhooks."
        action={
          <a href="https://dashboard.stripe.com/subscriptions" target="_blank" rel="noopener noreferrer"
            className="inline-flex items-center gap-1 font-medium text-orange-600 hover:underline">
            Open Stripe dashboard <ExternalLink className="size-3.5" aria-hidden />
          </a>
        }
      />
    </div>
  );
}
