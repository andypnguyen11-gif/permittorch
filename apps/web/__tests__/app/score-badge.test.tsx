// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ScoreBadge } from "@/components/app/score-badge";

describe("ScoreBadge", () => {
  it("renders the score and a Hot label with flame for >=90", () => {
    render(<ScoreBadge score={94} showLabel />);
    const badge = screen.getByTestId("score-badge");
    expect(badge).toHaveTextContent("94");
    expect(screen.getByText("Hot")).toBeInTheDocument();
    expect(badge.dataset.band).toBe("hot");
    expect(badge.className).toContain("bg-orange-500");
  });

  it("renders strong band for 80-89 without flame label", () => {
    render(<ScoreBadge score={86} showLabel />);
    const badge = screen.getByTestId("score-badge");
    expect(badge.dataset.band).toBe("strong");
    expect(screen.queryByText("Hot")).not.toBeInTheDocument();
  });

  it("renders medium and muted bands", () => {
    const { rerender } = render(<ScoreBadge score={72} />);
    expect(screen.getByTestId("score-badge").dataset.band).toBe("medium");
    rerender(<ScoreBadge score={41} />);
    expect(screen.getByTestId("score-badge").dataset.band).toBe("muted");
  });

  it("exposes the score to screen readers as text, not an aria-label on a span", () => {
    render(<ScoreBadge score={72} showLabel />);
    const badge = screen.getByTestId("score-badge");
    expect(badge).not.toHaveAttribute("aria-label");
    expect(badge).toHaveAttribute("aria-hidden", "true");
    const srText = screen.getByText("Lead score 72 of 100 (High)");
    expect(srText).toHaveClass("sr-only");
  });
});
