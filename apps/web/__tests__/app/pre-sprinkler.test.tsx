// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import type { PreSprinklerContact, PreSprinklerLead } from "@permittorch/types";
import * as api from "@/lib/api";
import AdminPreSprinklerPage from "@/app/app/admin/pre-sprinkler/page";
import { groupPreSprinklerLeads } from "@/components/app/admin/pre-sprinkler-groups";
import { readPreSprinklerQuery } from "@/components/app/admin/pre-sprinkler-query";
import { mockAccountMe } from "@/lib/fixtures/account";
import { mockPreSprinklerLeads } from "@/lib/fixtures/admin";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

const builder: PreSprinklerContact = {
  role: "CONTRACTOR", roleLabel: "Contractor", name: "Test Builder", company: null,
  phone: "(555) 010-0001", email: "b@example.com", licenseNumber: null,
};
const lead = (id: string, street: string | null, contacts: PreSprinklerContact[] = [builder], city = "Fremont"): PreSprinklerLead =>
  ({ ...mockPreSprinklerLeads[0], id, street, city, contacts });

describe("groupPreSprinklerLeads", () => {
  it("puts leads with the same address and first contact under one heading, in order", () => {
    const groups = groupPreSprinklerLeads([
      lead("a", "100 Test Ave"), lead("b", "7 Other St"), lead("c", "100  test ave"), lead("d", "100 Test Ave"),
    ]);
    expect(groups.map((g) => g.leads.map((l) => l.id))).toEqual([["a", "c", "d"], ["b"]]);
    expect(groups[0].heading).toBe("100 Test Ave, Fremont · 3 permits · Contractor: Test Builder");
    expect(groups[1].heading).toBeNull();
  });

  it("keeps apart leads at one address with a different first contact or city", () => {
    const other = { ...builder, name: "Test Owner", role: "OWNER" as const };
    const groups = groupPreSprinklerLeads([
      lead("a", "100 Test Ave"), lead("b", "100 Test Ave", [other, builder]), lead("c", "100 Test Ave", [builder], "Hayward"),
    ]);
    expect(groups).toHaveLength(3);
    expect(groups.every((g) => g.heading === null)).toBe(true);
  });

  it("never groups leads without a street or without a contact", () => {
    const groups = groupPreSprinklerLeads([lead("a", null), lead("b", null), lead("c", "1 A St", []), lead("d", "1 A St", [])]);
    expect(groups).toHaveLength(4);
  });
});

describe("readPreSprinklerQuery", () => {
  it("defaults to OPEN and drops values the page does not offer", () => {
    expect(readPreSprinklerQuery({ market: "oakland-ca", kind: "REMODEL", filedFrom: "08/01/2026" }, ["fremont-ca"]))
      .toEqual({ market: undefined, kind: undefined, standing: "OPEN", filedFrom: undefined, filedTo: undefined });
    expect(readPreSprinklerQuery({ market: "fremont-ca", kind: "ADU", standing: "ALL", filedTo: "2026-09-30" }, ["fremont-ca"]))
      .toEqual({ market: "fremont-ca", kind: "ADU", standing: "ALL", filedFrom: undefined, filedTo: "2026-09-30" });
  });
});

describe("pre-sprinkler page", () => {
  it("shows open leads by default, the six tract lots under one heading", async () => {
    render(await AdminPreSprinklerPage({ searchParams: Promise.resolve({}) }));
    expect(screen.getByRole("heading", { level: 1, name: "Pre-sprinkler leads" })).toBeInTheDocument();
    const table = screen.getByRole("table");
    const headings = table.querySelectorAll("[data-group-heading]");
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent("100 Test Ave, Fremont · 6 permits · Applicant: Test Builder (Test Homes Inc)");
    expect(table.querySelectorAll("[data-standing]")).toHaveLength(7);
    expect(table.querySelectorAll("[data-standing='SPRINKLER_FILED']")).toHaveLength(0);
    expect(within(table).getAllByText("No sprinkler permit yet")[0]).toHaveAttribute("title",
      expect.stringContaining("No fire sprinkler application found at this address in Fremont's fire permits"));
    expect(within(table).getAllByRole("link", { name: "(555) 010-0001" })[0]).toHaveAttribute("href", "tel:+15550100001");
    expect(screen.getByRole("button", { name: /Download CSV/ })).toBeInTheDocument();
    expect(screen.getByText("7 leads")).toBeInTheDocument();
  });

  it("shows sprinkler-filed leads with the permit number when asked for", async () => {
    render(await AdminPreSprinklerPage({ searchParams: Promise.resolve({ standing: "SPRINKLER_FILED" }) }));
    expect(screen.getByText("Sprinkler permit filed: FSP-0008")).toBeInTheDocument();
    expect(screen.getByText("1 lead")).toBeInTheDocument();
  });

  it("passes the filters on screen to the CSV download", async () => {
    const exportSpy = vi.spyOn(api, "exportPreSprinklerCsv");
    URL.createObjectURL = vi.fn(() => "blob:x");
    URL.revokeObjectURL = vi.fn();
    render(await AdminPreSprinklerPage({ searchParams: Promise.resolve({ kind: "ADU", market: "hayward-ca" }) }));
    screen.getByRole("button", { name: /Download CSV/ }).click();
    await vi.waitFor(() => expect(exportSpy).toHaveBeenCalledWith(
      { market: "hayward-ca", kind: "ADU", standing: "OPEN", filedFrom: undefined, filedTo: undefined }, "mock-token"));
    exportSpy.mockRestore();
  });

  it("redirects a MEMBER before loading any lead", async () => {
    const me = vi.spyOn(api, "getAccountMe").mockResolvedValue({ ...mockAccountMe, role: "MEMBER" });
    const leads = vi.spyOn(api, "getPreSprinklerLeads");
    await expect(AdminPreSprinklerPage({ searchParams: Promise.resolve({}) })).rejects.toThrow("REDIRECT:/app");
    expect(leads).not.toHaveBeenCalled();
    me.mockRestore();
    leads.mockRestore();
  });
});
