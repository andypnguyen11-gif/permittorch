// FIXTURE CONTRACT: lib/api.ts (frozen) calls these by name in mock mode.
// Signatures mirror lib/api.ts with token parameters dropped. Do NOT add,
// remove, or rename these functions. Bodies are thin adapters over the
// mock* fixture internals below.
// NOTE: getMarkets/getMarketStats are intentionally ABSENT — lib/api.ts's
// mock branch imports mockMarkets/mockMarketStats from ./markets (WS3-owned).
import type {
  AccountMe, AdminSource, DigestFrequency, EmailPreferences, FeedbackInput, LeadDetail, LeadsResponse,
  Market, Paged, PlanTier, PreSprinklerLead, PreSprinklerMarket, Removal, RemovalPreview, RemovalRecord,
  SavedLeadItem, SavedLeadStatus, ScraperRunSummary,
} from "@permittorch/types";
import {
  ApiError, type CreateRemovalInput, type LeadsExport, type LeadsQuery, type PreSprinklerQuery,
} from "@/lib/api";
import { mockLeads, mockLeadsResponse, mockLeadDetail } from "./leads";
import { mockSavedLeads } from "./saved";
import { mockAccountForRole, mockAccountMarkets, mockAccountMe } from "./account";
import {
  mockAdminSources, mockAdminRuns, mockRemovals, mockRemovalPreview, mockRemovalRecords,
  mockPreSprinklerLeads, mockPreSprinklerMarkets,
} from "./admin";

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
export async function getEmailPreferences(): Promise<EmailPreferences> {
  return { frequency: mockAccountMe.digestFrequency, marketSlugs: [mockAccountMarkets[0].slug] };
}
export async function updateEmailPreferences(_frequency: DigestFrequency, _marketSlugs?: string[]): Promise<void> { /* mock no-op */ }
export async function acceptTerms(_version: string): Promise<void> { /* mock no-op */ }
// Mock mode accepts the sample-lead form as a no-op success.
export async function submitFeedback(_input: FeedbackInput): Promise<void> {}

export async function submitSampleLeadRequest(_input: { name: string; email: string; company: string; marketSlug: string }): Promise<void> {}
export async function createCheckout(_plan: PlanTier, _marketSlugs: string[]): Promise<{ url: string }> { return { url: "#" }; }
export async function createBillingPortal(): Promise<{ url: string }> { return { url: "#" }; }
export async function getAdminSources(): Promise<AdminSource[]> { return mockAdminSources; }
export async function getAdminRuns(params: { sourceId?: string; page?: number }): Promise<Paged<ScraperRunSummary>> { return mockAdminRuns(params); }
export async function setSourceActive(id: string, active: boolean): Promise<void> {
  const src = mockAdminSources.find((s) => s.id === id);
  if (src) src.active = active;
}
export async function getRemovals(): Promise<Paged<Removal>> {
  return { items: mockRemovals, total: mockRemovals.length, page: 1, pageSize: 25 };
}
export async function previewRemoval(): Promise<RemovalPreview> { return mockRemovalPreview; }
export async function searchRemovalRecords(): Promise<RemovalRecord[]> { return mockRemovalRecords; }
export async function createRemoval(input: CreateRemovalInput): Promise<Removal> {
  return { id: "rem-new", kind: input.kind, value: input.value ?? "BLD-2026-0117", label: null,
    note: input.note ?? null, recordsAffected: input.confirmedCount, createdAt: new Date().toISOString() };
}
export async function undoRemoval(_id: string): Promise<void> { /* mock no-op */ }
// Mirrors the API's filters closely enough for mock dev: standing defaults to OPEN, "ALL" shows every one.
function preSprinklerMatches(params: PreSprinklerQuery): PreSprinklerLead[] {
  const standing = params.standing ?? "OPEN";
  return mockPreSprinklerLeads
    .filter((l) => standing === "ALL" || l.standing === standing)
    .filter((l) => !params.market || l.marketSlug === params.market)
    .filter((l) => !params.kind || l.kind === params.kind)
    .filter((l) => !params.filedFrom || (l.filedOn !== null && l.filedOn >= params.filedFrom))
    .filter((l) => !params.filedTo || (l.filedOn !== null && l.filedOn <= params.filedTo))
    .sort((a, b) => b.score - a.score);
}
export async function getPreSprinklerLeads(params: PreSprinklerQuery): Promise<Paged<PreSprinklerLead>> {
  const items = preSprinklerMatches(params);
  return { items, total: items.length, page: 1, pageSize: 100 };
}
export async function getPreSprinklerMarkets(): Promise<PreSprinklerMarket[]> { return mockPreSprinklerMarkets; }
export async function exportPreSprinklerCsv(params: PreSprinklerQuery): Promise<LeadsExport> {
  const quote = (value: string) => `"${value.replace(/"/g, '""')}"`;
  const lines = ["City,Address,Building permit #",
    ...preSprinklerMatches(params).map((l) => `${quote(l.city ?? "")},${quote(l.street ?? "")},${quote(l.permitNumber)}`)];
  return { blob: new Blob([lines.join("\r\n") + "\r\n"], { type: "text/csv" }), truncated: false };
}

// Internal fixture exports for app tests.
export { mockLeads, mockLeadDetails, mockLeadsResponse, mockLeadDetail } from "./leads";
export { mockSavedLeads } from "./saved";
export { mockAccountMe, mockAccountMarkets } from "./account";
export {
  mockAdminSources, mockAdminRuns, mockRemovals, mockRemovalPreview, mockRemovalRecords,
  mockPreSprinklerLeads, mockPreSprinklerMarkets,
} from "./admin";
export { mockMarkets, mockMarketStats } from "./markets";
