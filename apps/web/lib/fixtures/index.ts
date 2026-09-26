// FIXTURE CONTRACT (WS0 stub). WS4 owns this directory and replaces each
// body with realistic fixture data matching @permittorch/types exactly.
// Signatures mirror lib/api.ts with token parameters dropped. Do NOT add,
// remove, or rename exports — lib/api.ts (frozen) calls them by name.
import type {
  AccountMe,
  AdminSource,
  DigestFrequency,
  LeadDetail,
  LeadsResponse,
  Market,
  Paged,
  PlanTier,
  SavedLeadItem,
  SavedLeadStatus,
  ScraperRunSummary,
} from "@permittorch/types";
import type { LeadsQuery } from "@/lib/api";

function notImplemented(name: string): never {
  throw new Error(`Fixture "${name}" is not implemented yet (WS4 owns apps/web/lib/fixtures/)`);
}

// NOTE: getMarkets/getMarketStats are intentionally ABSENT — lib/api.ts's
// mock branch imports mockMarkets/mockMarketStats from ./markets (WS3-owned).
export async function getLeads(_params: LeadsQuery): Promise<LeadsResponse> { notImplemented("getLeads"); }
export async function getLead(_id: string): Promise<LeadDetail> { notImplemented("getLead"); }
export async function getSavedLeads(): Promise<SavedLeadItem[]> { notImplemented("getSavedLeads"); }
export async function saveLead(_fireOpportunityId: string): Promise<SavedLeadItem> { notImplemented("saveLead"); }
export async function updateSavedLead(_id: string, _status: SavedLeadStatus): Promise<void> { notImplemented("updateSavedLead"); }
export async function unsaveLead(_id: string): Promise<void> { notImplemented("unsaveLead"); }
export async function getAccountMarkets(): Promise<Market[]> { notImplemented("getAccountMarkets"); }
export async function getAccountMe(): Promise<AccountMe> { notImplemented("getAccountMe"); }
export async function updateEmailPreferences(_frequency: DigestFrequency): Promise<void> { notImplemented("updateEmailPreferences"); }
export async function submitSampleLeadRequest(_input: { name: string; email: string; company: string; marketSlug: string }): Promise<void> { notImplemented("submitSampleLeadRequest"); }
export async function createCheckout(_plan: PlanTier): Promise<{ url: string }> { notImplemented("createCheckout"); }
export async function createBillingPortal(): Promise<{ url: string }> { notImplemented("createBillingPortal"); }
export async function getAdminSources(): Promise<AdminSource[]> { notImplemented("getAdminSources"); }
export async function getAdminRuns(_params: { sourceId?: string; page?: number }): Promise<Paged<ScraperRunSummary>> { notImplemented("getAdminRuns"); }
export async function setSourceActive(_id: string, _active: boolean): Promise<void> { notImplemented("setSourceActive"); }
