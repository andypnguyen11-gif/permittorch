// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import "./dom-cleanup";
import { render, screen, within } from "@testing-library/react";
import type { LeadOutcomeReport, Market } from "@permittorch/types";
import { OutcomeReportFilters, reportFilters } from "@/components/app/admin/outcome-report-filters";

const markets = [{ id: "m1", name: "Houston, TX" }, { id: "m2", name: "Tulsa, OK" }] as Market[];

const report = (o: Partial<LeadOutcomeReport> = {}): LeadOutcomeReport => ({
  bands: [], minAnswersPerBand: 10, version: "r2-bbbbbb", currentVersion: "r2-bbbbbb", permitAge: "ALL",
  versions: [
    { version: "r2-bbbbbb", answers: 4 },
    { version: "r1-aaaaaa", answers: 30 },
    { version: "unversioned", answers: 7 },
  ],
  ...o,
});

const options = (name: string) =>
  within(screen.getByRole("combobox", { name })).getAllByRole("option").map((o) => o.textContent);

describe("OutcomeReportFilters", () => {
  it("lists each scoring version with its answers, the current one first", () => {
    render(<OutcomeReportFilters markets={markets} report={report()} />);
    expect(options("Scoring version")).toEqual([
      "r2-bbbbbb (current) · 4 answers",
      "r1-aaaaaa · 30 answers",
      "Before versioning · 7 answers",
    ]);
    expect(screen.getByRole("combobox", { name: "Scoring version" })).toHaveValue("r2-bbbbbb");
  });

  it("still offers the current version before anyone has tagged under it", () => {
    render(<OutcomeReportFilters markets={markets}
      report={report({ version: "r3-cccccc", currentVersion: "r3-cccccc", versions: [{ version: "r2-bbbbbb", answers: 4 }] })} />);
    expect(options("Scoring version")).toEqual(["r3-cccccc (current) · 0 answers", "r2-bbbbbb · 4 answers"]);
    expect(screen.getByRole("combobox", { name: "Scoring version" })).toHaveValue("r3-cccccc");
  });

  it("splits by how old the permit was when it was tagged", () => {
    render(<OutcomeReportFilters markets={markets} marketId="m2" report={report({ permitAge: "OLDER" })} />);
    expect(options("Permit age")).toEqual([
      "All permits", "Recent: 30 days old or less when tagged", "Older: more than 30 days old when tagged",
    ]);
    expect(screen.getByRole("combobox", { name: "Permit age" })).toHaveValue("OLDER");
    expect(screen.getByRole("combobox", { name: "Market" })).toHaveValue("m2");
  });
});

describe("reportFilters", () => {
  it("keeps a known market, a well-formed version and a known permit age", () => {
    expect(reportFilters({ marketId: "m1", version: "r12-0a1b2c", permitAge: "RECENT" }, markets))
      .toEqual({ marketId: "m1", version: "r12-0a1b2c", permitAge: "RECENT" });
    expect(reportFilters({ version: "unversioned" }, markets)).toEqual({ version: "unversioned" });
  });

  it("drops anything a hand-edited URL could send that the API would not know", () => {
    expect(reportFilters({ marketId: "nope", version: "r1-ZZZZZZ", permitAge: "recent" }, markets)).toEqual({});
    expect(reportFilters({ marketId: ["m1", "m2"], version: ["r1-aaaaaa"] }, markets)).toEqual({});
  });
});
