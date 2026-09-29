import type { Metadata } from "next";
import { getMarkets, getRemovals } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { RemovalForm } from "@/components/app/admin/removal-form";
import { RemovalTable } from "@/components/app/admin/removal-table";

export const metadata: Metadata = { title: "Admin · Removals" };

export default async function AdminRemovalsPage() {
  const token = await requireSuperAdmin();
  const [removals, markets] = await Promise.all([getRemovals(token), getMarkets()])
    .catch((err) => handleApiError(err));
  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Removals</h1>
        <p className="text-sm text-stone-500">
          What people asked to have removed. A removed value is cleared from what is stored and kept out of every later import.
        </p>
      </div>
      <RemovalForm markets={markets} />
      <RemovalTable removals={removals.items} />
    </div>
  );
}
