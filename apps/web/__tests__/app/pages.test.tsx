// @vitest-environment jsdom
// Server-component page smoke tests against the mock API (lib/api.ts → fixtures).
import "./dom-cleanup";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => "/app",
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
  notFound: () => { throw new Error("NOT_FOUND"); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import SavedPage from "@/app/app/saved/page";
import AlertsPage from "@/app/app/alerts/page";
import AccountPage from "@/app/app/account/page";
import { TooltipProvider } from "@/components/ui/tooltip";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

describe("/app/saved", () => {
  it("lists the saved fixture leads with their tracking status", async () => {
    render(await SavedPage());
    expect(screen.getByRole("heading", { name: "Saved leads" })).toBeInTheDocument();
    expect(screen.getByText("Warehouse Fire Sprinkler System")).toBeInTheDocument();
    expect(screen.getByText("Restaurant Kitchen Hood Suppression")).toBeInTheDocument();
    expect(screen.getByText("Mixed-Use Tower Fire Alarm")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Mark contacted" })).toHaveLength(2);
    expect(screen.getAllByRole("button", { name: "Mark saved" })).toHaveLength(1);
  });
});

describe("/app/alerts", () => {
  it("shows the account's digest frequency", async () => {
    render(await AlertsPage());
    expect(screen.getByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /Daily/ })).toBeChecked();
    expect(screen.getByText("john@davisfireprotection.com")).toBeInTheDocument();
  });
});

describe("/app/account", () => {
  it("shows profile, plan, and entitled markets", async () => {
    render(<TooltipProvider>{await AccountPage()}</TooltipProvider>);
    expect(screen.getByText("Davis Fire Protection")).toBeInTheDocument();
    expect(screen.getByText("Pro plan")).toBeInTheDocument();
    expect(screen.getByText("Houston, TX")).toBeInTheDocument();
    expect(screen.getByText("Dallas, TX")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Upgrade to Territory" })).toBeDisabled();
  });
});
