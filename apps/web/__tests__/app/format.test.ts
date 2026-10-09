import { describe, expect, it } from "vitest";
import {
  CONTRACTOR_NOT_PUBLISHED, contractorDisplay, dataThroughLabel,
  formatDate, formatRelative, formatValueShort, scoreBand,
  emailHref, humanizeMachineString, permitStatusDisplay, phoneHref, recordKind, recordLink, repeatsTitle,
} from "@/components/app/format";

describe("scoreBand", () => {
  it("bands scores per mockup: >=90 hot, 80-89 strong, 70-79 medium, else muted", () => {
    expect(scoreBand(94)).toBe("hot");
    expect(scoreBand(90)).toBe("hot");
    expect(scoreBand(89)).toBe("strong");
    expect(scoreBand(80)).toBe("strong");
    expect(scoreBand(79)).toBe("medium");
    expect(scoreBand(70)).toBe("medium");
    expect(scoreBand(69)).toBe("muted");
    expect(scoreBand(41)).toBe("muted");
  });
});

describe("formatRelative", () => {
  const now = Date.parse("2026-08-19T12:00:00Z");
  it("renders minutes, hours, days, and null", () => {
    expect(formatRelative("2026-08-19T11:59:40Z", now)).toBe("just now");
    expect(formatRelative("2026-08-19T11:42:00Z", now)).toBe("18 minutes ago");
    expect(formatRelative("2026-08-19T09:00:00Z", now)).toBe("3 hours ago");
    expect(formatRelative("2026-08-17T12:00:00Z", now)).toBe("2 days ago");
    expect(formatRelative("2026-08-19T11:59:00Z", now)).toBe("1 minute ago");
    expect(formatRelative(null, now)).toBe("—");
  });
});

describe("formatValueShort", () => {
  it("abbreviates currency like the mockup", () => {
    expect(formatValueShort(1850000)).toBe("$1.85M");
    expect(formatValueShort(24600000)).toBe("$24.6M");
    expect(formatValueShort(620000)).toBe("$620K");
    expect(formatValueShort(150000)).toBe("$150K");
    expect(formatValueShort(15000)).toBe("$15K");
    expect(formatValueShort(950)).toBe("$950");
    expect(formatValueShort(null)).toBe("—");
  });
});

describe("formatDate", () => {
  it("renders a medium date or em dash", () => {
    expect(formatDate("2025-05-13T10:00:00Z")).toBe("May 13, 2025");
    expect(formatDate(null)).toBe("—");
  });
});

describe("humanizeMachineString", () => {
  it("converts snake_case to a capitalized phrase", () => {
    expect(humanizeMachineString("new_installation")).toBe("New installation");
    expect(humanizeMachineString("multifamily_residential")).toBe("Multifamily residential");
  });

  it("capitalizes a single word", () => {
    expect(humanizeMachineString("inspection")).toBe("Inspection");
  });

  it("treats unknown (any case) as absent", () => {
    expect(humanizeMachineString("unknown")).toBeNull();
    expect(humanizeMachineString("UNKNOWN")).toBeNull();
    expect(humanizeMachineString("Unknown")).toBeNull();
  });

  it("treats null and empty string as absent", () => {
    expect(humanizeMachineString(null)).toBeNull();
    expect(humanizeMachineString("")).toBeNull();
    expect(humanizeMachineString("   ")).toBeNull();
  });
});

describe("permitStatusDisplay", () => {
  it("appends the raw status when it differs from the label", () => {
    expect(permitStatusDisplay("Failed", "Open/Follow-Up Needed")).toBe("Failed · Open/Follow-Up Needed");
  });

  it("omits the raw status when it matches the label case-insensitively", () => {
    expect(permitStatusDisplay("Failed", "failed")).toBe("Failed");
    expect(permitStatusDisplay("Closed", "CLOSED")).toBe("Closed");
  });

  it("omits the raw status when it is null or empty", () => {
    expect(permitStatusDisplay("Active", null)).toBe("Active");
    expect(permitStatusDisplay("Active", "")).toBe("Active");
  });
});

describe("recordKind", () => {
  it("recognizes inspection and violation case-insensitively and whitespace-tolerantly", () => {
    expect(recordKind("inspection")).toBe("inspection");
    expect(recordKind("Inspection")).toBe("inspection");
    expect(recordKind(" INSPECTION ")).toBe("inspection");
    expect(recordKind("violation")).toBe("violation");
    expect(recordKind("Violation")).toBe("violation");
    expect(recordKind(" VIOLATION ")).toBe("violation");
  });

  it("defaults to permit for null, empty, or any other value", () => {
    expect(recordKind(null)).toBe("permit");
    expect(recordKind("")).toBe("permit");
    expect(recordKind("permit")).toBe("permit");
    expect(recordKind("standpipe")).toBe("permit");
  });
});

describe("repeatsTitle", () => {
  it("is true when the value only repeats the title, ignoring case and spacing", () => {
    expect(repeatsTitle("Inspection", "Inspection")).toBe(true);
    expect(repeatsTitle(" inspection ", "Inspection")).toBe(true);
  });

  it("is false for a different or missing value", () => {
    expect(repeatsTitle("Fire sprinkler", "Violation")).toBe(false);
    expect(repeatsTitle("Fire code violation", "Violation")).toBe(false);
    expect(repeatsTitle(null, "Inspection")).toBe(false);
  });
});

