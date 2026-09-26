import type { Metadata } from "next";
import { Check } from "lucide-react";
import { getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { DigestForm } from "@/components/app/alerts/digest-form";
import { SectionCard } from "@/components/app/section-card";

export const metadata: Metadata = { title: "Alerts" };

const CONTENTS = [
  "New fire-protection opportunities in your markets since the last digest",
  "Hot leads (score 90+) called out first, with score and project value",
  "A one-line “why this matters” for each lead",
  "A link back to the full lead list",
];

export default async function AlertsPage() {
  const me = await getAccountMe(await getApiToken()).catch((err) => handleApiError(err));
  return (
    <div className="max-w-2xl space-y-6">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Alerts</h1>
        <p className="text-sm text-stone-500">
          Choose how often PermitTorch emails <span className="font-medium text-stone-700">{me.email}</span> new opportunities.
        </p>
      </div>
      <DigestForm initialFrequency={me.digestFrequency} />
      <SectionCard title="What’s in the digest">
        <ul className="space-y-2 text-sm text-stone-600">
          {CONTENTS.map((c) => (
            <li key={c} className="flex gap-2"><Check className="mt-0.5 size-4 shrink-0 text-orange-500" aria-hidden />{c}</li>
          ))}
        </ul>
        <p className="mt-4 text-xs text-stone-400">Email is the only notification channel for now.</p>
      </SectionCard>
    </div>
  );
}
