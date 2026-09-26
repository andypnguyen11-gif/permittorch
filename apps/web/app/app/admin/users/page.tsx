import type { Metadata } from "next";
import { ExternalLink, Users } from "lucide-react";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { EmptyState } from "@/components/app/leads/lead-table";

export const metadata: Metadata = { title: "Admin · Users" };

export default async function AdminUsersPage() {
  await requireSuperAdmin();
  return (
    <div className="max-w-2xl space-y-5">
      <h1 className="text-2xl font-bold tracking-tight">Users</h1>
      <EmptyState
        icon={Users}
        title="Managed in the Firebase console"
        description="User accounts, sign-in methods, and sessions are administered in Firebase. An in-app user admin ships after the MVP."
        action={
          <a href="https://console.firebase.google.com" target="_blank" rel="noopener noreferrer"
            className="inline-flex items-center gap-1 font-medium text-orange-600 hover:underline">
            Open Firebase console <ExternalLink className="size-3.5" aria-hidden />
          </a>
        }
      />
    </div>
  );
}
