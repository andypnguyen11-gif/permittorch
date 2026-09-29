// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render } from "@testing-library/react";
import PrivacyPage from "@/app/(marketing)/privacy/page";
import { TERMS_VERSION } from "@/lib/terms";

afterEach(() => cleanup());

describe("privacy page", () => {
  it("describes both supported sign-in methods", () => {
    const { container } = render(<PrivacyPage />);
    expect(container.textContent).toMatch(/Firebase Authentication with email\/password or Google/);
  });

  it("speaks to the people named in permit records", () => {
    const { container } = render(<PrivacyPage />);
    const text = container.textContent ?? "";
    expect(text).toMatch(/We take this information only from the government record/);
    expect(text).toMatch(/never on a public page/);
    expect(text).toMatch(/We do not call, text or email the people named in permit records/);
  });

  it("says how a person asks to be removed, and by when we act", () => {
    const { container } = render(<PrivacyPage />);
    const text = container.textContent ?? "";
    expect(text).toMatch(/You can ask us to remove your phone number, your email address, your name, or a whole record/);
    expect(text).toMatch(/Email support@permittorch\.com/);
    expect(text).toMatch(/within 30 days/);
  });

  it("says that a removed value is kept on a list so that it stays removed", () => {
    const { container } = render(<PrivacyPage />);
    const text = container.textContent ?? "";
    expect(text).toMatch(/we keep what you asked us to remove on a private list that our daily import checks/);
    expect(text).toMatch(/Customers never see that list/);
  });

  it("does not say that permit records are not sold", () => {
    const { container } = render(<PrivacyPage />);
    const text = container.textContent ?? "";
    expect(text).not.toMatch(/We do not sell your personal information/);
    expect(text).toMatch(/access to them is what our customers pay for/);
  });

  it("shows the version a customer agrees to", () => {
    const { container } = render(<PrivacyPage />);
    expect(container.textContent).toContain(`Version ${TERMS_VERSION}.`);
  });
});
