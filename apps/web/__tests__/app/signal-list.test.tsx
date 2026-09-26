// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SignalList } from "@/components/app/lead-detail/signal-list";

const signals = [
  { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial construction", weight: 25 },
  { signalType: "FIRE_SPRINKLER_SCOPE", description: "Sprinkler scope detected", weight: 25 },
  { signalType: "PERMIT_RECENT", description: "Filed within 72 hours", weight: 15 },
  { signalType: "OLD_PERMIT", description: "Permit aging beyond 30 days", weight: -20 },
];

describe("SignalList (score explanation, PRD §16)", () => {
  it("renders the headline with the score", () => {
    render(<SignalList score={91} signals={signals} />);
    expect(screen.getByText("Why this is a 91")).toBeInTheDocument();
  });

  it("renders each signal with an explicit sign", () => {
    render(<SignalList score={91} signals={signals} />);
    expect(screen.getByText("New commercial construction")).toBeInTheDocument();
    const weights = screen.getAllByTestId("signal-weight");
    expect(weights.map((w) => w.textContent)).toEqual(["+25", "+25", "+15", "−20"]);
  });

  it("colors positive weights green and negative weights red", () => {
    render(<SignalList score={91} signals={signals} />);
    const weights = screen.getAllByTestId("signal-weight");
    expect(weights[0].className).toContain("text-green-600");
    expect(weights[3].className).toContain("text-red-600");
  });
});

describe("SignalList totals", () => {
  it("shows a total row that equals the sum of the signals", () => {
    render(<SignalList score={45} signals={signals} />);
    expect(screen.getByTestId("signal-total")).toHaveTextContent("45");
  });

  it("explains when no signals were recorded", () => {
    render(<SignalList score={0} signals={[]} />);
    expect(screen.getByText(/No scoring signals were recorded/)).toBeInTheDocument();
  });
});
