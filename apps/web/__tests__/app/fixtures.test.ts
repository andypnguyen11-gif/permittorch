import { describe, expect, it } from "vitest";
import {
  mockLeads,
  mockLeadDetails,
  mockLeadsResponse,
  mockLeadDetail,
} from "@/lib/fixtures/leads";
import { ApiError } from "@/lib/api";

const HOURS = 3_600_000;

const LOCKED_WEIGHTS: Record<string, number> = {
  NEW_COMMERCIAL_BUILD: 25, FIRE_SPRINKLER_SCOPE: 25, FIRE_ALARM_SCOPE: 20,
  FAILED_INSPECTION: 20, PERMIT_RECENT: 15, HIGH_PROJECT_VALUE: 10,
  LARGE_SQUARE_FOOTAGE: 10, NO_CONTRACTOR_LISTED: 10, OLD_PERMIT: -20, CLOSED_PERMIT: -30,
};
const clamp = (n: number) => Math.min(100, Math.max(0, n));

describe("fixture integrity", () => {
  it("has 25 leads with scores spanning the full 0–100 range", () => {
    expect(mockLeads).toHaveLength(25);
    const scores = mockLeads.map((l) => l.score);
    expect(Math.max(...scores)).toBe(100);
    expect(Math.min(...scores)).toBe(0);
  });

  it("covers every FireCategory and every PermitStatus", () => {
    const cats = new Set(mockLeads.map((l) => l.category));
    const statuses = new Set(mockLeads.map((l) => l.status));
    expect(cats).toEqual(
      new Set([
        "FIRE_SPRINKLER", "FIRE_ALARM", "FIRE_SUPPRESSION", "KITCHEN_SUPPRESSION",
        "FIRE_INSPECTION", "VIOLATION_CORRECTION", "GENERAL_FIRE_PROTECTION",
      ]),
    );
    expect(statuses).toEqual(
      new Set(["NEW", "ACTIVE", "INSPECTION", "FAILED", "CLOSED", "UNKNOWN"]),
    );
  });

  it("has a detail for every lead, and isNew means first detected < 72h ago", () => {
    expect(mockLeadDetails.map((d) => d.id)).toEqual(mockLeads.map((l) => l.id));
    for (const d of mockLeadDetails) {
      const age = Date.now() - Date.parse(d.firstDetectedAt);
      expect(d.isNew, d.id).toBe(age < 72 * HOURS);
    }
  });
});

describe("fixture scoring invariant (API scoring contract)", () => {
  it("every detail starts with the BASE_SCORE row", () => {
    for (const d of mockLeadDetails) {
      expect(d.signals[0], d.id).toEqual({
        signalType: "BASE_SCORE",
        description: "Baseline for a classified fire-protection permit",
        weight: 30,
      });
    }
  });

  it("every detail uses only locked signal types at their default weights, once each", () => {
    for (const d of mockLeadDetails) {
      const rest = d.signals.slice(1);
      for (const s of rest) {
        expect(LOCKED_WEIGHTS, `${d.id} ${s.signalType}`).toHaveProperty(s.signalType);
        expect(s.weight, `${d.id} ${s.signalType}`).toBe(LOCKED_WEIGHTS[s.signalType]);
      }
      expect(new Set(rest.map((s) => s.signalType)).size, d.id).toBe(rest.length);
    }
  });

  it("every detail has score === clamp(Σ weights, 0, 100), and the summary agrees", () => {
    for (const d of mockLeadDetails) {
      expect(d.score, d.id).toBe(clamp(d.signals.reduce((a, s) => a + s.weight, 0)));
      expect(mockLeads.find((l) => l.id === d.id)!.score, d.id).toBe(d.score);
    }
  });

  it("every detail fires signals consistently with its permit data (WS1 rules)", () => {
    for (const d of mockLeadDetails) {
      const types = new Set(d.signals.map((s) => s.signalType));
      const filedAge = d.filedDate ? Date.now() - Date.parse(d.filedDate) : null;
      const check = (type: string, expected: boolean) =>
        expect(types.has(type), `${d.id} ${type}`).toBe(expected);
      check("FIRE_SPRINKLER_SCOPE", d.category === "FIRE_SPRINKLER");
      check("FIRE_ALARM_SCOPE", d.category === "FIRE_ALARM");
      check("FAILED_INSPECTION", d.status === "FAILED");
      check("CLOSED_PERMIT", d.status === "CLOSED");
      check("PERMIT_RECENT", filedAge != null && filedAge < 72 * HOURS);
      check("OLD_PERMIT", filedAge != null && filedAge > 90 * 24 * HOURS);
      check("HIGH_PROJECT_VALUE", (d.estimatedValue ?? 0) > 500_000);
      check("LARGE_SQUARE_FOOTAGE", (d.permit.squareFootage ?? 0) > 20_000);
      check("NO_CONTRACTOR_LISTED", !d.permit.contractorName);
    }
  });

  it("includes capped and floored examples", () => {
    const sums = mockLeadDetails.map((d) => d.signals.reduce((a, s) => a + s.weight, 0));
    expect(sums.some((n) => n > 100)).toBe(true);
    expect(sums.some((n) => n < 0)).toBe(true);
  });
});

