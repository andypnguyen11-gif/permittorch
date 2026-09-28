import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft, ExternalLink, MapPin } from "lucide-react";
import { getLead, getSavedLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { ScoreBadge } from "@/components/app/score-badge";
import { CategoryChip } from "@/components/app/category-chip";
import { SectionCard } from "@/components/app/section-card";
import { SignalList } from "@/components/app/lead-detail/signal-list";
import { SaveButton } from "@/components/app/lead-detail/save-button";
import { TrackOnMount } from "@/components/app/track-on-mount";
import { STATUS_LABELS } from "@/components/app/leads/query";
import {
  formatDate, formatRelative, formatValueShort,
  humanizeMachineString, permitStatusDisplay,
} from "@/components/app/format";
import { Badge } from "@/components/ui/badge";

export const metadata: Metadata = { title: "Lead" };

function Field({ label, value }: { label: string; value: string | number | null }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-[11px] font-medium tracking-wider text-stone-400 uppercase">{label}</dt>
      <dd className="text-sm text-stone-800">{value ?? "—"}</dd>
    </div>
  );
}

// Participant roles arrive from the API as UPPER_SNAKE enum values
// (JsonNamingPolicy.SnakeCaseUpper): OWNER, APPLICANT, CONTRACTOR, GENERAL_CONTRACTOR.
// An unrecognized role falls back to a humanized version of the machine value,
// never the raw enum string.
const ROLE_LABELS: Record<string, string> = {
  OWNER: "Owner", APPLICANT: "Applicant", CONTRACTOR: "Contractor", GENERAL_CONTRACTOR: "General contractor",
};
const roleLabel = (role: string): string => ROLE_LABELS[role] ?? humanizeMachineString(role) ?? role;

// A record whose recordType is "inspection" or "violation" is not a permit;
// the Permit card retitles itself and relabels the permit-number field to match.
function recordCardTitle(recordType: string | null): string {
  if (recordType === "inspection") return "Inspection";
  if (recordType === "violation") return "Violation";
  return "Permit";
}
function recordNumberLabel(recordType: string | null): string {
  return recordType === "inspection" || recordType === "violation" ? "Record number" : "Permit number";
}

