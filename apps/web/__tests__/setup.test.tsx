import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

function Probe() {
  return <h1>PermitTorch</h1>;
}

describe("test harness", () => {
  it("renders a React component into jsdom", () => {
    render(<Probe />);
    expect(screen.getByRole("heading", { name: "PermitTorch" })).toBeInTheDocument();
  });
});
