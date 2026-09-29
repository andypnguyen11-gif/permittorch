// FIXTURE CONTRACT: lib/api.ts (frozen) calls these by name in mock mode.
// Signatures mirror lib/api.ts with token parameters dropped. Do NOT add,
// remove, or rename these functions. Bodies are thin adapters over the
// mock* fixture internals below.
// NOTE: getMarkets/getMarketStats are intentionally ABSENT — lib/api.ts's
// mock branch imports mockMarkets/mockMarketStats from ./markets (WS3-owned).
import type {
  AccountMe, AdminSource, DigestFrequency, LeadDetail, LeadsResponse,
  Market, Paged, PlanTier, SavedLeadItem, SavedLeadStatus, ScraperRunSummary,
} from "@permittorch/types";
import { ApiError, type LeadsExport, type LeadsQuery } from "@/lib/api";
import { mockLeads, mockLeadsResponse, mockLeadDetail } from "./leads";
import { mockSavedLeads } from "./saved";
import { mockAccountForRole, mockAccountMarkets } from "./account";
import { mockAdminSources, mockAdminRuns } from "./admin";

// In-memory saved-leads state so optimistic UI flows work in mock dev.
const savedState: SavedLeadItem[] = [...mockSavedLeads];

export async function getLeads(params: LeadsQuery): Promise<LeadsResponse> { return mockLeadsResponse(params); }
export async function getLead(id: string): Promise<LeadDetail> { return mockLeadDetail(id); }
// A stand-in for the API's file: the matching fixture leads, all pages, in three columns.
export async function exportLeadsCsv(params: LeadsQuery): Promise<LeadsExport> {
  const { items } = mockLeadsResponse({ ...params, page: 1, pageSize: mockLeads.length });
  const quote = (value: string) => `"${value.replace(/"/g, '""')}"`;
  const lines = ["Score,Address,City", ...items.map((l) => `${l.score},${quote(l.address ?? "")},${quote(l.city)}`)];
  return { blob: new Blob([lines.join("\r\n") + "\r\n"], { type: "text/csv" }), truncated: false };
}
export async function getSavedLeads(): Promise<SavedLeadItem[]> { return [...savedState]; }
export async function saveLead(fireOpportunityId: string): Promise<SavedLeadItem> {
  const lead = mockLeads.find((l) => l.id === fireOpportunityId);
  if (!lead) throw new ApiError("Lead not found", 404);
  const existing = savedState.find((s) => s.lead.id === fireOpportunityId);
  if (existing) return existing;
  const item: SavedLeadItem = {
    id: `saved-${fireOpportunityId}`,
    status: "SAVED",
    createdAt: new Date().toISOString(),
    lead,
  };
  savedState.unshift(item);
  return item;
}
// Unknown ids mirror the API: 404 (PATCH/DELETE /api/saved-leads/{id}).
export async function updateSavedLead(id: string, status: SavedLeadStatus): Promise<void> {
  const idx = savedState.findIndex((s) => s.id === id);
  if (idx < 0) throw new ApiError("Saved lead not found", 404);
  savedState[idx] = { ...savedState[idx], status };
}
export async function unsaveLead(id: string): Promise<void> {
  const idx = savedState.findIndex((s) => s.id === id);
  if (idx < 0) throw new ApiError("Saved lead not found", 404);
  savedState.splice(idx, 1);
}
export async function getAccountMarkets(): Promise<Market[]> { return [...mockAccountMarkets]; }
export async function getAccountMe(): Promise<AccountMe> { return mockAccountForRole(); }
export async function updateEmailPreferences(_frequency: DigestFrequency): Promise<void> { /* mock no-op */ }
// Mock mode accepts the sample-lead form as a no-op success.
export async function submitSampleLeadRequest(_input: { name: string; email: string; company: string; marketSlug: string }): Promise<void> {}
export async function createCheckout(_plan: PlanTier, _marketSlugs: string[]): Promise<{ url: string }> { return { url: "#" }; }
export async function createBillingPortal(): Promise<{ url: string }> { return { url: "#" }; }
export async function getAdminSources(): Promise<AdminSource[]> { return mockAdminSources; }
export async function getAdminRuns(params: { sourceId?: string; page?: number }): Promise<Paged<ScraperRunSummary>> { return mockAdminRuns(params); }
export async function setSourceActive(id: string, active: boolean): Promise<void> {
  const src = mockAdminSources.find((s) => s.id === id);
  if (src) src.active = active;
}

// Internal fixture exports for app tests.
export { mockLeads, mockLeadDetails, mockLeadsResponse, mockLeadDetail } from "./leads";
export { mockSavedLeads } from "./saved";
export { mockAccountMe, mockAccountMarkets } from "./account";
export { mockAdminSources, mockAdminRuns } from "./admin";
export { mockMarkets, mockMarketStats } from "./markets";