describe("mockLeadsResponse filtering", () => {
  it("defaults: page 1, pageSize 25, all leads, freshness present", () => {
    const res = mockLeadsResponse({});
    expect(res.items).toHaveLength(25);
    expect(res.total).toBe(25);
    expect(res.page).toBe(1);
    expect(res.pageSize).toBe(25);
    expect(res.freshness.lastUpdatedAt).not.toBeNull();
  });

  it("filters by category", () => {
    const res = mockLeadsResponse({ category: "FIRE_ALARM" });
    expect(res.items.length).toBeGreaterThan(0);
    expect(res.items.every((l) => l.category === "FIRE_ALARM")).toBe(true);
    expect(res.total).toBe(res.items.length);
  });

  it("filters by minScore", () => {
    const res = mockLeadsResponse({ minScore: 90 });
    expect(res.items.length).toBeGreaterThan(0);
    expect(res.items.every((l) => l.score >= 90)).toBe(true);
  });

  it("filters by maxAgeDays and excludes null filedDate", () => {
    const res = mockLeadsResponse({ maxAgeDays: 3 });
    expect(res.items.length).toBeGreaterThan(0);
    for (const l of res.items) {
      expect(l.filedDate).not.toBeNull();
      expect(Date.now() - Date.parse(l.filedDate!)).toBeLessThanOrEqual(3 * 24 * HOURS);
    }
  });

  it("filters by status", () => {
    const res = mockLeadsResponse({ status: "FAILED" });
    expect(res.items.every((l) => l.status === "FAILED")).toBe(true);
    expect(res.items.length).toBeGreaterThan(0);
  });

  it("filters by market slug (city-state)", () => {
    const houston = mockLeadsResponse({ market: "houston-tx" });
    const dallas = mockLeadsResponse({ market: "dallas-tx" });
    expect(houston.items.every((l) => l.city === "Houston")).toBe(true);
    expect(dallas.items.every((l) => l.city === "Dallas")).toBe(true);
    expect(houston.total + dallas.total).toBe(25);
  });

  it("searches q case-insensitively across title, address, city, reason", () => {
    const res = mockLeadsResponse({ q: "warehouse" });
    expect(res.items.length).toBeGreaterThan(0);
    for (const l of res.items) {
      const hay = `${l.title} ${l.address ?? ""} ${l.city} ${l.reason}`.toLowerCase();
      expect(hay).toContain("warehouse");
    }
  });

  it("combines filters (AND semantics)", () => {
    const res = mockLeadsResponse({ category: "FIRE_SPRINKLER", minScore: 90 });
    expect(res.items.every((l) => l.category === "FIRE_SPRINKLER" && l.score >= 90)).toBe(true);
  });

  it("sorts by score desc, then most recently first-detected, before paginating", () => {
    const items = mockLeadsResponse({ pageSize: 100 }).items;
    const detected = new Map(mockLeadDetails.map((d) => [d.id, Date.parse(d.firstDetectedAt)]));
    for (let i = 1; i < items.length; i++) {
      const [a, b] = [items[i - 1], items[i]];
      expect(a.score).toBeGreaterThanOrEqual(b.score);
      if (a.score === b.score) expect(detected.get(a.id)!).toBeGreaterThanOrEqual(detected.get(b.id)!);
    }
    // lead-002 (100, detected 18h ago) outranks lead-001 (100, detected 38h ago).
    expect(items.slice(0, 3).map((l) => l.id)).toEqual(["lead-002", "lead-020", "lead-001"]);
    const firstPage = mockLeadsResponse({ pageSize: 3 }).items.map((l) => l.id);
    expect(firstPage).toEqual(["lead-002", "lead-020", "lead-001"]);
  });

  it("paginates: page 2 of pageSize 10 returns items 11–20 of the filtered set", () => {
    const all = mockLeadsResponse({ pageSize: 100 }).items;
    const page2 = mockLeadsResponse({ page: 2, pageSize: 10 });
    expect(page2.items).toHaveLength(10);
    expect(page2.total).toBe(25);
    expect(page2.page).toBe(2);
    expect(page2.pageSize).toBe(10);
    expect(page2.items[0].id).toBe(all[10].id);
  });

  it("returns an empty page, not an error, when nothing matches", () => {
    const res = mockLeadsResponse({ q: "zzz-no-such-lead" });
    expect(res.items).toHaveLength(0);
    expect(res.total).toBe(0);
  });
});

