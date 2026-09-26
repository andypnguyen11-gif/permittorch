// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render } from "@testing-library/react";
import PrivacyPage from "@/app/(marketing)/privacy/page";

afterEach(() => cleanup());

describe("privacy page", () => {
  it("describes both supported sign-in methods", () => {
    const { container } = render(<PrivacyPage />);
    expect(container.textContent).toMatch(/Firebase Authentication with email\/password or Google/);
  });
});
