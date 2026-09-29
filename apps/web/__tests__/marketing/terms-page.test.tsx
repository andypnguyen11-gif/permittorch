// @vitest-environment jsdom
import { readFileSync } from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render } from "@testing-library/react";
import TermsPage from "@/app/(marketing)/terms/page";
import { TERMS_UPDATED, TERMS_VERSION } from "@/lib/terms";

afterEach(() => cleanup());

const page = () => {
  const { container } = render(<TermsPage />);
  return { container, text: container.textContent ?? "" };
};

describe("terms page", () => {
  it("has one h1 and numbers its sections in order", () => {
    const { container } = page();
    expect(container.querySelectorAll("h1")).toHaveLength(1);
    const headings = [...container.querySelectorAll("h2")].map((h) => h.textContent ?? "");
    expect(headings.length).toBeGreaterThan(0);
    headings.forEach((heading, i) => expect(heading.startsWith(`${i + 1}. `), heading).toBe(true));
  });

  it("keeps the sections the other sections point to where they say", () => {
    const { container } = page();
    const headings = [...container.querySelectorAll("h2")].map((h) => h.textContent);
    expect(headings).toEqual(expect.arrayContaining([
      "5. Contacting people named in permit records",
      "6. Uses that are not allowed",
      "8. Complaints, suspension and closing an account",
      "9. Claims that arise from your use",
      "10. No warranty",
      "11. Limitation of liability",
    ]));
  });

  it("says that a lead is no consent and that the customer answers for the contact", () => {
    const { text } = page();
    expect(text).toMatch(/you alone are responsible for doing it lawfully/);
    expect(text).toMatch(/A lead gives you no permission to contact anyone/);
    expect(text).toMatch(/Being named in a public record is not consent/);
  });

  it("names the calling and email laws and the duties under them", () => {
    const { text } = page();
    for (const law of ["Telephone Consumer Protection Act", "Telemarketing Sales Rule",
      "National Do Not Call Registry", "CAN-SPAM Act"]) expect(text).toContain(law);
    expect(text).toMatch(/Not use an automatic dialing system, a prerecorded or artificial voice/);
    expect(text).toMatch(/When a person asks you to stop, stop/);
    expect(text).toMatch(/Honor an opt-out within 10 business days/);
  });

  it("says what we do not check about a phone number or email address", () => {
    expect(page().text).toMatch(/We do not check whether a phone number or an email address is current/);
  });

  it("rules out credit, insurance, employment and housing decisions", () => {
    const { text } = page();
    expect(text).toMatch(/not a consumer reporting agency/);
    expect(text).toMatch(/credit, insurance, employment or housing/);
  });

  it("keeps the ban on sharing and reselling exported data", () => {
    expect(page().text).toMatch(/Do not share exported data outside your organization, resell it, or republish it/);
  });

  it("lets us suspend an account and makes the customer answer for claims", () => {
    const { text } = page();
    expect(text).toMatch(/suspend your account, or close it/);
    expect(text).toMatch(/indemnify, defend and hold harmless PermitTorch/);
  });

  it("names the law and the courts that apply", () => {
    const { text } = page();
    expect(text).toMatch(/governed by the laws of the State of Texas/);
    expect(text).toMatch(/state or federal courts located in Harris County, Texas/);
  });

  it("sets the warranty and liability paragraphs apart from the rest", () => {
    const { container } = page();
    const notices = [...container.querySelectorAll("p.font-semibold")].map((p) => p.textContent ?? "");
    expect(notices).toHaveLength(2);
    expect(notices[0]).toMatch(/PROVIDED “AS IS”/);
    expect(notices[1]).toMatch(/LIMITED TO THE AMOUNT YOU PAID US IN THE TWELVE MONTHS/);
  });

  it("shows the version and date a customer agrees to", () => {
    expect(page().text).toContain(`Last updated ${TERMS_UPDATED}. Version ${TERMS_VERSION}.`);
  });
});

// The API refuses an agreement to any version but its own, so the two must be the same.
describe("TERMS_VERSION ↔ API Terms.CurrentVersion", () => {
  it("is the version the API holds as current", () => {
    const api = readFileSync(path.resolve(__dirname, "../../../api/Features/Account/Terms.cs"), "utf8");
    const match = api.match(/public const string CurrentVersion = "([^"]+)";/);
    expect(match).not.toBeNull();
    expect(TERMS_VERSION).toBe(match![1]);
  });

  it("starts with the date shown as last updated", () => {
    expect(TERMS_VERSION).toMatch(/^\d{4}-\d{2}-\d{2}(\.\d+)?$/);
    expect(new Date(`${TERMS_VERSION.slice(0, 10)}T12:00:00Z`).toLocaleDateString("en-US", {
      year: "numeric", month: "long", day: "numeric", timeZone: "UTC",
    })).toBe(TERMS_UPDATED);
  });
});