export default async function LeadDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = await getApiToken();
  const [lead, savedLeads] = await Promise.all([getLead(id, token), getSavedLeads(token)])
    .catch((err) => handleApiError(err, { notFoundOn404: true }));
  const savedItem = savedLeads.find((s) => s.lead.id === lead.id) ?? null;

  const cardTitle = recordCardTitle(lead.permit.recordType);
  const numberLabel = recordNumberLabel(lead.permit.recordType);
  const recordTypeLabel = humanizeMachineString(lead.permit.recordType);
  const workTypeLabel = humanizeMachineString(lead.permit.workType);
  const propertyTypeLabel = humanizeMachineString(lead.permit.propertyType);

  return (
    <div className="space-y-6">
      <TrackOnMount event="lead_opened" props={{ leadId: lead.id, score: lead.score, category: lead.category }} />
      <Link href="/app/leads"
        className="inline-flex items-center gap-1.5 rounded text-sm text-stone-500 outline-none hover:text-stone-900 focus-visible:ring-3 focus-visible:ring-ring/50">
        <ArrowLeft className="size-4" aria-hidden /> All leads
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4 rounded-xl border border-border bg-white p-5 shadow-xs">
        <div className="flex min-w-0 items-start gap-4">
          <ScoreBadge score={lead.score} showLabel />
          <div className="min-w-0 space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold tracking-tight">{lead.title}</h1>
              {lead.isNew && <Badge className="bg-orange-100 text-orange-700">New</Badge>}
            </div>
            <p className="flex items-center gap-1.5 text-sm text-stone-500">
              <MapPin className="size-4 shrink-0 text-stone-400" aria-hidden />
              {lead.address ?? "Address unavailable"} · {lead.city}, {lead.state}
              {lead.permit.zip ? ` ${lead.permit.zip}` : ""}
            </p>
            <CategoryChip category={lead.category} />
          </div>
        </div>
        <SaveButton leadId={lead.id} savedId={savedItem?.id ?? null} />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <SectionCard title="Opportunity">
            <p className="mb-4 rounded-lg border border-orange-100 bg-orange-50 p-3 text-sm text-orange-900">
              {lead.reason}
            </p>
            <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
              <Field label="Lead score" value={lead.score} />
              <Field label="Classification confidence" value={`${Math.round(lead.confidence * 100)}%`} />
              <Field label="Permit status"
                value={permitStatusDisplay(STATUS_LABELS[lead.status], lead.permit.rawStatus)} />
              <Field label="First discovered" value={formatRelative(lead.firstDetectedAt)} />
              <Field label="Last updated" value={formatRelative(lead.lastUpdatedAt)} />
            </dl>
          </SectionCard>

          <SectionCard title={cardTitle}>
            <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
              <Field label={numberLabel} value={lead.permit.permitNumber} />
              <Field label="Permit type" value={lead.permitType} />
              <Field label="Filed" value={formatDate(lead.filedDate)} />
              <Field label="Issued" value={formatDate(lead.permit.issuedDate)} />
              <Field label="Estimated value" value={formatValueShort(lead.estimatedValue)} />
              <Field label="Square footage"
                value={lead.permit.squareFootage != null
                  ? `${lead.permit.squareFootage.toLocaleString("en-US")} sq ft` : null} />
              <Field label="Owner" value={lead.permit.ownerName} />
              <Field label="Contractor" value={lead.permit.contractorName} />
              <Field label="Zip" value={lead.permit.zip} />
              {recordTypeLabel && <Field label="Record type" value={recordTypeLabel} />}
              {workTypeLabel && <Field label="Work type" value={workTypeLabel} />}
              {lead.permit.expirationDate && <Field label="Expires" value={formatDate(lead.permit.expirationDate)} />}
              {lead.permit.inspectionDate &&
                <Field label="Inspection date" value={formatDate(lead.permit.inspectionDate)} />}
              {lead.permit.businessName && <Field label="Business" value={lead.permit.businessName} />}
              {propertyTypeLabel && <Field label="Property type" value={propertyTypeLabel} />}
            </dl>
            {lead.permit.description && (
              <div className="mt-4 border-t border-border pt-4">
                <p className="text-[11px] font-medium tracking-wider text-stone-400 uppercase">Description</p>
                <p className="mt-1 text-sm leading-relaxed text-stone-700">{lead.permit.description}</p>
              </div>
            )}
          </SectionCard>

          <SectionCard title="Participants">
            {lead.participants.length === 0 ? (
              <p className="text-sm text-stone-500">No participants listed on this permit.</p>
            ) : (
              <ul className="divide-y divide-border">
                {lead.participants.map((p) => (
                  <li key={p.role + p.name} className="flex justify-between gap-4 py-2 text-sm first:pt-0 last:pb-0">
                    <span className="text-stone-500">{roleLabel(p.role)}</span>
                    <span className="text-right font-medium text-stone-800">{p.name}</span>
                  </li>
                ))}
              </ul>
            )}
          </SectionCard>
        </div>

        <div className="space-y-6">
          <SignalList score={lead.score} signals={lead.signals} />
          <SectionCard title="Source">
            <div className="space-y-2 text-sm">
              <p className="font-medium text-stone-800">{lead.source.name}</p>
              <a href={lead.source.url} target="_blank" rel="noopener noreferrer"
                className="inline-flex items-center gap-1 rounded font-medium text-orange-600 outline-none hover:underline focus-visible:ring-3 focus-visible:ring-ring/50">
                View original record <ExternalLink className="size-3.5" aria-hidden />
              </a>
              <p className="text-xs text-stone-500">
                Last checked {formatRelative(lead.source.lastCheckedAt)}
              </p>
            </div>
          </SectionCard>
        </div>
      </div>
    </div>
  );
}
