import { describe, expect, it } from "vitest";
import {
  mockLeads,
  mockLeadDetails,
  mockLeadsResponse,
  mockLeadDetail,
} from "@/lib/fixtures/leads";

const HOURS = 3_600_000;

describe("fixture integrity", () => {
  it("has 25 leads with scores spread 41–94", () => {
    expect(mockLeads).toHaveLength(25);
    const scores = mockLeads.map((l) => l.score);
    expect(Math.max(...scores)).toBe(94);
    expect(Math.min(...scores)).toBe(41);
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

  it("marks isNew consistently with filedDate < 72h", () => {
    for (const l of mockLeads) {
      if (l.isNew) {
        expect(l.filedDate).not.toBeNull();
        expect(Date.now() - Date.parse(l.filedDate!)).toBeLessThan(72 * HOURS);
      }
    }
  });

  it("curated detail signals sum exactly to the score (explainability)", () => {
    expect(mockLeadDetails).toHaveLength(5);
    for (const d of mockLeadDetails) {
      const sum = d.signals.reduce((acc, s) => acc + s.weight, 0);
      expect(sum).toBe(d.score);
    }
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
  it("returns the curated detail for lead-001", () => {
    const d = mockLeadDetail("lead-001");
    expect(d.score).toBe(94);
    expect(d.signals.length).toBeGreaterThanOrEqual(5);
    expect(d.permit.permitNumber).not.toBeNull();
  });

  it("synthesizes a fallback detail whose signals sum to the score", () => {
    const d = mockLeadDetail("lead-002"); // not curated
    expect(d.id).toBe("lead-002");
    expect(d.signals.reduce((a, s) => a + s.weight, 0)).toBe(d.score);
    expect(d.source.name.length).toBeGreaterThan(0);
  });

  it("throws for an unknown id", () => {
    expect(() => mockLeadDetail("nope")).toThrowError(/No fixture lead/);
  });
});
