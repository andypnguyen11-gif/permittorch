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
