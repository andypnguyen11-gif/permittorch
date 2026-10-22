// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import "./dom-cleanup";
import { render, screen, within } from "@testing-library/react";
import type { LeadOutcomeReport, OutcomeBand } from "@permittorch/types";
import { OutcomeReportTable } from "@/components/app/admin/outcome-report-table";

const band = (low: number, o: Partial<OutcomeBand> = {}): OutcomeBand => ({
  low, high: low === 90 ? 100 : low + 9, worthContacting: 0, alreadyAwarded: 0, wrongTrade: 0, tooLate: 0,
  total: 0, worthContactingShare: null, ...o,
});

const report: LeadOutcomeReport = {
  minAnswersPerBand: 10, version: "r1-3fa9c2", currentVersion: "r1-3fa9c2", permitAge: "ALL",
  versions: [{ version: "r1-3fa9c2", answers: 12 }],
  bands: [
    band(90, { worthContacting: 4, alreadyAwarded: 3, wrongTrade: 2, tooLate: 1, total: 10, worthContactingShare: 0.4 }),
    band(80, { worthContacting: 9, total: 9 }),
    band(70), band(60), band(50), band(40), band(30), band(20), band(10), band(0),
  ],
};

const row = (name: string) => screen.getByRole("row", { name: new RegExp(`^${name}`) });

describe("OutcomeReportTable", () => {
  it("lists ten bands, top first", () => {
    render(<OutcomeReportTable report={report} />);
    const rows = screen.getAllByRole("row").slice(1);
    expect(rows).toHaveLength(10);
    expect(within(rows[0]).getByText("90–100")).toBeInTheDocument();
    expect(within(rows[9]).getByText("0–9")).toBeInTheDocument();
  });

  it("shows each tag's count and the worth-contacting share as a whole percent", () => {
    render(<OutcomeReportTable report={report} />);
    const top = row("90–100");
    expect(within(top).getAllByRole("cell").map((c) => c.textContent)).toEqual(
      ["90–100", "Hot", "4", "3", "2", "1", "10", "40%"]);
  });

  it("shows counts but no share below the minimum number of answers", () => {
    render(<OutcomeReportTable report={report} />);
    const eighties = row("80–89");
    expect(within(eighties).getByText("9", { selector: "td:nth-child(7)" })).toBeInTheDocument();
    expect(within(eighties).getByText("Too few answers to read")).toBeInTheDocument();
  });

  it("shows a dash for a band with no answers", () => {
    render(<OutcomeReportTable report={report} />);
    const cells = within(row("70–79")).getAllByRole("cell");
    expect(cells[7]).toHaveTextContent("—");
  });

  it("names each band the way contractors see it", () => {
    render(<OutcomeReportTable report={report} />);
    expect(within(row("80–89")).getByText("Very High")).toBeInTheDocument();
    expect(within(row("70–79")).getByText("High")).toBeInTheDocument();
    expect(within(row("60–69")).getByText("Monitor")).toBeInTheDocument();
  });
});
