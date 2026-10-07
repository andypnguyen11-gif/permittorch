// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ContractorStatusBadge, FIRE_CONTRACTOR_BADGE_TEXT } from "@/components/app/contractor-status-badge";

describe("ContractorStatusBadge", () => {
  it("says a fire contractor is on the permit when the record names one", () => {
    render(<ContractorStatusBadge status="FIRE_CONTRACTOR_NAMED" />);
    expect(screen.getByText("Fire contractor on permit")).toBeInTheDocument();
    expect(FIRE_CONTRACTOR_BADGE_TEXT).toBe("Fire contractor on permit");
  });

  it("renders nothing for every other status and for an unassessed lead", () => {
    for (const status of ["NOT_APPLICABLE", "NO_CONTRACTOR_LISTED", "OTHER_CONTRACTOR_NAMED", null] as const) {
      const { container, unmount } = render(<ContractorStatusBadge status={status} />);
      expect(container, String(status)).toBeEmptyDOMElement();
      unmount();
    }
  });

  it("never words the record as awarded, unassigned, or won", () => {
    render(<ContractorStatusBadge status="FIRE_CONTRACTOR_NAMED" />);
    expect(document.body.textContent).not.toMatch(/awarded|unassigned|won/i);
  });
});