describe("recordLink", () => {
  const dataset = "https://data.sf.gov/Public-Safety/Fire-Permits/893e-xam6";
  const record = "https://data.sf.gov/resource/893e-xam6.json?permit_number=2026-0042";

  it("calls the city's own page for the record the original record", () => {
    expect(recordLink({ url: dataset, recordUrl: "https://sf.gov/permits/2026-0042", recordUrlKind: "PAGE" }))
      .toEqual({ href: "https://sf.gov/permits/2026-0042", label: "View original record" });
  });

  it("calls a raw data row source data, never the original record", () => {
    for (const kind of ["REST", "DATA"] as const) {
      expect(recordLink({ url: dataset, recordUrl: record, recordUrlKind: kind }))
        .toEqual({ href: record, label: "View source data for this record" });
    }
  });

  it("treats a missing or unrecognized kind as raw data", () => {
    expect(recordLink({ url: dataset, recordUrl: record, recordUrlKind: null })?.label)
      .toBe("View source data for this record");
    expect(recordLink({ url: dataset, recordUrl: record, recordUrlKind: "PORTAL" as never })?.label)
      .toBe("View source data for this record");
  });

  it("links to the dataset, and says so, when the record has no link of its own", () => {
    expect(recordLink({ url: dataset, recordUrl: null, recordUrlKind: null }))
      .toEqual({ href: dataset, label: "View source dataset" });
    // A kind without a link means nothing.
    expect(recordLink({ url: dataset, recordUrl: null, recordUrlKind: "PAGE" }))
      .toEqual({ href: dataset, label: "View source dataset" });
    expect(recordLink({ url: dataset, recordUrl: "  ", recordUrlKind: "PAGE" }))
      .toEqual({ href: dataset, label: "View source dataset" });
  });

  it("still works against an API response from before record links existed", () => {
    expect(recordLink({ url: dataset } as never))
      .toEqual({ href: dataset, label: "View source dataset" });
  });

  it("never links to anything but a web address", () => {
    expect(recordLink({ url: dataset, recordUrl: "javascript:alert(1)", recordUrlKind: "PAGE" }))
      .toEqual({ href: dataset, label: "View source dataset" });
    expect(recordLink({ url: "javascript:alert(1)", recordUrl: null, recordUrlKind: null })).toBeNull();
    expect(recordLink({ url: "", recordUrl: null, recordUrlKind: null })).toBeNull();
  });
});

// Made-up values throughout: 555-01xx numbers and example.com addresses.
describe("phoneHref", () => {
  it("dials one full US number, however the record punctuates it", () => {
    for (const phone of ["7135550142", "(713) 555-0142", "713-555-0142", "713.555.0142", "+1 713 555 0142", "1-713-555-0142"]) {
      expect(phoneHref(phone), phone).toBe("tel:+17135550142");
    }
  });

  it("does not guess a number to dial when the value holds more than one", () => {
    expect(phoneHref("713-555-0142 x12")).toBeNull();
    expect(phoneHref("713-555-0142 / 713-555-0199")).toBeNull();
    expect(phoneHref("713-555-0142 ext 4")).toBeNull();
    expect(phoneHref("+44 20 7946 0958")).toBeNull();
  });

  it("is null for what is not a phone", () => {
    expect(phoneHref("555-0142")).toBeNull();
    expect(phoneHref("N/A")).toBeNull();
    expect(phoneHref("javascript:alert(1)")).toBeNull();
    expect(phoneHref("")).toBeNull();
    expect(phoneHref(null)).toBeNull();
  });
});

describe("emailHref", () => {
  it("writes to one plain address", () => {
    expect(emailHref("office@example.com")).toBe("mailto:office@example.com");
    expect(emailHref(" First.Last+permits@sub.example.com ")).toBe("mailto:First.Last+permits@sub.example.com");
  });

  it("is null for anything that could carry more than one address", () => {
    expect(emailHref("a@example.com; b@example.com")).toBeNull();
    expect(emailHref("a@example.com,b@example.com")).toBeNull();
    expect(emailHref("office@example.com?subject=x&bcc=other@example.com")).toBeNull();
    expect(emailHref("office@example.com%0Abcc:other@example.com")).toBeNull();
  });

  it("is null for what is not an email", () => {
    expect(emailHref("office at example.com")).toBeNull();
    expect(emailHref("office@example")).toBeNull();
    expect(emailHref("javascript:alert(1)")).toBeNull();
    expect(emailHref("")).toBeNull();
    expect(emailHref(null)).toBeNull();
  });
});

describe("contractorDisplay", () => {
  it("shows the record's contractor when it names one", () => {
    expect(contractorDisplay("Summit Builders", "NOT_PUBLISHED")).toBe("Summit Builders");
  });
  it("says the source does not publish it, never that none is listed", () => {
    expect(contractorDisplay(null, "NOT_PUBLISHED")).toBe(CONTRACTOR_NOT_PUBLISHED);
    expect(CONTRACTOR_NOT_PUBLISHED).toBe("Not published by this source");
  });
  it("leaves every other empty contractor empty (rendered as a dash)", () => {
    expect(contractorDisplay(null, "NO_CONTRACTOR_LISTED")).toBeNull();
    expect(contractorDisplay(null, null)).toBeNull();
  });
});

describe("dataThroughLabel", () => {
  it("names the newest permit date and the cadence", () => {
    expect(dataThroughLabel("2026-08-07T00:00:00Z")).toBe("Data through Aug 7, 2026, published monthly");
  });
});