describe("mockLeadDetail", () => {
  it("returns the detail for lead-001", () => {
    const d = mockLeadDetail("lead-001");
    expect(d.score).toBe(100);
    expect(d.signals.length).toBeGreaterThanOrEqual(5);
    expect(d.permit.permitNumber).not.toBeNull();
  });

  it("throws an ApiError 404 for an unknown id, like the real client", () => {
    expect(() => mockLeadDetail("nope")).toThrow(ApiError);
    expect(() => mockLeadDetail("nope")).toThrow(expect.objectContaining({ status: 404 }));
  });
});

import { mockSavedLeads } from "@/lib/fixtures/saved";
import { mockAccountMe } from "@/lib/fixtures/account";
import { mockAdminSources, mockAdminRuns } from "@/lib/fixtures/admin";

describe("saved/account/admin fixtures", () => {
  it("saved leads reference real fixture leads", () => {
    expect(mockSavedLeads).toHaveLength(3);
    for (const s of mockSavedLeads) {
      expect(mockLeads.some((l) => l.id === s.lead.id)).toBe(true);
    }
    expect(new Set(mockSavedLeads.map((s) => s.status))).toEqual(new Set(["SAVED", "CONTACTED"]));
  });

  it("account fixture is a Pro-plan super admin", () => {
    expect(mockAccountMe.plan).toBe("PRO");
    expect(mockAccountMe.role).toBe("SUPER_ADMIN");
    expect(mockAccountMe.email).toContain("@");
  });

  it("admin sources cover all five health statuses", () => {
    expect(mockAdminSources).toHaveLength(5);
    expect(new Set(mockAdminSources.map((s) => s.healthStatus))).toEqual(
      new Set(["HEALTHY", "WARNING", "STALE", "FAILED", "DISABLED"]),
    );
  });

  it("mockAdminRuns paginates and filters by sourceId", () => {
    const all = mockAdminRuns();
    expect(all.items).toHaveLength(10);
    expect(all.total).toBe(10);
    const filtered = mockAdminRuns({ sourceId: "src-001" });
    expect(filtered.items.length).toBeGreaterThan(0);
    expect(filtered.items.length).toBeLessThan(10);
    for (const run of all.items) expect(run).not.toHaveProperty("__sourceId");
  });
});

import * as fixtures from "@/lib/fixtures";

describe("fixtures index (lib/api.ts mock contract)", () => {
  it("getLeads and getLead adapt the lead fixtures", async () => {
    const res = await fixtures.getLeads({ category: "FIRE_ALARM" });
    expect(res.items.every((l) => l.category === "FIRE_ALARM")).toBe(true);
    await expect(fixtures.getLead("lead-001")).resolves.toMatchObject({ id: "lead-001", score: 100 });
  });

  it("saveLead / updateSavedLead / unsaveLead mutate in-memory saved state", async () => {
    const before = (await fixtures.getSavedLeads()).length;
    const item = await fixtures.saveLead("lead-002");
    expect(item.status).toBe("SAVED");
    expect(item.lead.id).toBe("lead-002");
    expect(await fixtures.getSavedLeads()).toHaveLength(before + 1);
    await fixtures.updateSavedLead(item.id, "CONTACTED");
    expect((await fixtures.getSavedLeads()).find((s) => s.id === item.id)?.status).toBe("CONTACTED");
    await fixtures.unsaveLead(item.id);
    expect(await fixtures.getSavedLeads()).toHaveLength(before);
  });

  it("saveLead rejects an unknown lead id with an ApiError 404", async () => {
    await expect(fixtures.saveLead("lead-nope")).rejects.toMatchObject({ name: "ApiError", status: 404 });
  });

  it("getAccountMarkets returns the entitled fixture markets matching lead cities", async () => {
    const markets = await fixtures.getAccountMarkets();
    expect(markets.map((m) => m.slug)).toEqual(["houston-tx", "dallas-tx"]);
  });

  it("billing fixtures resolve to a non-navigable placeholder url", async () => {
    await expect(fixtures.createCheckout("PRO")).resolves.toEqual({ url: "#" });
    await expect(fixtures.createBillingPortal()).resolves.toEqual({ url: "#" });
  });
});
