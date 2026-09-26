// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SignalList } from "@/components/app/lead-detail/signal-list";

const BASE = {
  signalType: "BASE_SCORE",
  description: "Baseline for a classified fire-protection permit",
  weight: 30,
};

// 30 + 25 + 25 + 15 − 20 = 75
const signals = [
  BASE,
  { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial construction", weight: 25 },
  { signalType: "FIRE_SPRINKLER_SCOPE", description: "Explicit fire sprinkler scope", weight: 25 },
  { signalType: "PERMIT_RECENT", description: "Filed within the last 72 hours", weight: 15 },
  { signalType: "OLD_PERMIT", description: "Permit older than 90 days", weight: -20 },
];

describe("SignalList (score explanation, PRD §16)", () => {
  it("renders the headline with the score", () => {
    render(<SignalList score={75} signals={signals} />);
    expect(screen.getByText("Why this is a 75")).toBeInTheDocument();
  });

  it("renders BASE_SCORE as the first row, like any other signal", () => {
    const { container } = render(<SignalList score={75} signals={signals} />);
    const rows = container.querySelectorAll("li[data-signal-type]");
    expect(rows[0].getAttribute("data-signal-type")).toBe("BASE_SCORE");
    expect(rows[0]).toHaveTextContent("Baseline for a classified fire-protection permit");
    expect(rows[0]).toHaveTextContent("+30");
  });

  it("renders each signal with an explicit sign", () => {
    render(<SignalList score={75} signals={signals} />);
    expect(screen.getByText("New commercial construction")).toBeInTheDocument();
    const weights = screen.getAllByTestId("signal-weight");
    expect(weights.map((w) => w.textContent)).toEqual(["+30", "+25", "+25", "+15", "−20"]);
  });

  it("colors positive weights green and negative weights red", () => {
    render(<SignalList score={75} signals={signals} />);
    const weights = screen.getAllByTestId("signal-weight");
    expect(weights[0].className).toContain("text-green-600");
    expect(weights[4].className).toContain("text-red-600");
  });
});

describe("SignalList totals", () => {
  it("shows the canonical score as the total, with no clamp note when unclamped", () => {
    render(<SignalList score={75} signals={signals} />);
    expect(screen.getByTestId("signal-total")).toHaveTextContent("75");
    expect(screen.queryByTestId("signal-clamp-note")).toBeNull();
  });

  it("shows total 100 and a cap note when the signals sum past 100", () => {
    // 30 + 25 + 25 + 15 + 10 + 10 + 10 = 125
    const capped = [
      BASE,
      { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial construction", weight: 25 },
      { signalType: "FIRE_SPRINKLER_SCOPE", description: "Explicit fire sprinkler scope", weight: 25 },
      { signalType: "PERMIT_RECENT", description: "Filed within the last 72 hours", weight: 15 },
      { signalType: "HIGH_PROJECT_VALUE", description: "Project value above $500K", weight: 10 },
      { signalType: "LARGE_SQUARE_FOOTAGE", description: "Large square footage (over 20,000 sqft)", weight: 10 },
      { signalType: "NO_CONTRACTOR_LISTED", description: "No contractor listed yet", weight: 10 },
    ];
    render(<SignalList score={100} signals={capped} />);
    expect(screen.getByTestId("signal-total")).toHaveTextContent("100");
    expect(screen.getByTestId("signal-clamp-note")).toHaveTextContent("capped at 100");
  });

  it("shows a floor note when the signals sum below zero", () => {
    const floored = [
      BASE,
      { signalType: "OLD_PERMIT", description: "Permit older than 90 days", weight: -20 },
      { signalType: "CLOSED_PERMIT", description: "Permit is closed", weight: -30 },
    ];
    render(<SignalList score={0} signals={floored} />);
    expect(screen.getByTestId("signal-total")).toHaveTextContent("0");
    expect(screen.getByTestId("signal-clamp-note")).toHaveTextContent("floored at 0");
  });

  it("displays the API score even if it differs from the signal sum", () => {
    render(<SignalList score={80} signals={signals} />);
    expect(screen.getByTestId("signal-total")).toHaveTextContent("80");
  });

  it("explains when no signals were recorded", () => {
    render(<SignalList score={0} signals={[]} />);
    expect(screen.getByText(/No scoring signals were recorded/)).toBeInTheDocument();
  });
});
