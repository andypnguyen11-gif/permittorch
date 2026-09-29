import type {
  AccountMe,
  AdminSource,
  DigestFrequency,
  FireCategory,
  LeadDetail,
  LeadsResponse,
  Market,
  MarketStats,
  Paged,
  PermitStatus,
  PlanTier,
  Removal,
  RemovalKind,
  RemovalPreview,
  RemovalRecord,
  SavedLeadItem,
  SavedLeadStatus,
  ScraperRunSummary,
} from "@permittorch/types";
import { resolveApiBaseUrl } from "@/lib/api-base-url";

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = "ApiError";
  }
}

export interface LeadsQuery {
  market?: string; category?: FireCategory; minScore?: number;
  maxAgeDays?: number; status?: PermitStatus; q?: string; page?: number; pageSize?: number;
}

function isMock(): boolean {
  return process.env.NEXT_PUBLIC_API_MOCK === "1";
}

// Fixtures are owned by WS4 (apps/web/lib/fixtures/). Imported lazily so the
// fixture module is only ever loaded when NEXT_PUBLIC_API_MOCK=1 — marketing
// builds never touch it.
function fixtures() {
  return import("@/lib/fixtures");
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
  token?: string,
): Promise<T> {
  // Literal process.env references so Next inlines them into the client bundle.
  const base = resolveApiBaseUrl({
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
    NODE_ENV: process.env.NODE_ENV,
  });
  const headers = new Headers(init.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (init.body !== undefined) headers.set("Content-Type", "application/json");

  const res = await fetch(`${base}${path}`, { ...init, headers });

  if (!res.ok) {
    let message = `API request failed with status ${res.status}`;
    try {
      const body = (await res.json()) as { error?: string };
      if (body.error) message = body.error;
    } catch {
      // Non-JSON error body: keep the generic message.
    }
    throw new ApiError(message, res.status);
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export async function getLeads(params: LeadsQuery, token: string): Promise<LeadsResponse> {
  if (isMock()) return (await fixtures()).getLeads(params);
  const qs = buildQuery({
    market: params.market,
    category: params.category,
    minScore: params.minScore,
    maxAgeDays: params.maxAgeDays,
    status: params.status,
    q: params.q,
    page: params.page,
    pageSize: params.pageSize,
  });
  return apiFetch<LeadsResponse>(`/api/leads${qs}`, {}, token);
}

export interface LeadsExport { blob: Blob; truncated: boolean; }

// The export covers every lead the filters match, up to the API's limit, so paging is left
// out. `truncated` is true when the API cut the file at that limit.
export async function exportLeadsCsv(params: LeadsQuery, token: string): Promise<LeadsExport> {
  if (isMock()) return (await fixtures()).exportLeadsCsv(params);
  const qs = buildQuery({
    market: params.market,
    category: params.category,
    minScore: params.minScore,
    maxAgeDays: params.maxAgeDays,
    status: params.status,
    q: params.q,
  });
  const base = resolveApiBaseUrl({
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
    NODE_ENV: process.env.NODE_ENV,
  });
  const res = await fetch(`${base}/api/leads/export.csv${qs}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) {
    let message = `API request failed with status ${res.status}`;
    try {
      const body = (await res.json()) as { error?: string };
      if (body.error) message = body.error;
    } catch {
      // Non-JSON error body: keep the generic message.
    }
    throw new ApiError(message, res.status);
  }
  return { blob: await res.blob(), truncated: res.headers.get("X-Truncated") === "true" };
}

export async function getLead(id: string, token: string): Promise<LeadDetail> {
  if (isMock()) return (await fixtures()).getLead(id);
  return apiFetch<LeadDetail>(`/api/leads/${encodeURIComponent(id)}`, {}, token);
}

// Markets bypass the fixtures index: WS3 (marketing) needs mock markets in a
// worktree where lib/fixtures/index.ts is still the WS0 throwing stub, so
// these two functions import WS3-owned lib/fixtures/markets.ts directly.
export async function getMarkets(): Promise<Market[]> {
  if (isMock()) return (await import("@/lib/fixtures/markets")).mockMarkets;
  return apiFetch<Market[]>("/api/markets");
}

export async function getMarketStats(slug: string): Promise<MarketStats> {
  if (isMock()) {
    const stats = (await import("@/lib/fixtures/markets")).mockMarketStats[slug];
    if (!stats) throw new Error(`Unknown market: ${slug}`);
    return stats;
  }
  return apiFetch<MarketStats>(`/api/markets/${encodeURIComponent(slug)}/stats`);
}

/** Stats for every active market in one request (marketing pages need them all). */
export async function getAllMarketStats(): Promise<MarketStats[]> {
  if (isMock()) return Object.values((await import("@/lib/fixtures/markets")).mockMarketStats);
  return apiFetch<MarketStats[]>("/api/markets/stats");
}

export async function getSavedLeads(token: string): Promise<SavedLeadItem[]> {
  if (isMock()) return (await fixtures()).getSavedLeads();
  return apiFetch<SavedLeadItem[]>("/api/saved-leads", {}, token);
}

export async function saveLead(fireOpportunityId: string, token: string): Promise<SavedLeadItem> {
  if (isMock()) return (await fixtures()).saveLead(fireOpportunityId);
  return apiFetch<SavedLeadItem>(
    "/api/saved-leads",
    { method: "POST", body: JSON.stringify({ fireOpportunityId }) },
    token,
  );
}

export async function updateSavedLead(id: string, status: SavedLeadStatus, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).updateSavedLead(id, status);
  return apiFetch<void>(
    `/api/saved-leads/${encodeURIComponent(id)}`,
    { method: "PATCH", body: JSON.stringify({ status }) },
    token,
  );
}

export async function unsaveLead(id: string, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).unsaveLead(id);
  return apiFetch<void>(`/api/saved-leads/${encodeURIComponent(id)}`, { method: "DELETE" }, token);
}

export async function getAccountMarkets(token: string): Promise<Market[]> {
  if (isMock()) return (await fixtures()).getAccountMarkets();
  return apiFetch<Market[]>("/api/account/markets", {}, token);
}

export async function getAccountMe(token: string): Promise<AccountMe> {
  if (isMock()) return (await fixtures()).getAccountMe();
  return apiFetch<AccountMe>("/api/account/me", {}, token);
}

export async function updateEmailPreferences(frequency: DigestFrequency, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).updateEmailPreferences(frequency);
  return apiFetch<void>(
    "/api/email-preferences",
    { method: "PUT", body: JSON.stringify({ frequency }) },
    token,
  );
}

// The API records the agreement only when `version` is the one it holds as current (409 otherwise).
export async function acceptTerms(version: string, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).acceptTerms(version);
  return apiFetch<void>("/api/account/terms", { method: "POST", body: JSON.stringify({ version }) }, token);
}

export async function submitSampleLeadRequest(
  input: { name: string; email: string; company: string; marketSlug: string },
): Promise<void> {
  if (isMock()) return (await fixtures()).submitSampleLeadRequest(input);
  return apiFetch<void>("/api/sample-leads", { method: "POST", body: JSON.stringify(input) });
}

// Entitlement comes only from the markets attached to the subscription, so
// checkout always carries the market selection (exactly 1 slug for
// STARTER/PRO, 1–5 for TERRITORY — the API validates and returns 400 otherwise).
export async function createCheckout(plan: PlanTier, marketSlugs: string[], token: string): Promise<{ url: string }> {
  if (isMock()) return (await fixtures()).createCheckout(plan, marketSlugs);
  return apiFetch<{ url: string }>(
    "/api/billing/checkout",
    { method: "POST", body: JSON.stringify({ plan, marketSlugs }) },
    token,
  );
}

export async function createBillingPortal(token: string): Promise<{ url: string }> {
  if (isMock()) return (await fixtures()).createBillingPortal();
  return apiFetch<{ url: string }>("/api/billing/portal", { method: "POST" }, token);
}

export async function getAdminSources(token: string): Promise<AdminSource[]> {
  if (isMock()) return (await fixtures()).getAdminSources();
  return apiFetch<AdminSource[]>("/api/admin/sources", {}, token);
}

export async function getAdminRuns(
  params: { sourceId?: string; page?: number },
  token: string,
): Promise<Paged<ScraperRunSummary>> {
  if (isMock()) return (await fixtures()).getAdminRuns(params);
  const qs = buildQuery({ sourceId: params.sourceId, page: params.page });
  return apiFetch<Paged<ScraperRunSummary>>(`/api/admin/scraper-runs${qs}`, {}, token);
}

export async function setSourceActive(id: string, active: boolean, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).setSourceActive(id, active);
  return apiFetch<void>(
    `/api/admin/sources/${encodeURIComponent(id)}/${active ? "enable" : "disable"}`,
    { method: "POST" },
    token,
  );
}

export interface CreateRemovalInput {
  kind: RemovalKind; value?: string; permitId?: string; note?: string; confirmedCount: number;
}

export async function getRemovals(token: string, page = 1): Promise<Paged<Removal>> {
  if (isMock()) return (await fixtures()).getRemovals();
  return apiFetch<Paged<Removal>>(`/api/admin/removals${buildQuery({ page, pageSize: 100 })}`, {}, token);
}

// Counts what a removal would change. Changes nothing.
export async function previewRemoval(kind: RemovalKind, value: string, token: string): Promise<RemovalPreview> {
  if (isMock()) return (await fixtures()).previewRemoval();
  return apiFetch<RemovalPreview>(
    "/api/admin/removals/preview", { method: "POST", body: JSON.stringify({ kind, value }) }, token);
}

export async function searchRemovalRecords(market: string, q: string, token: string): Promise<RemovalRecord[]> {
  if (isMock()) return (await fixtures()).searchRemovalRecords();
  return apiFetch<RemovalRecord[]>(`/api/admin/removals/records${buildQuery({ market, q })}`, {}, token);
}

// `confirmedCount` is the count the admin saw. The API refuses the removal (409
// "count_changed") when it is no longer the number of permits that match.
export async function createRemoval(input: CreateRemovalInput, token: string): Promise<Removal> {
  if (isMock()) return (await fixtures()).createRemoval(input);
  return apiFetch<Removal>("/api/admin/removals", { method: "POST", body: JSON.stringify(input) }, token);
}

export async function undoRemoval(id: string, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).undoRemoval(id);
  return apiFetch<void>(`/api/admin/removals/${encodeURIComponent(id)}`, { method: "DELETE" }, token);
}
