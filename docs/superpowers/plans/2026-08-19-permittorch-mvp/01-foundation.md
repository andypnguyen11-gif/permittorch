# PermitTorch WS0 — Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the complete monorepo foundation on `main` — workspace scaffold, shared types, web app shell (layout/middleware/API client), .NET API skeleton with the full locked database schema and migration, all dependencies for every later workstream, and CI — so that four parallel worktrees (WS1–WS4) can branch without ever touching `package.json`, `.csproj`, `Program.cs`, `middleware.ts`, or `Data/`.

**Architecture:** A pnpm-workspace monorepo: `apps/web` (Next.js 15+ App Router, marketing + dashboard surfaces), `apps/api` (ASP.NET Core minimal API `PermitTorch.Api` with EF Core 10 + Npgsql against PostgreSQL), and `packages/types` (shared TS API contract `@permittorch/types`). `Program.cs` ships in final form delegating all future registration to two stub extension methods (`AddPipelineServices` for WS1, `AddFeatureServices`/`MapFeatureEndpoints` for WS2); the web API client `lib/api.ts` ships fully implemented with a fixture-mock branch so WS4 builds against it offline.

**Tech Stack:** .NET 10 (LTS) · ASP.NET Core minimal APIs · EF Core 10 + Npgsql + EFCore.NamingConventions · xUnit + Microsoft.AspNetCore.Mvc.Testing + Testcontainers.PostgreSql · Next.js 15+ (App Router) · TypeScript strict · Tailwind CSS (v4) · shadcn/ui · Clerk (@clerk/nextjs) · Vitest + @testing-library/react · Playwright · Node 22 LTS · pnpm workspaces · GitHub Actions.

**Spec:** `Prd.md` (product scope; esp. §28–33, §77–79), `Architecture.md` (system design), and `docs/superpowers/plans/2026-08-19-permittorch-mvp/00-overview-and-contracts.md` (**the master contracts doc — every name, type, route, and path in it is LOCKED; this plan uses them verbatim**). Engineering rules: `CLAUDE.md`.

## Global Constraints (master doc §2)

- .NET 10 (LTS), ASP.NET Core minimal APIs + EF Core 10 + Npgsql. Test framework: xUnit.
- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Test framework: Vitest + @testing-library/react. E2E: Playwright.
- Node 22 LTS, pnpm workspaces.
- Commit messages: imperative, descriptive, no PR/task references, no Claude co-author trailers (CLAUDE.md).
- No Redis, no Elasticsearch, no microservices, no message queues (Architecture.md §9).
- All timestamps stored UTC (`timestamptz`). All money `numeric` / C# `decimal`.
- Server-side authorization always; market entitlement enforced in API queries.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family) brand accents, sidebar nav, score-badged tables. Mockup data is illustrative only.

**WS0-specific rules:**

- Do NOT move or modify the existing root files: `Prd.md`, `Architecture.md`, `CLAUDE.md`, `Tasks.md`, `UI Mockup.png`, `docs/`. (The repo is already a git repo on `main` with these committed; `docs/` and a small `Architecture.md` edit are currently uncommitted — Task 1 commits them first.)
- Everything WS0 creates is later frozen for someone: after WS0 merges, no other workstream may edit `package.json`, `pnpm-lock.yaml`, any `.csproj`, `Program.cs`, `middleware.ts`, `packages/types/`, `apps/web/lib/api.ts`, or `apps/api/Data/`. Get them right here.
- Prerequisites on the machine: .NET 10 SDK, Node 22, pnpm ≥ 9, Docker (for the Testcontainers migration test).

---

### Task 1: Commit pending docs and scaffold the pnpm workspace root

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/pnpm-workspace.yaml`
- Create: `/Users/andynguyen/Desktop/Permit Torch/package.json`
- Create: `/Users/andynguyen/Desktop/Permit Torch/.gitignore`
- Create: `/Users/andynguyen/Desktop/Permit Torch/global.json`

**Interfaces:**
- Consumes: existing git repo on `main`.
- Produces: workspace root every subsequent task installs into; `pnpm -r typecheck` / `pnpm -r test` scripts CI will call.

**Steps:**

- [ ] Verify tool prerequisites; each must succeed before continuing:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  node --version     # expect v22.x
  pnpm --version     # expect >= 9
  dotnet --list-sdks # expect a 10.0.x entry
  docker info --format '{{.ServerVersion}}'  # expect a version (daemon running)
  ```
- [ ] Commit the pending planning documents so worktrees branched later contain them:
  ```bash
  git add docs Architecture.md
  git commit -m "Add master plan and workstream contracts for MVP build"
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/.gitignore`:
  ```gitignore
  # Node / Next.js
  node_modules/
  .next/
  out/
  dist/
  coverage/
  next-env.d.ts
  *.tsbuildinfo

  # .NET
  bin/
  obj/
  *.user

  # Playwright
  test-results/
  playwright-report/
  blob-report/

  # Env & secrets
  .env
  .env.*
  !.env.example

  # OS / editor
  .DS_Store
  .idea/
  .vscode/
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/pnpm-workspace.yaml`:
  ```yaml
  packages:
    - "apps/*"
    - "packages/*"
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/package.json`:
  ```json
  {
    "name": "permittorch",
    "version": "0.1.0",
    "private": true,
    "engines": {
      "node": ">=22"
    },
    "scripts": {
      "typecheck": "pnpm -r typecheck",
      "test": "pnpm -r test"
    }
  }
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/global.json` (pins the .NET SDK major so CI and worktrees agree):
  ```json
  {
    "sdk": {
      "version": "10.0.100",
      "rollForward": "latestFeature"
    }
  }
  ```
  If `dotnet --list-sdks` showed a 10.0.x version lower than 10.0.100, use that exact version string instead.
- [ ] Verify: `cd "/Users/andynguyen/Desktop/Permit Torch" && pnpm install` exits 0 and creates a root `pnpm-lock.yaml` (empty workspace is fine).
- [ ] Commit:
  ```bash
  git add .gitignore pnpm-workspace.yaml package.json global.json pnpm-lock.yaml
  git commit -m "Scaffold pnpm workspace monorepo root"
  ```

---

### Task 2: Build `@permittorch/types` (TDD: compile contract first)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/packages/types/package.json`
- Create: `/Users/andynguyen/Desktop/Permit Torch/packages/types/tsconfig.json`
- Create: `/Users/andynguyen/Desktop/Permit Torch/packages/types/src/index.ts`
- Test: `/Users/andynguyen/Desktop/Permit Torch/packages/types/src/contract-usage.ts` (compile-time contract exercise)

**Interfaces:**
- Consumes: master doc §7 (LOCKED TypeScript, byte-for-byte).
- Produces: workspace package `@permittorch/types` exporting `FireCategory`, `PermitStatus`, `PlanTier`, `DigestFrequency`, `SavedLeadStatus`, `HealthStatus`, `Paged<T>`, `Freshness`, `LeadSummary`, `LeadsResponse`, `LeadSignal`, `LeadDetail`, `Market`, `MarketStats`, `SavedLeadItem`, `AccountMe`, `AdminSource`, `ScraperRunSummary` — consumed by `apps/web/lib/api.ts` (Task 10), WS3, WS4.

**Steps:**

- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/packages/types/package.json`:
  ```json
  {
    "name": "@permittorch/types",
    "version": "0.1.0",
    "private": true,
    "type": "module",
    "main": "./src/index.ts",
    "types": "./src/index.ts",
    "exports": {
      ".": "./src/index.ts"
    },
    "scripts": {
      "typecheck": "tsc --noEmit"
    },
    "devDependencies": {
      "typescript": "^5.7.0"
    }
  }
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/packages/types/tsconfig.json`:
  ```json
  {
    "compilerOptions": {
      "target": "ES2022",
      "module": "ESNext",
      "moduleResolution": "Bundler",
      "strict": true,
      "noEmit": true,
      "skipLibCheck": true,
      "noUnusedLocals": false
    },
    "include": ["src"]
  }
  ```
- [ ] Run `pnpm install` at the repo root so the `typescript` dev dependency lands.
- [ ] **Write the failing compile test first.** Create `/Users/andynguyen/Desktop/Permit Torch/packages/types/src/contract-usage.ts` — it imports every locked export and assigns literal values, so any drift from §7 breaks `tsc`:
  ```typescript
  // Compile-time contract exercise for the LOCKED types in master doc §7.
  // This file has no runtime behavior; it exists so `tsc --noEmit` fails if
  // any exported name or shape drifts from the contract.
  import type {
    AccountMe, AdminSource, DigestFrequency, FireCategory, Freshness,
    HealthStatus, LeadDetail, LeadSignal, LeadsResponse, LeadSummary,
    Market, MarketStats, Paged, PermitStatus, PlanTier, SavedLeadItem,
    SavedLeadStatus, ScraperRunSummary,
  } from "./index";

  const category: FireCategory = "FIRE_SPRINKLER";
  const status: PermitStatus = "NEW";
  const plan: PlanTier = "PRO";
  const digest: DigestFrequency = "WEEKLY";
  const savedStatus: SavedLeadStatus = "CONTACTED";
  const health: HealthStatus = "HEALTHY";

  const lead: LeadSummary = {
    id: "op_1", score: 91, title: "Fire sprinkler system installation",
    address: "1200 Main St", city: "Houston", state: "TX",
    category, permitType: "Fire Protection", status,
    filedDate: "2026-08-01T00:00:00Z", estimatedValue: 250000,
    reason: "New commercial build with sprinkler scope", isNew: true,
  };

  const signal: LeadSignal = {
    signalType: "NEW_COMMERCIAL_BUILD",
    description: "New commercial construction",
    weight: 25,
  };

  const paged: Paged<LeadSummary> = { items: [lead], total: 1, page: 1, pageSize: 25 };
  const freshness: Freshness = { lastUpdatedAt: null };
  const leads: LeadsResponse = { ...paged, freshness };

  const detail: LeadDetail = {
    ...lead,
    confidence: 0.92,
    firstDetectedAt: "2026-08-18T12:00:00Z",
    lastUpdatedAt: "2026-08-19T12:00:00Z",
    permit: {
      permitNumber: "FP-2026-001", description: "Install sprinkler system",
      zip: "77002", issuedDate: null, squareFootage: 12000,
      ownerName: "Acme Holdings", contractorName: null,
    },
    participants: [{ role: "Owner", name: "Acme Holdings" }],
    signals: [signal],
    source: { name: "Houston", url: "https://example.com", lastCheckedAt: null },
  };

  const market: Market = { id: "m_1", name: "Houston", city: "Houston", state: "TX", slug: "houston-tx" };

  const stats: MarketStats = {
    slug: "houston-tx",
    totalLast30Days: 128,
    byCategory: {
      FIRE_SPRINKLER: 40, FIRE_ALARM: 30, FIRE_SUPPRESSION: 10,
      KITCHEN_SUPPRESSION: 8, FIRE_INSPECTION: 20, VIOLATION_CORRECTION: 10,
      GENERAL_FIRE_PROTECTION: 10,
    },
    lastUpdatedAt: null,
  };

  const savedItem: SavedLeadItem = { id: "sl_1", status: savedStatus, createdAt: "2026-08-19T00:00:00Z", lead };

  const me: AccountMe = {
    email: "owner@example.com", role: "ADMIN",
    organizationName: "Acme Fire", plan, digestFrequency: digest,
  };

  const adminSource: AdminSource = {
    id: "src_1", name: "Houston", city: "Houston", state: "TX", active: true,
    healthStatus: health, lastSuccessfulRunAt: null, recordsLastRun: 0,
  };

  const run: ScraperRunSummary = {
    id: "run_1", apifyRunId: "apify_1", status: "SUCCEEDED",
    startedAt: "2026-08-19T00:00:00Z", finishedAt: null,
    recordsImported: 100, duplicatesSkipped: 5, failures: 0, durationSeconds: 42.5,
  };

  export type ContractOk = [
    typeof leads, typeof detail, typeof market, typeof stats,
    typeof savedItem, typeof me, typeof adminSource, typeof run,
  ];
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/packages/types" && pnpm typecheck
  ```
  Expected failure: `error TS2307: Cannot find module './index' or its corresponding type declarations.`
- [ ] **Minimal implementation.** Create `/Users/andynguyen/Desktop/Permit Torch/packages/types/src/index.ts` with the master doc §7 content verbatim:
  ```typescript
  export type FireCategory = "FIRE_SPRINKLER" | "FIRE_ALARM" | "FIRE_SUPPRESSION"
    | "KITCHEN_SUPPRESSION" | "FIRE_INSPECTION" | "VIOLATION_CORRECTION" | "GENERAL_FIRE_PROTECTION";
  export type PermitStatus = "NEW" | "ACTIVE" | "INSPECTION" | "FAILED" | "CLOSED" | "UNKNOWN";
  export type PlanTier = "STARTER" | "PRO" | "TERRITORY";
  export type DigestFrequency = "NONE" | "DAILY" | "WEEKLY";
  export type SavedLeadStatus = "SAVED" | "CONTACTED";
  export type HealthStatus = "HEALTHY" | "WARNING" | "STALE" | "FAILED" | "DISABLED";

  export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }
  export interface Freshness { lastUpdatedAt: string | null; }   // ISO

  export interface LeadSummary {
    id: string; score: number; title: string;
    address: string | null; city: string; state: string;
    category: FireCategory; permitType: string | null; status: PermitStatus;
    filedDate: string | null; estimatedValue: number | null;
    reason: string; isNew: boolean;   // isNew = firstDetectedAt < 72h ago
  }
  export interface LeadsResponse extends Paged<LeadSummary> { freshness: Freshness; }

  export interface LeadSignal { signalType: string; description: string; weight: number; }
  export interface LeadDetail extends LeadSummary {
    confidence: number; firstDetectedAt: string; lastUpdatedAt: string;
    permit: {
      permitNumber: string | null; description: string | null; zip: string | null;
      issuedDate: string | null; squareFootage: number | null;
      ownerName: string | null; contractorName: string | null;
    };
    participants: { role: string; name: string }[];
    signals: LeadSignal[];
    source: { name: string; url: string; lastCheckedAt: string | null };
  }

  export interface Market { id: string; name: string; city: string; state: string; slug: string; }
  export interface MarketStats {
    slug: string; totalLast30Days: number;
    byCategory: Record<FireCategory, number>; lastUpdatedAt: string | null;
  }
  export interface SavedLeadItem { id: string; status: SavedLeadStatus; createdAt: string; lead: LeadSummary; }
  export interface AccountMe {
    email: string; role: "MEMBER" | "ADMIN" | "SUPER_ADMIN";
    organizationName: string; plan: PlanTier | null; digestFrequency: DigestFrequency;
  }
  export interface AdminSource {
    id: string; name: string; city: string; state: string; active: boolean;
    healthStatus: HealthStatus; lastSuccessfulRunAt: string | null; recordsLastRun: number;
  }
  export interface ScraperRunSummary {
    id: string; apifyRunId: string; status: string; startedAt: string; finishedAt: string | null;
    recordsImported: number; duplicatesSkipped: number; failures: number; durationSeconds: number;
  }
  ```
- [ ] Run to verify pass: `cd "/Users/andynguyen/Desktop/Permit Torch/packages/types" && pnpm typecheck` — exits 0, no output errors.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add packages/types pnpm-lock.yaml
  git commit -m "Add shared API contract types package"
  ```

---

### Task 3: Scaffold `apps/web` with create-next-app

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/` (full create-next-app output: `package.json`, `tsconfig.json`, `next.config.ts`, `app/layout.tsx`, `app/page.tsx`, `app/globals.css`, `eslint.config.mjs`, `postcss.config.mjs`)

**Interfaces:**
- Consumes: workspace root (Task 1).
- Produces: Next.js App Router app named `web` (pnpm filter target `web`) with TypeScript strict and Tailwind — the base every web task builds on.

**Steps:**

- [ ] Scaffold (run from the repo root; answer no prompts — all flags supplied):
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  pnpm create next-app@latest apps/web --typescript --tailwind --eslint --app --no-src-dir --import-alias "@/*" --use-pnpm --turbopack
  ```
  Expected outcome: `apps/web/` exists with `package.json` (name `web`), `app/` directory, `tsconfig.json`, Tailwind v4 wired via `postcss.config.mjs` and `app/globals.css`.
- [ ] If create-next-app produced `apps/web/pnpm-lock.yaml` or `apps/web/node_modules` outside the workspace, remove the stray lockfile and reinstall from the root so there is exactly ONE lockfile:
  ```bash
  rm -f "/Users/andynguyen/Desktop/Permit Torch/apps/web/pnpm-lock.yaml"
  cd "/Users/andynguyen/Desktop/Permit Torch" && pnpm install
  ```
- [ ] Verify TypeScript strict mode: open `/Users/andynguyen/Desktop/Permit Torch/apps/web/tsconfig.json` and confirm `"strict": true` under `compilerOptions` (create-next-app default). If absent, add it.
- [ ] Verify the app builds: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm build` — expect `✓ Compiled successfully` and a route table including `/`.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web pnpm-lock.yaml
  git commit -m "Scaffold Next.js app with App Router, TypeScript strict, and Tailwind"
  ```

---

### Task 4: Install ALL web dependencies and wire the types package

**Files:**
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/package.json`
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/next.config.ts`

**Interfaces:**
- Consumes: `@permittorch/types` (Task 2).
- Produces: frozen `apps/web/package.json` — after WS0, no workstream may edit it. Every dependency WS3/WS4/WS5 will need must land here.

**Steps:**

- [ ] Install runtime dependencies:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  pnpm --filter web add @clerk/nextjs posthog-js @sentry/nextjs
  pnpm --filter web add "@permittorch/types@workspace:*"
  ```
  Expected: `apps/web/package.json` gains all four under `dependencies`, with `"@permittorch/types": "workspace:*"`.
- [ ] Install dev/test dependencies:
  ```bash
  pnpm --filter web add -D vitest @vitejs/plugin-react jsdom @testing-library/react @testing-library/jest-dom @playwright/test
  ```
- [ ] Replace `/Users/andynguyen/Desktop/Permit Torch/apps/web/next.config.ts` with:
  ```typescript
  import type { NextConfig } from "next";

  const nextConfig: NextConfig = {
    transpilePackages: ["@permittorch/types"],
  };

  export default nextConfig;
  ```
- [ ] Add scripts to `/Users/andynguyen/Desktop/Permit Torch/apps/web/package.json` — merge into the existing `"scripts"` block (keep the create-next-app `dev`/`build`/`start`/`lint` entries):
  ```json
  "typecheck": "tsc --noEmit",
  "test": "vitest run",
  "test:watch": "vitest"
  ```
- [ ] Verify: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm typecheck` exits 0, and `node -e "console.log(require('./package.json').dependencies['@permittorch/types'])"` prints `workspace:*`.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web/package.json apps/web/next.config.ts pnpm-lock.yaml
  git commit -m "Install web dependencies for all workstreams and wire shared types package"
  ```

---

### Task 5: Initialize shadcn/ui and pre-install the component set

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/components.json`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/lib/utils.ts`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/components/ui/*` (one file per component below)
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/globals.css` (shadcn token blocks; branded in Task 8)
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/package.json` (radix/cva deps added by the CLI — this is the LAST task allowed to touch it besides nothing; it must happen in WS0 because `shadcn add` edits `package.json`)

**Interfaces:**
- Consumes: Tailwind v4 setup from Task 3.
- Produces: `components/ui/` primitives + `cn()` in `lib/utils.ts`, consumed by WS3 (`components/marketing/`) and WS4 (`components/app/`).

**Steps:**

- [ ] Initialize shadcn/ui non-interactively:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/web"
  pnpm dlx shadcn@latest init --yes --base-color neutral
  ```
  Expected outcome: `components.json` created, `lib/utils.ts` created, `app/globals.css` rewritten with shadcn CSS variables, `class-variance-authority`/`clsx`/`tailwind-merge`/`lucide-react`/`tw-animate-css` added to `package.json`.
- [ ] Pre-install every component WS3/WS4 will need (this pulls all radix deps into `package.json` NOW so parallel branches never touch it):
  ```bash
  pnpm dlx shadcn@latest add --yes button card badge table input select checkbox label separator skeleton tabs dialog sheet dropdown-menu tooltip popover avatar switch textarea sonner pagination breadcrumb form accordion radio-group
  ```
  Expected outcome: one `.tsx` file per component under `components/ui/`, plus `react-hook-form`/`zod`/radix packages in `package.json`.
- [ ] Verify: `pnpm typecheck` in `apps/web` exits 0, and `ls components/ui | wc -l` reports at least 22 files.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web pnpm-lock.yaml
  git commit -m "Initialize shadcn/ui and pre-install shared component set"
  ```

---

### Task 6: Configure Vitest + Testing Library

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/vitest.config.mts`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/vitest.setup.ts`
- Test: `/Users/andynguyen/Desktop/Permit Torch/apps/web/__tests__/setup.test.tsx`

**Interfaces:**
- Consumes: dev deps from Task 4.
- Produces: `pnpm --filter web test` harness used by Task 10, WS3 (`__tests__/marketing/`), and WS4 (`__tests__/app/`).

**Steps:**

- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/vitest.config.mts`:
  ```typescript
  import { defineConfig } from "vitest/config";
  import react from "@vitejs/plugin-react";
  import path from "node:path";

  export default defineConfig({
    plugins: [react()],
    test: {
      environment: "jsdom",
      setupFiles: ["./vitest.setup.ts"],
      include: ["__tests__/**/*.test.{ts,tsx}"],
    },
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "."),
      },
    },
  });
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/vitest.setup.ts`:
  ```typescript
  import "@testing-library/jest-dom/vitest";
  ```
- [ ] **Write the harness smoke test.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/__tests__/setup.test.tsx`:
  ```tsx
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
  ```
- [ ] Run to verify pass: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm test` — expect `1 passed`.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web/vitest.config.mts apps/web/vitest.setup.ts apps/web/__tests__
  git commit -m "Configure Vitest with Testing Library and jsdom"
  ```

---

### Task 7: Configure Playwright (config only — specs come in WS5)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/playwright.config.ts`
- Create: `/Users/andynguyen/Desktop/Permit Torch/e2e/.gitkeep`

**Interfaces:**
- Consumes: `@playwright/test` from Task 4.
- Produces: E2E harness pointing at repo-root `e2e/` (WS5-owned). WS5 runs `pnpm --filter web exec playwright test`.

**Steps:**

- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/playwright.config.ts`:
  ```typescript
  import { defineConfig, devices } from "@playwright/test";

  // E2E specs live at the repo root in e2e/ (owned by WS5). This config ships
  // in WS0 so no later workstream edits tooling files.
  export default defineConfig({
    testDir: "../../e2e",
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    reporter: process.env.CI ? "github" : "list",
    use: {
      baseURL: "http://localhost:3000",
      trace: "on-first-retry",
    },
    projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
    webServer: {
      command: "pnpm dev",
      url: "http://localhost:3000",
      reuseExistingServer: !process.env.CI,
    },
  });
  ```
- [ ] Create the empty spec directory placeholder: `mkdir -p "/Users/andynguyen/Desktop/Permit Torch/e2e" && touch "/Users/andynguyen/Desktop/Permit Torch/e2e/.gitkeep"`
- [ ] Verify config parses and finds zero tests (both outcomes below are acceptable — the point is no config error):
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm exec playwright test --list
  ```
  Expected: `Total: 0 tests in 0 files` (or `Error: No tests found` — fine; a config syntax error is NOT fine). Do not install browsers now; WS5 does that.
- [ ] Verify Vitest does not pick up e2e: `pnpm test` in `apps/web` still reports only the smoke test.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web/playwright.config.ts e2e/.gitkeep
  git commit -m "Add Playwright configuration targeting root e2e directory"
  ```

---

### Task 8: Root layout and globals with light theme and orange brand tokens

**Files:**
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/layout.tsx`
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/globals.css`
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/page.tsx`

**Interfaces:**
- Consumes: shadcn token structure from Task 5; `@clerk/nextjs` from Task 4.
- Produces: `<ClerkProvider>`-wrapped root layout and the light/orange token palette (`--primary` = #F97316 family) that WS3 and WS4 style against.

**Steps:**

- [ ] Replace `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/layout.tsx` with:
  ```tsx
  import type { Metadata } from "next";
  import { ClerkProvider } from "@clerk/nextjs";
  import { Inter } from "next/font/google";
  import "./globals.css";

  const inter = Inter({ subsets: ["latin"], variable: "--font-sans" });

  export const metadata: Metadata = {
    title: {
      default: "PermitTorch — Fire Protection Permit Leads",
      template: "%s | PermitTorch",
    },
    description:
      "PermitTorch turns public permit data into scored, explainable sales leads for fire protection contractors.",
  };

  export default function RootLayout({
    children,
  }: Readonly<{ children: React.ReactNode }>) {
    return (
      <ClerkProvider>
        <html lang="en" className={inter.variable}>
          <body className="min-h-screen bg-background font-sans text-foreground antialiased">
            {children}
          </body>
        </html>
      </ClerkProvider>
    );
  }
  ```
- [ ] In `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/globals.css` (as rewritten by `shadcn init` in Task 5): keep the `@import` lines, the `@custom-variant dark` line, and the `@theme inline { ... }` block exactly as generated, then **replace the generated `:root { ... }` block and delete the generated `.dark { ... }` block**, so the app is permanently light-themed with orange brand accents:
  ```css
  /* PermitTorch is light-theme only for MVP (UI Mockup.png). Orange brand
     accents from the #F97316 (Tailwind orange-500) family. */
  :root {
    color-scheme: light;
    --radius: 0.625rem;
    --background: oklch(1 0 0);
    --foreground: oklch(0.147 0.004 49.25);
    --card: oklch(1 0 0);
    --card-foreground: oklch(0.147 0.004 49.25);
    --popover: oklch(1 0 0);
    --popover-foreground: oklch(0.147 0.004 49.25);
    --primary: oklch(0.705 0.213 47.604);            /* #F97316 */
    --primary-foreground: oklch(0.98 0.016 73.684);
    --secondary: oklch(0.97 0.001 106.424);
    --secondary-foreground: oklch(0.216 0.006 56.043);
    --muted: oklch(0.97 0.001 106.424);
    --muted-foreground: oklch(0.553 0.013 58.071);
    --accent: oklch(0.954 0.038 75.164);             /* orange-100 tint */
    --accent-foreground: oklch(0.47 0.157 37.304);   /* orange-800 */
    --destructive: oklch(0.577 0.245 27.325);
    --border: oklch(0.923 0.003 48.717);
    --input: oklch(0.923 0.003 48.717);
    --ring: oklch(0.705 0.213 47.604);
    --chart-1: oklch(0.705 0.213 47.604);
    --chart-2: oklch(0.6 0.118 184.704);
    --chart-3: oklch(0.398 0.07 227.392);
    --chart-4: oklch(0.828 0.189 84.429);
    --chart-5: oklch(0.769 0.188 70.08);
    --sidebar: oklch(0.985 0.001 106.423);
    --sidebar-foreground: oklch(0.147 0.004 49.25);
    --sidebar-primary: oklch(0.705 0.213 47.604);
    --sidebar-primary-foreground: oklch(0.98 0.016 73.684);
    --sidebar-accent: oklch(0.954 0.038 75.164);
    --sidebar-accent-foreground: oklch(0.47 0.157 37.304);
    --sidebar-border: oklch(0.923 0.003 48.717);
    --sidebar-ring: oklch(0.705 0.213 47.604);
  }
  ```
  (If the generated `@theme inline` block references a token not listed above — e.g. extra sidebar tokens — keep its line and add a matching `:root` entry using the closest value above rather than deleting from `@theme inline`.)
- [ ] Replace `/Users/andynguyen/Desktop/Permit Torch/apps/web/app/page.tsx` with a minimal placeholder (WS3 owns the real homepage under `app/(marketing)/` — **WS3 is authorized to `git rm` this placeholder when it creates `app/(marketing)/page.tsx`**, since a root `page.tsx` and a route-group `(marketing)/page.tsx` cannot both resolve `/`; this placeholder just keeps the WS0 scaffold building without Next.js template branding):
  ```tsx
  export default function Home() {
    return (
      <main className="flex min-h-screen items-center justify-center">
        <h1 className="text-3xl font-bold text-primary">PermitTorch</h1>
      </main>
    );
  }
  ```
- [ ] Verify with a build using a Clerk development-format placeholder key (ClerkProvider requires a syntactically valid publishable key at build time; this one decodes to `clerk.example.com$` and never contacts Clerk during static rendering):
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/web"
  NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_Y2xlcmsuZXhhbXBsZS5jb20k pnpm build
  ```
  Expected: `✓ Compiled successfully`.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web/app
  git commit -m "Add Clerk-wrapped root layout with light theme and orange brand tokens"
  ```

---

### Task 9: `middleware.ts` in FINAL form (Clerk public routes)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/middleware.ts`

**Interfaces:**
- Consumes: `@clerk/nextjs/server` (`clerkMiddleware`, `createRouteMatcher`).
- Produces: the frozen auth boundary — public routes exactly as locked: `/`, `/pricing`, `/how-it-works`, `/fire-protection-leads(.*)`, `/fire-sprinkler-leads`, `/fire-alarm-leads`, `/locations(.*)`, `/blog(.*)`, `/login(.*)`, `/signup(.*)`, `/api/(.*)`. Everything else (i.e. `/app/**`) requires a session. NO workstream may edit this file after WS0.

**Steps:**

- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/middleware.ts`:
  ```typescript
  import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

  // FINAL FORM (WS0). Do not edit in any workstream.
  // Public routes are locked in the master contracts doc; everything else
  // (the /app dashboard surface) requires an authenticated Clerk session.
  const isPublicRoute = createRouteMatcher([
    "/",
    "/pricing",
    "/how-it-works",
    "/fire-protection-leads(.*)",
    "/fire-sprinkler-leads",
    "/fire-alarm-leads",
    "/locations(.*)",
    "/blog(.*)",
    "/login(.*)",
    "/signup(.*)",
    "/api/(.*)",
  ]);

  export default clerkMiddleware(async (auth, req) => {
    if (!isPublicRoute(req)) {
      await auth.protect();
    }
  });

  export const config = {
    matcher: [
      // Run on everything except Next.js internals and static assets.
      "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
      // Always run for API routes.
      "/(api|trpc)(.*)",
    ],
  };
  ```
- [ ] Verify: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm typecheck` exits 0, then `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_Y2xlcmsuZXhhbXBsZS5jb20k pnpm build` succeeds and the build output lists `ƒ Middleware`.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web/middleware.ts
  git commit -m "Add final Clerk middleware with locked public route list"
  ```

---

### Task 10: `apps/web/lib/api.ts` — full API client with mock branch (TDD)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/lib/api.ts`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/web/lib/fixtures/index.ts` (typed stub — WS4 replaces the bodies, keeping the signatures)
- Test: `/Users/andynguyen/Desktop/Permit Torch/apps/web/__tests__/lib/api.test.ts`

**Interfaces:**
- Consumes: `@permittorch/types` (Task 2); env vars `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_API_MOCK`.
- Produces (LOCKED signatures from master doc §8, verbatim):
  ```typescript
  export async function getLeads(params: LeadsQuery, token: string): Promise<LeadsResponse>;
  export async function getLead(id: string, token: string): Promise<LeadDetail>;
  export async function getMarkets(): Promise<Market[]>;
  export async function getMarketStats(slug: string): Promise<MarketStats>;
  export async function getSavedLeads(token: string): Promise<SavedLeadItem[]>;
  export async function saveLead(fireOpportunityId: string, token: string): Promise<SavedLeadItem>;
  export async function updateSavedLead(id: string, status: SavedLeadStatus, token: string): Promise<void>;
  export async function unsaveLead(id: string, token: string): Promise<void>;
  export async function getAccountMarkets(token: string): Promise<Market[]>;
  export async function getAccountMe(token: string): Promise<AccountMe>;
  export async function updateEmailPreferences(frequency: DigestFrequency, token: string): Promise<void>;
  export async function submitSampleLeadRequest(input: { name: string; email: string; company: string; marketSlug: string }): Promise<void>;
  export async function createCheckout(plan: PlanTier, token: string): Promise<{ url: string }>;
  export async function createBillingPortal(token: string): Promise<{ url: string }>;
  export async function getAdminSources(token: string): Promise<AdminSource[]>;
  export async function getAdminRuns(params: { sourceId?: string; page?: number }, token: string): Promise<Paged<ScraperRunSummary>>;
  export async function setSourceActive(id: string, active: boolean, token: string): Promise<void>;

  export interface LeadsQuery {
    market?: string; category?: FireCategory; minScore?: number;
    maxAgeDays?: number; status?: PermitStatus; q?: string; page?: number; pageSize?: number;
  }
  ```
  Plus the fixture-module contract WS4 implements in `apps/web/lib/fixtures/index.ts`: same function names, token parameters dropped — EXCEPT `getMarkets`/`getMarketStats`, whose mock branches import `mockMarkets`/`mockMarketStats` directly from WS3-owned `apps/web/lib/fixtures/markets.ts` (so marketing pages work in mock mode before WS4's fixtures exist).

**Steps:**

- [ ] **Write the failing tests first.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/__tests__/lib/api.test.ts`:
  ```typescript
  import { beforeEach, describe, expect, it, vi } from "vitest";

  const fixtureLeads = {
    items: [], total: 0, page: 1, pageSize: 25,
    freshness: { lastUpdatedAt: null },
  };
  const fixtureMarkets = [
    { id: "m_1", name: "Houston", city: "Houston", state: "TX", slug: "houston-tx" },
  ];

  vi.mock("@/lib/fixtures", () => ({
    getLeads: vi.fn(async () => fixtureLeads),
  }));

  vi.mock("@/lib/fixtures/markets", () => ({
    mockMarkets: fixtureMarkets,
    mockMarketStats: {},
  }));

  function jsonResponse(body: unknown, status = 200): Response {
    return new Response(JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    });
  }

  describe("lib/api", () => {
    beforeEach(() => {
      vi.resetModules();
      vi.unstubAllEnvs();
      vi.unstubAllGlobals();
    });

    it("returns fixture data when NEXT_PUBLIC_API_MOCK=1 without calling fetch", async () => {
      vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1");
      const fetchMock = vi.fn();
      vi.stubGlobal("fetch", fetchMock);
      const api = await import("@/lib/api");

      const markets = await api.getMarkets();
      const leads = await api.getLeads({ market: "houston-tx" }, "tok_ignored");

      expect(markets).toEqual(fixtureMarkets);
      expect(leads).toEqual(fixtureLeads);
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it("calls the real API with base URL, query params, and bearer token when mock is off", async () => {
      vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
      vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
      const fetchMock = vi.fn(async () => jsonResponse(fixtureLeads));
      vi.stubGlobal("fetch", fetchMock);
      const api = await import("@/lib/api");

      await api.getLeads({ market: "houston-tx", minScore: 70, page: 2, pageSize: 10 }, "tok_123");

      expect(fetchMock).toHaveBeenCalledTimes(1);
      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toBe("http://api.test/api/leads?market=houston-tx&minScore=70&page=2&pageSize=10");
      expect(new Headers(init.headers).get("Authorization")).toBe("Bearer tok_123");
    });

    it("sends JSON bodies for mutating calls", async () => {
      vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
      vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
      const fetchMock = vi.fn(async () => jsonResponse({ id: "sl_1", status: "SAVED", createdAt: "2026-08-19T00:00:00Z", lead: null }));
      vi.stubGlobal("fetch", fetchMock);
      const api = await import("@/lib/api");

      await api.saveLead("op_1", "tok_123");

      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toBe("http://api.test/api/saved-leads");
      expect(init.method).toBe("POST");
      expect(init.body).toBe(JSON.stringify({ fireOpportunityId: "op_1" }));
      expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
    });

    it("throws the API-provided error message on non-2xx responses", async () => {
      vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
      vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
      vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: "Lead not found" }, 404)));
      const api = await import("@/lib/api");

      await expect(api.getLead("missing", "tok_123")).rejects.toThrow("Lead not found");
    });

    it("resolves void for 204 responses", async () => {
      vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
      vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
      const fetchMock = vi.fn(async () => new Response(null, { status: 204 }));
      vi.stubGlobal("fetch", fetchMock);
      const api = await import("@/lib/api");

      await expect(api.unsaveLead("sl_1", "tok_123")).resolves.toBeUndefined();
      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toBe("http://api.test/api/saved-leads/sl_1");
      expect(init.method).toBe("DELETE");
    });

    it("maps setSourceActive to the enable/disable admin routes", async () => {
      vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
      vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
      const fetchMock = vi.fn(async () => new Response(null, { status: 200 }));
      vi.stubGlobal("fetch", fetchMock);
      const api = await import("@/lib/api");

      await api.setSourceActive("src_1", false, "tok_admin");
      await api.setSourceActive("src_1", true, "tok_admin");

      expect(fetchMock.mock.calls[0][0]).toBe("http://api.test/api/admin/sources/src_1/disable");
      expect(fetchMock.mock.calls[1][0]).toBe("http://api.test/api/admin/sources/src_1/enable");
    });
  });
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm test
  ```
  Expected failure: the `api.test.ts` suite errors with `Failed to resolve import "@/lib/api"` (module does not exist yet). The Task 6 smoke test still passes.
- [ ] **Implementation.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/web/lib/api.ts`:
  ```typescript
  import type {
    AccountMe,
    AdminSource,
    DigestFrequency,
    FireCategory,
    LeadDetail,
    LeadsResponse,
    Market,
    MarketStats,
    Paged,
    PermitStatus,
    PlanTier,
    SavedLeadItem,
    SavedLeadStatus,
    ScraperRunSummary,
  } from "@permittorch/types";

  export interface LeadsQuery {
    market?: string; category?: FireCategory; minScore?: number;
    maxAgeDays?: number; status?: PermitStatus; q?: string; page?: number; pageSize?: number;
  }

  function isMock(): boolean {
    return process.env.NEXT_PUBLIC_API_MOCK === "1";
  }

  // Fixtures are owned by WS4 (apps/web/lib/fixtures/). Imported lazily so the
  // fixture module is only ever loaded when NEXT_PUBLIC_API_MOCK=1 — marketing
  // builds never touch it.
  function fixtures() {
    return import("@/lib/fixtures");
  }

  function buildQuery(params: Record<string, string | number | undefined>): string {
    const search = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== "") search.set(key, String(value));
    }
    const qs = search.toString();
    return qs ? `?${qs}` : "";
  }

  export async function apiFetch<T>(
    path: string,
    init: RequestInit = {},
    token?: string,
  ): Promise<T> {
    const base = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
    const headers = new Headers(init.headers);
    if (token) headers.set("Authorization", `Bearer ${token}`);
    if (init.body !== undefined) headers.set("Content-Type", "application/json");

    const res = await fetch(`${base}${path}`, { ...init, headers });

    if (!res.ok) {
      let message = `API request failed with status ${res.status}`;
      try {
        const body = (await res.json()) as { error?: string };
        if (body.error) message = body.error;
      } catch {
        // Non-JSON error body: keep the generic message.
      }
      throw new Error(message);
    }

    if (res.status === 204) return undefined as T;
    const text = await res.text();
    return (text ? JSON.parse(text) : undefined) as T;
  }

  export async function getLeads(params: LeadsQuery, token: string): Promise<LeadsResponse> {
    if (isMock()) return (await fixtures()).getLeads(params);
    const qs = buildQuery({
      market: params.market,
      category: params.category,
      minScore: params.minScore,
      maxAgeDays: params.maxAgeDays,
      status: params.status,
      q: params.q,
      page: params.page,
      pageSize: params.pageSize,
    });
    return apiFetch<LeadsResponse>(`/api/leads${qs}`, {}, token);
  }

  export async function getLead(id: string, token: string): Promise<LeadDetail> {
    if (isMock()) return (await fixtures()).getLead(id);
    return apiFetch<LeadDetail>(`/api/leads/${id}`, {}, token);
  }

  // Markets bypass the fixtures index: WS3 (marketing) needs mock markets in a
  // worktree where lib/fixtures/index.ts is still the WS0 throwing stub, so
  // these two functions import WS3-owned lib/fixtures/markets.ts directly.
  export async function getMarkets(): Promise<Market[]> {
    if (isMock()) return (await import("@/lib/fixtures/markets")).mockMarkets;
    return apiFetch<Market[]>("/api/markets");
  }

  export async function getMarketStats(slug: string): Promise<MarketStats> {
    if (isMock()) {
      const stats = (await import("@/lib/fixtures/markets")).mockMarketStats[slug];
      if (!stats) throw new Error(`Unknown market: ${slug}`);
      return stats;
    }
    return apiFetch<MarketStats>(`/api/markets/${slug}/stats`);
  }

  export async function getSavedLeads(token: string): Promise<SavedLeadItem[]> {
    if (isMock()) return (await fixtures()).getSavedLeads();
    return apiFetch<SavedLeadItem[]>("/api/saved-leads", {}, token);
  }

  export async function saveLead(fireOpportunityId: string, token: string): Promise<SavedLeadItem> {
    if (isMock()) return (await fixtures()).saveLead(fireOpportunityId);
    return apiFetch<SavedLeadItem>(
      "/api/saved-leads",
      { method: "POST", body: JSON.stringify({ fireOpportunityId }) },
      token,
    );
  }

  export async function updateSavedLead(id: string, status: SavedLeadStatus, token: string): Promise<void> {
    if (isMock()) return (await fixtures()).updateSavedLead(id, status);
    return apiFetch<void>(
      `/api/saved-leads/${id}`,
      { method: "PATCH", body: JSON.stringify({ status }) },
      token,
    );
  }

  export async function unsaveLead(id: string, token: string): Promise<void> {
    if (isMock()) return (await fixtures()).unsaveLead(id);
    return apiFetch<void>(`/api/saved-leads/${id}`, { method: "DELETE" }, token);
  }

  export async function getAccountMarkets(token: string): Promise<Market[]> {
    if (isMock()) return (await fixtures()).getAccountMarkets();
    return apiFetch<Market[]>("/api/account/markets", {}, token);
  }

  export async function getAccountMe(token: string): Promise<AccountMe> {
    if (isMock()) return (await fixtures()).getAccountMe();
    return apiFetch<AccountMe>("/api/account/me", {}, token);
  }

  export async function updateEmailPreferences(frequency: DigestFrequency, token: string): Promise<void> {
    if (isMock()) return (await fixtures()).updateEmailPreferences(frequency);
    return apiFetch<void>(
      "/api/email-preferences",
      { method: "PUT", body: JSON.stringify({ frequency }) },
      token,
    );
  }

  export async function submitSampleLeadRequest(
    input: { name: string; email: string; company: string; marketSlug: string },
  ): Promise<void> {
    if (isMock()) return (await fixtures()).submitSampleLeadRequest(input);
    return apiFetch<void>("/api/sample-leads", { method: "POST", body: JSON.stringify(input) });
  }

  export async function createCheckout(plan: PlanTier, token: string): Promise<{ url: string }> {
    if (isMock()) return (await fixtures()).createCheckout(plan);
    return apiFetch<{ url: string }>(
      "/api/billing/checkout",
      { method: "POST", body: JSON.stringify({ plan }) },
      token,
    );
  }

  export async function createBillingPortal(token: string): Promise<{ url: string }> {
    if (isMock()) return (await fixtures()).createBillingPortal();
    return apiFetch<{ url: string }>("/api/billing/portal", { method: "POST" }, token);
  }

  export async function getAdminSources(token: string): Promise<AdminSource[]> {
    if (isMock()) return (await fixtures()).getAdminSources();
    return apiFetch<AdminSource[]>("/api/admin/sources", {}, token);
  }

  export async function getAdminRuns(
    params: { sourceId?: string; page?: number },
    token: string,
  ): Promise<Paged<ScraperRunSummary>> {
    if (isMock()) return (await fixtures()).getAdminRuns(params);
    const qs = buildQuery({ sourceId: params.sourceId, page: params.page });
    return apiFetch<Paged<ScraperRunSummary>>(`/api/admin/scraper-runs${qs}`, {}, token);
  }

  export async function setSourceActive(id: string, active: boolean, token: string): Promise<void> {
    if (isMock()) return (await fixtures()).setSourceActive(id, active);
    return apiFetch<void>(
      `/api/admin/sources/${id}/${active ? "enable" : "disable"}`,
      { method: "POST" },
      token,
    );
  }
  ```
- [ ] Create the typed fixture stub `/Users/andynguyen/Desktop/Permit Torch/apps/web/lib/fixtures/index.ts` — this file IS the fixture contract; WS4 replaces every body with real fixture data but must keep every signature:
  ```typescript
  // FIXTURE CONTRACT (WS0 stub). WS4 owns this directory and replaces each
  // body with realistic fixture data matching @permittorch/types exactly.
  // Signatures mirror lib/api.ts with token parameters dropped. Do NOT add,
  // remove, or rename exports — lib/api.ts (frozen) calls them by name.
  import type {
    AccountMe,
    AdminSource,
    DigestFrequency,
    LeadDetail,
    LeadsResponse,
    Market,
    Paged,
    PlanTier,
    SavedLeadItem,
    SavedLeadStatus,
    ScraperRunSummary,
  } from "@permittorch/types";
  import type { LeadsQuery } from "@/lib/api";

  function notImplemented(name: string): never {
    throw new Error(`Fixture "${name}" is not implemented yet (WS4 owns apps/web/lib/fixtures/)`);
  }

  // NOTE: getMarkets/getMarketStats are intentionally ABSENT — lib/api.ts's
  // mock branch imports mockMarkets/mockMarketStats from ./markets (WS3-owned).
  export async function getLeads(_params: LeadsQuery): Promise<LeadsResponse> { notImplemented("getLeads"); }
  export async function getLead(_id: string): Promise<LeadDetail> { notImplemented("getLead"); }
  export async function getSavedLeads(): Promise<SavedLeadItem[]> { notImplemented("getSavedLeads"); }
  export async function saveLead(_fireOpportunityId: string): Promise<SavedLeadItem> { notImplemented("saveLead"); }
  export async function updateSavedLead(_id: string, _status: SavedLeadStatus): Promise<void> { notImplemented("updateSavedLead"); }
  export async function unsaveLead(_id: string): Promise<void> { notImplemented("unsaveLead"); }
  export async function getAccountMarkets(): Promise<Market[]> { notImplemented("getAccountMarkets"); }
  export async function getAccountMe(): Promise<AccountMe> { notImplemented("getAccountMe"); }
  export async function updateEmailPreferences(_frequency: DigestFrequency): Promise<void> { notImplemented("updateEmailPreferences"); }
  export async function submitSampleLeadRequest(_input: { name: string; email: string; company: string; marketSlug: string }): Promise<void> { notImplemented("submitSampleLeadRequest"); }
  export async function createCheckout(_plan: PlanTier): Promise<{ url: string }> { notImplemented("createCheckout"); }
  export async function createBillingPortal(): Promise<{ url: string }> { notImplemented("createBillingPortal"); }
  export async function getAdminSources(): Promise<AdminSource[]> { notImplemented("getAdminSources"); }
  export async function getAdminRuns(_params: { sourceId?: string; page?: number }): Promise<Paged<ScraperRunSummary>> { notImplemented("getAdminRuns"); }
  export async function setSourceActive(_id: string, _active: boolean): Promise<void> { notImplemented("setSourceActive"); }
  ```
- [ ] Run to verify pass: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/web" && pnpm test` — expect all 7 tests passing (6 api + 1 smoke). Then `pnpm typecheck` — exits 0.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/web/lib apps/web/__tests__/lib
  git commit -m "Implement typed API client with fixture mock branch"
  ```

---

### Task 11: Scaffold `PermitTorch.Api`, test project, solution, and ALL NuGet dependencies

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/PermitTorch.Api.csproj` (via `dotnet new web`; project root is `apps/api` itself so owned paths are `apps/api/Domain/`, `apps/api/Features/`, etc.)
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Program.cs` (template version for now; final form in Task 14)
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/appsettings.json` (template + `Scoring` weights section)
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/PermitTorch.sln`
- Create: `/Users/andynguyen/Desktop/Permit Torch/.config/dotnet-tools.json` (dotnet-ef local tool)

**Interfaces:**
- Consumes: .NET 10 SDK.
- Produces: frozen `.csproj` files with every package WS1/WS2/WS5 need. After WS0, no workstream edits any `.csproj`.

**Steps:**

- [ ] Scaffold the API project with the csproj directly in `apps/api`:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  dotnet new web -n PermitTorch.Api -o apps/api
  ```
  Expected: `apps/api/PermitTorch.Api.csproj`, `apps/api/Program.cs`, `apps/api/appsettings.json` created.
- [ ] Scaffold the test project nested under the API (locked path from the ownership table):
  ```bash
  dotnet new xunit -n PermitTorch.Api.Tests -o apps/api/tests/PermitTorch.Api.Tests
  dotnet add apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj reference apps/api/PermitTorch.Api.csproj
  ```
- [ ] **Critical:** because `tests/` is nested inside the API project directory, exclude it from the API project's default compile globs. Add this `ItemGroup` inside `<Project>` in `/Users/andynguyen/Desktop/Permit Torch/apps/api/PermitTorch.Api.csproj`:
  ```xml
  <ItemGroup>
    <Compile Remove="tests/**" />
    <Content Remove="tests/**" />
    <None Remove="tests/**" />
  </ItemGroup>
  ```
- [ ] Create the solution and add both projects:
  ```bash
  dotnet new sln -n PermitTorch -o apps/api
  dotnet sln apps/api/PermitTorch.sln add apps/api/PermitTorch.Api.csproj apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj
  ```
- [ ] Install ALL API packages (main project):
  ```bash
  dotnet add apps/api/PermitTorch.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
  dotnet add apps/api/PermitTorch.Api.csproj package Microsoft.EntityFrameworkCore.Design
  dotnet add apps/api/PermitTorch.Api.csproj package EFCore.NamingConventions
  dotnet add apps/api/PermitTorch.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
  dotnet add apps/api/PermitTorch.Api.csproj package Stripe.net
  dotnet add apps/api/PermitTorch.Api.csproj package Sentry.AspNetCore
  ```
  Expected: each command reports the latest stable version added. Confirm `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, and `Microsoft.AspNetCore.Authentication.JwtBearer` resolved to 10.x versions (EF Core 10 / .NET 10 line) by reading the `PackageReference` lines in the csproj.
- [ ] Install ALL test packages:
  ```bash
  dotnet add apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
  dotnet add apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj package Testcontainers.PostgreSql
  ```
  (xunit, its runner, and coverlet come with the template.)
- [ ] Install the EF CLI as a repo-local tool:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  dotnet new tool-manifest
  dotnet tool install dotnet-ef
  ```
- [ ] Pre-populate scoring configuration so WS1 never edits `appsettings.json` (weights = PRD §15 defaults, keys = the locked SignalType strings from master doc §5). Replace `/Users/andynguyen/Desktop/Permit Torch/apps/api/appsettings.json` with:
  ```json
  {
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "AllowedHosts": "*",
    "Scoring": {
      "Weights": {
        "NEW_COMMERCIAL_BUILD": 25,
        "FIRE_SPRINKLER_SCOPE": 25,
        "FIRE_ALARM_SCOPE": 20,
        "FAILED_INSPECTION": 20,
        "PERMIT_RECENT": 15,
        "HIGH_PROJECT_VALUE": 10,
        "LARGE_SQUARE_FOOTAGE": 10,
        "NO_CONTRACTOR_LISTED": 10,
        "OLD_PERMIT": -20,
        "CLOSED_PERMIT": -30
      }
    }
  }
  ```
- [ ] Verify: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/api" && dotnet build PermitTorch.sln` succeeds (0 errors), and `dotnet test PermitTorch.sln` runs the template's placeholder test green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/api .config
  git commit -m "Scaffold ASP.NET Core API and xUnit test project with all NuGet dependencies"
  ```

---

### Task 12: Data layer — Enums, Entities, AppDbContext (TDD via model tests)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/Enums.cs`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/Entities.cs`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/AppDbContext.cs`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/DesignTimeDbContextFactory.cs`
- Test: `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/Data/AppDbContextModelTests.cs`

**Interfaces:**
- Consumes: master doc §3 (LOCKED schema — every class, property, and comment name verbatim; field shorthand converted to `{ get; set; }` auto-properties).
- Produces: `PermitTorch.Api.Data` namespace — `AppDbContext` with DbSets `Markets, Sources, Permits, PermitParticipants, FireOpportunities, LeadSignals, ScraperRuns, Organizations, AppUsers, Subscriptions, SubscriptionMarkets, SavedLeads, EmailPreferences, SampleLeadRequests`; snake_case table names via `UseSnakeCaseNamingConvention()`; consumed by WS1 (ingestion), WS2 (features), Task 13 (migration).

**Steps:**

- [ ] Delete the template placeholder test `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/UnitTest1.cs`.
- [ ] **Write the failing tests first.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/Data/AppDbContextModelTests.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Tests.Data;

  public class AppDbContextModelTests
  {
      private static AppDbContext CreateContext()
      {
          // Model construction only — never connects.
          var options = new DbContextOptionsBuilder<AppDbContext>()
              .UseNpgsql("Host=localhost;Database=model_only")
              .Options;
          return new AppDbContext(options);
      }

      [Theory]
      [InlineData(typeof(Market))]
      [InlineData(typeof(Source))]
      [InlineData(typeof(Permit))]
      [InlineData(typeof(PermitParticipant))]
      [InlineData(typeof(FireOpportunity))]
      [InlineData(typeof(LeadSignal))]
      [InlineData(typeof(ScraperRun))]
      [InlineData(typeof(Organization))]
      [InlineData(typeof(AppUser))]
      [InlineData(typeof(Subscription))]
      [InlineData(typeof(SubscriptionMarket))]
      [InlineData(typeof(SavedLead))]
      [InlineData(typeof(EmailPreference))]
      [InlineData(typeof(SampleLeadRequest))]
      public void Model_contains_entity(Type entityType)
      {
          using var db = CreateContext();
          Assert.NotNull(db.Model.FindEntityType(entityType));
      }

      [Fact]
      public void Tables_use_snake_case_names()
      {
          using var db = CreateContext();
          Assert.Equal("app_users", db.Model.FindEntityType(typeof(AppUser))!.GetTableName());
          Assert.Equal("markets", db.Model.FindEntityType(typeof(Market))!.GetTableName());
          Assert.Equal("saved_leads", db.Model.FindEntityType(typeof(SavedLead))!.GetTableName());
          Assert.Equal("sample_lead_requests", db.Model.FindEntityType(typeof(SampleLeadRequest))!.GetTableName());
          Assert.Equal("permits", db.Model.FindEntityType(typeof(Permit))!.GetTableName());
      }

      [Fact]
      public void Permit_has_unique_index_on_source_id_and_external_id()
      {
          using var db = CreateContext();
          var entity = db.Model.FindEntityType(typeof(Permit))!;
          var index = entity.GetIndexes().Single(i =>
              i.Properties.Select(p => p.Name).SequenceEqual(new[] { "SourceId", "ExternalId" }));
          Assert.True(index.IsUnique);
      }

      [Fact]
      public void Permit_has_nonunique_index_on_fingerprint()
      {
          using var db = CreateContext();
          var entity = db.Model.FindEntityType(typeof(Permit))!;
          var index = entity.GetIndexes().Single(i =>
              i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Fingerprint" }));
          Assert.False(index.IsUnique);
      }

      [Fact]
      public void AppUser_has_unique_index_on_clerk_user_id()
      {
          using var db = CreateContext();
          var index = db.Model.FindEntityType(typeof(AppUser))!.GetIndexes().Single(i =>
              i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ClerkUserId" }));
          Assert.True(index.IsUnique);
      }

      [Fact]
      public void Market_has_unique_index_on_slug()
      {
          using var db = CreateContext();
          var index = db.Model.FindEntityType(typeof(Market))!.GetIndexes().Single(i =>
              i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Slug" }));
          Assert.True(index.IsUnique);
      }

      [Fact]
      public void SavedLead_has_unique_index_on_user_and_opportunity()
      {
          using var db = CreateContext();
          var index = db.Model.FindEntityType(typeof(SavedLead))!.GetIndexes().Single(i =>
              i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "FireOpportunityId" }));
          Assert.True(index.IsUnique);
      }

      [Fact]
      public void SampleLeadRequest_has_unique_index_on_email_and_market_slug()
      {
          using var db = CreateContext();
          var index = db.Model.FindEntityType(typeof(SampleLeadRequest))!.GetIndexes().Single(i =>
              i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Email", "MarketSlug" }));
          Assert.True(index.IsUnique);
      }

      [Fact]
      public void SubscriptionMarket_has_composite_primary_key()
      {
          using var db = CreateContext();
          var key = db.Model.FindEntityType(typeof(SubscriptionMarket))!.FindPrimaryKey()!;
          Assert.Equal(new[] { "SubscriptionId", "MarketId" }, key.Properties.Select(p => p.Name).ToArray());
      }

      [Fact]
      public void FireOpportunity_is_one_to_one_with_permit()
      {
          using var db = CreateContext();
          var fk = db.Model.FindEntityType(typeof(FireOpportunity))!.GetForeignKeys()
              .Single(k => k.PrincipalEntityType.ClrType == typeof(Permit));
          Assert.True(fk.IsUnique);
          Assert.Equal("PermitId", fk.Properties.Single().Name);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/api" && dotnet test PermitTorch.sln
  ```
  Expected failure: compile errors `CS0246: The type or namespace name 'AppDbContext' could not be found` (and the entity types).
- [ ] **Implementation.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/Enums.cs` (master doc §3 verbatim):
  ```csharp
  namespace PermitTorch.Api.Data;

  public enum HealthStatus { Healthy, Warning, Stale, Failed, Disabled }
  public enum FireCategory { FireSprinkler, FireAlarm, FireSuppression, KitchenSuppression, FireInspection, ViolationCorrection, GeneralFireProtection }
  public enum ParticipantRole { Owner, Applicant, Contractor, GeneralContractor }
  public enum UserRole { Member, Admin, SuperAdmin }          // SuperAdmin = PermitTorch staff
  public enum PlanTier { Starter, Pro, Territory }
  public enum SavedLeadStatus { Saved, Contacted }
  public enum DigestFrequency { None, Daily, Weekly }
  public enum PermitStatusKind { New, Active, Inspection, Failed, Closed, Unknown }
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/Entities.cs` — §3 fields converted to auto-properties; non-nullable strings/navigations initialized `= null!;`, collections `= new();`:
  ```csharp
  namespace PermitTorch.Api.Data;

  // All classes public; Guid PKs generated client-side with Guid.NewGuid().
  public class Market
  {
      public Guid Id { get; set; }
      public string Name { get; set; } = null!;
      public string City { get; set; } = null!;
      public string State { get; set; } = null!;
      public string Slug { get; set; } = null!;              // e.g. "houston-tx"
      public bool Active { get; set; }
      public List<Source> Sources { get; set; } = new();
  }

  public class Source
  {
      public Guid Id { get; set; }
      public Guid MarketId { get; set; }
      public Market Market { get; set; } = null!;
      public string Name { get; set; } = null!;
      public string City { get; set; } = null!;
      public string State { get; set; } = null!;
      public string PortalType { get; set; } = null!;        // e.g. "accela", "arcgis", "socrata"
      public string SourceUrl { get; set; } = null!;
      public string Jurisdiction { get; set; } = null!;      // matches scraper COVERAGE_REPORT jurisdiction key
      public bool Active { get; set; }
      public DateTime? LastSuccessfulRunAt { get; set; }
      public DateTime? LastRecordSeenAt { get; set; }
      public int RecordsLastRun { get; set; }
      public HealthStatus HealthStatus { get; set; }
  }

  public class Permit
  {
      public Guid Id { get; set; }
      public Guid SourceId { get; set; }
      public Source Source { get; set; } = null!;
      public string ExternalId { get; set; } = null!;        // scraper record id; unique with SourceId
      public string? PermitNumber { get; set; }
      public string? PermitType { get; set; }
      public string? Description { get; set; }
      public PermitStatusKind Status { get; set; }
      public string? RawStatus { get; set; }
      public string? Address { get; set; }
      public string City { get; set; } = null!;
      public string State { get; set; } = null!;
      public string? Zip { get; set; }
      public double? Latitude { get; set; }
      public double? Longitude { get; set; }
      public DateTime? FiledDate { get; set; }
      public DateTime? IssuedDate { get; set; }
      public decimal? EstimatedValue { get; set; }
      public int? SquareFootage { get; set; }
      public string? OwnerName { get; set; }
      public string? ContractorName { get; set; }
      public string SourceUrl { get; set; } = null!;
      public string Fingerprint { get; set; } = null!;       // sha256 of address|permit_type|filed_date|description
      public DateTime FirstSeenAt { get; set; }
      public DateTime LastSeenAt { get; set; }
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }
      public List<PermitParticipant> Participants { get; set; } = new();
      public FireOpportunity? Opportunity { get; set; }
  }

  public class PermitParticipant
  {
      public Guid Id { get; set; }
      public Guid PermitId { get; set; }
      public ParticipantRole Role { get; set; }
      public string Name { get; set; } = null!;
  }

  public class FireOpportunity
  {
      public Guid Id { get; set; }
      public Guid PermitId { get; set; }
      public Permit Permit { get; set; } = null!;
      public FireCategory Category { get; set; }
      public int LeadScore { get; set; }                     // 0–100, computed by PermitTorch ScoringEngine
      public decimal Confidence { get; set; }                // 0–1 classification confidence
      public string Reason { get; set; } = null!;            // one-sentence "why this matters"
      public DateTime FirstDetectedAt { get; set; }
      public DateTime LastUpdatedAt { get; set; }
      public List<LeadSignal> Signals { get; set; } = new();
  }

  public class LeadSignal
  {
      public Guid Id { get; set; }
      public Guid FireOpportunityId { get; set; }
      public string SignalType { get; set; } = null!;        // e.g. "NEW_COMMERCIAL_BUILD"
      public string Description { get; set; } = null!;       // human-readable, e.g. "New commercial construction"
      public int Weight { get; set; }                        // signed points contributed
  }

  public class ScraperRun
  {
      public Guid Id { get; set; }
      public Guid? SourceId { get; set; }
      public string ApifyRunId { get; set; } = null!;
      public string Status { get; set; } = null!;            // Apify run status string
      public DateTime StartedAt { get; set; }
      public DateTime? FinishedAt { get; set; }
      public int RecordsImported { get; set; }
      public int DuplicatesSkipped { get; set; }
      public int Classified { get; set; }
      public int Failures { get; set; }
      public double DurationSeconds { get; set; }
      public string? CoverageReportJson { get; set; }        // raw COVERAGE_REPORT payload
  }

  public class Organization
  {
      public Guid Id { get; set; }
      public string Name { get; set; } = null!;
      public string? ClerkOrgId { get; set; }
      public List<AppUser> Users { get; set; } = new();
      public Subscription? Subscription { get; set; }
  }

  public class AppUser
  {
      public Guid Id { get; set; }
      public string ClerkUserId { get; set; } = null!;
      public string Email { get; set; } = null!;
      public Guid OrganizationId { get; set; }
      public Organization Organization { get; set; } = null!;
      public UserRole Role { get; set; }
  }

  public class Subscription
  {
      public Guid Id { get; set; }
      public Guid OrganizationId { get; set; }
      public string StripeCustomerId { get; set; } = null!;
      public string? StripeSubscriptionId { get; set; }
      public PlanTier Plan { get; set; }
      public string Status { get; set; } = null!;            // Stripe status: trialing|active|past_due|canceled
      public DateTime? TrialEndsAt { get; set; }
      public List<SubscriptionMarket> Markets { get; set; } = new();
  }

  public class SubscriptionMarket
  {
      public Guid SubscriptionId { get; set; }
      public Guid MarketId { get; set; }
  }

  public class SavedLead
  {
      public Guid Id { get; set; }
      public Guid UserId { get; set; }
      public Guid FireOpportunityId { get; set; }
      public SavedLeadStatus Status { get; set; }
      public DateTime CreatedAt { get; set; }
  }

  public class EmailPreference
  {
      public Guid Id { get; set; }
      public Guid UserId { get; set; }
      public DigestFrequency Frequency { get; set; }
  }

  public class SampleLeadRequest                             // marketing lead magnet capture
  {
      public Guid Id { get; set; }
      public string Name { get; set; } = null!;
      public string Email { get; set; } = null!;
      public string Company { get; set; } = null!;
      public string MarketSlug { get; set; } = null!;
      public DateTime CreatedAt { get; set; }
  }
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/AppDbContext.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;

  namespace PermitTorch.Api.Data;

  public class AppDbContext : DbContext
  {
      public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

      public DbSet<Market> Markets => Set<Market>();
      public DbSet<Source> Sources => Set<Source>();
      public DbSet<Permit> Permits => Set<Permit>();
      public DbSet<PermitParticipant> PermitParticipants => Set<PermitParticipant>();
      public DbSet<FireOpportunity> FireOpportunities => Set<FireOpportunity>();
      public DbSet<LeadSignal> LeadSignals => Set<LeadSignal>();
      public DbSet<ScraperRun> ScraperRuns => Set<ScraperRun>();
      public DbSet<Organization> Organizations => Set<Organization>();
      public DbSet<AppUser> AppUsers => Set<AppUser>();
      public DbSet<Subscription> Subscriptions => Set<Subscription>();
      public DbSet<SubscriptionMarket> SubscriptionMarkets => Set<SubscriptionMarket>();
      public DbSet<SavedLead> SavedLeads => Set<SavedLead>();
      public DbSet<EmailPreference> EmailPreferences => Set<EmailPreference>();
      public DbSet<SampleLeadRequest> SampleLeadRequests => Set<SampleLeadRequest>();

      protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
      {
          // Applied here (not per call site) so snake_case table/column naming
          // can never be forgotten regardless of how the context is constructed.
          optionsBuilder.UseSnakeCaseNamingConvention();
      }

      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          modelBuilder.Entity<Market>(e =>
          {
              e.HasIndex(m => m.Slug).IsUnique();
              e.HasMany(m => m.Sources).WithOne(s => s.Market).HasForeignKey(s => s.MarketId);
          });

          modelBuilder.Entity<Permit>(e =>
          {
              e.HasIndex(p => new { p.SourceId, p.ExternalId }).IsUnique();
              e.HasIndex(p => p.Fingerprint);
              e.HasOne(p => p.Source).WithMany().HasForeignKey(p => p.SourceId);
              e.HasMany(p => p.Participants).WithOne().HasForeignKey(pp => pp.PermitId);
          });

          modelBuilder.Entity<FireOpportunity>(e =>
          {
              e.HasOne(o => o.Permit).WithOne(p => p.Opportunity)
                  .HasForeignKey<FireOpportunity>(o => o.PermitId);
              e.HasMany(o => o.Signals).WithOne().HasForeignKey(s => s.FireOpportunityId);
          });

          modelBuilder.Entity<ScraperRun>(e =>
          {
              e.HasOne<Source>().WithMany().HasForeignKey(r => r.SourceId);
          });

          modelBuilder.Entity<Organization>(e =>
          {
              e.HasMany(o => o.Users).WithOne(u => u.Organization).HasForeignKey(u => u.OrganizationId);
              e.HasOne(o => o.Subscription).WithOne()
                  .HasForeignKey<Subscription>(s => s.OrganizationId);
          });

          modelBuilder.Entity<AppUser>(e =>
          {
              e.HasIndex(u => u.ClerkUserId).IsUnique();
          });

          modelBuilder.Entity<SubscriptionMarket>(e =>
          {
              e.HasKey(sm => new { sm.SubscriptionId, sm.MarketId });
              e.HasOne<Subscription>().WithMany(s => s.Markets).HasForeignKey(sm => sm.SubscriptionId);
              e.HasOne<Market>().WithMany().HasForeignKey(sm => sm.MarketId);
          });

          modelBuilder.Entity<SavedLead>(e =>
          {
              e.HasIndex(sl => new { sl.UserId, sl.FireOpportunityId }).IsUnique();
              e.HasOne<AppUser>().WithMany().HasForeignKey(sl => sl.UserId);
              e.HasOne<FireOpportunity>().WithMany().HasForeignKey(sl => sl.FireOpportunityId);
          });

          modelBuilder.Entity<EmailPreference>(e =>
          {
              e.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId);
          });

          modelBuilder.Entity<SampleLeadRequest>(e =>
          {
              e.HasIndex(r => new { r.Email, r.MarketSlug }).IsUnique();
          });
      }
  }
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/DesignTimeDbContextFactory.cs` so `dotnet ef` works without touching `Program.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Design;

  namespace PermitTorch.Api.Data;

  public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
  {
      public AppDbContext CreateDbContext(string[] args)
      {
          var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
              ?? "Host=localhost;Port=5432;Database=permittorch;Username=postgres;Password=postgres";
          var options = new DbContextOptionsBuilder<AppDbContext>()
              .UseNpgsql(connectionString)
              .Options;
          return new AppDbContext(options);
      }
  }
  ```
- [ ] Run to verify pass: `cd "/Users/andynguyen/Desktop/Permit Torch/apps/api" && dotnet test PermitTorch.sln` — expect all 23 model tests green (14 entity theory cases + 9 facts).
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/api/Data apps/api/tests
  git commit -m "Add EF Core entities, enums, and DbContext with locked schema"
  ```

---

### Task 13: Initial migration with unique indexes and FTS GIN index (Testcontainers-verified)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/Migrations/*_InitialCreate.cs` (generated, then edited for FTS)
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Data/Migrations/AppDbContextModelSnapshot.cs` (generated)
- Test: `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/Data/MigrationTests.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 12); Docker daemon (Testcontainers).
- Produces: the schema WS1/WS2/WS5 run against — snake_case tables, unique indexes from master doc §3, and GIN index `ix_permits_fts` on `to_tsvector('english', coalesce(description,'') || ' ' || coalesce(address,''))`.

**Steps:**

- [ ] **Write the failing integration test first.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/Data/MigrationTests.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using Testcontainers.PostgreSql;

  namespace PermitTorch.Api.Tests.Data;

  public class MigrationTests : IAsyncLifetime
  {
      private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
          .WithImage("postgres:17-alpine")
          .Build();

      public Task InitializeAsync() => _postgres.StartAsync();
      public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

      private AppDbContext CreateContext()
      {
          var options = new DbContextOptionsBuilder<AppDbContext>()
              .UseNpgsql(_postgres.GetConnectionString())
              .Options;
          return new AppDbContext(options);
      }

      [Fact]
      public async Task Migrations_apply_cleanly_and_create_expected_indexes()
      {
          await using var db = CreateContext();
          await db.Database.MigrateAsync();

          var indexdefs = await db.Database
              .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
              .ToListAsync();

          // Unique indexes locked in master doc §3.
          Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("permits") && d.Contains("source_id") && d.Contains("external_id"));
          Assert.Contains(indexdefs, d => !d.Contains("UNIQUE") && d.Contains("permits") && d.Contains("fingerprint"));
          Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("app_users") && d.Contains("clerk_user_id"));
          Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("markets") && d.Contains("slug"));
          Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("saved_leads") && d.Contains("user_id") && d.Contains("fire_opportunity_id"));
          Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("sample_lead_requests") && d.Contains("email") && d.Contains("market_slug"));

          // FTS GIN index on permits description + address.
          Assert.Contains(indexdefs, d => d.Contains("ix_permits_fts") && d.Contains("gin") && d.Contains("to_tsvector"));
      }

      [Fact]
      public async Task Timestamps_map_to_timestamptz_and_money_to_numeric()
      {
          await using var db = CreateContext();
          await db.Database.MigrateAsync();

          var columns = await db.Database
              .SqlQuery<string>($"SELECT column_name || ':' || data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'permits'")
              .ToListAsync();

          Assert.Contains("filed_date:timestamp with time zone", columns);
          Assert.Contains("created_at:timestamp with time zone", columns);
          Assert.Contains("estimated_value:numeric", columns);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~MigrationTests"
  ```
  Expected failure: `Migrations_apply_cleanly_and_create_expected_indexes` fails — `MigrateAsync` applies zero migrations, so every `Assert.Contains` on the (empty) index list fails.
- [ ] Generate the migration:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/api"
  dotnet tool run dotnet-ef migrations add InitialCreate --project PermitTorch.Api.csproj --output-dir Data/Migrations
  ```
  Expected outcome: `Data/Migrations/<timestamp>_InitialCreate.cs` + `AppDbContextModelSnapshot.cs` with snake_case tables (`permits`, `app_users`, …) and all six indexes from Task 12's model config.
- [ ] Edit the generated `<timestamp>_InitialCreate.cs`: at the END of the `Up` method add the FTS index, and at the START of the `Down` method drop it:
  ```csharp
  // In Up(MigrationBuilder migrationBuilder), after all generated statements:
  migrationBuilder.Sql(
      "CREATE INDEX ix_permits_fts ON permits USING GIN (to_tsvector('english', coalesce(description,'') || ' ' || coalesce(address,'')));");

  // In Down(MigrationBuilder migrationBuilder), before all generated statements:
  migrationBuilder.Sql("DROP INDEX IF EXISTS ix_permits_fts;");
  ```
- [ ] Run to verify pass (Docker must be running):
  ```bash
  dotnet test PermitTorch.sln --filter "FullyQualifiedName~MigrationTests"
  ```
  Expected: both tests green.
- [ ] Run the full API suite to confirm nothing regressed: `dotnet test PermitTorch.sln` — all green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/api/Data/Migrations apps/api/tests
  git commit -m "Add initial migration with unique indexes and full-text GIN index"
  ```

---

### Task 14: `Program.cs` in FINAL form + Setup stubs + health endpoint (TDD)

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Setup/PipelineSetup.cs`
- Create: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Setup/FeaturesSetup.cs`
- Modify: `/Users/andynguyen/Desktop/Permit Torch/apps/api/Program.cs` (this is the LAST edit ever made to this file)
- Test: `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/HealthEndpointTests.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 12).
- Produces (LOCKED):
  - `GET /api/health` → `200 { "status": "ok" }`, no auth.
  - `public static IServiceCollection AddPipelineServices(this IServiceCollection services, IConfiguration configuration)` in `Setup/PipelineSetup.cs` — WS1 fills in.
  - `public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)` and `public static WebApplication MapFeatureEndpoints(this WebApplication app)` in `Setup/FeaturesSetup.cs` — WS2 fills in (endpoint mapping AND any middleware, since `Program.cs` is frozen).
  - `public partial class Program { }` for `WebApplicationFactory<Program>` integration tests.

**Steps:**

- [ ] **Write the failing test first.** Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/tests/PermitTorch.Api.Tests/HealthEndpointTests.cs`:
  ```csharp
  using System.Net.Http.Json;
  using System.Text.Json;
  using Microsoft.AspNetCore.Mvc.Testing;

  namespace PermitTorch.Api.Tests;

  public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
  {
      private readonly WebApplicationFactory<Program> _factory;

      public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

      [Fact]
      public async Task Get_api_health_returns_200_with_ok_status()
      {
          var client = _factory.CreateClient();

          var response = await client.GetAsync("/api/health");

          response.EnsureSuccessStatusCode();
          var body = await response.Content.ReadFromJsonAsync<JsonElement>();
          Assert.Equal("ok", body.GetProperty("status").GetString());
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~HealthEndpointTests"
  ```
  Expected failure: compile error `CS0246: The type or namespace name 'Program' could not be found` (the template's top-level Program has no accessible `partial class Program`) — or, if the template already exposes it, a runtime 404 failing `EnsureSuccessStatusCode`. Either failure is the correct red state.
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/Setup/PipelineSetup.cs`:
  ```csharp
  namespace PermitTorch.Api.Setup;

  public static class PipelineSetup
  {
      /// <summary>
      /// WS1 (ws/pipeline) registers everything here: IPermitSourceProvider /
      /// ApifyPermitProvider, ScoringOptions (bound from the "Scoring" section
      /// of configuration), ingestion and source-health IHostedService jobs.
      /// WS0 ships it as an intentionally empty stub so Program.cs never changes.
      /// </summary>
      public static IServiceCollection AddPipelineServices(this IServiceCollection services, IConfiguration configuration)
      {
          return services;
      }
  }
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/apps/api/Setup/FeaturesSetup.cs`:
  ```csharp
  namespace PermitTorch.Api.Setup;

  public static class FeaturesSetup
  {
      /// <summary>
      /// WS2 (ws/api) registers everything here: Clerk JWT bearer authentication
      /// (Microsoft.AspNetCore.Authentication.JwtBearer against CLERK_JWKS_URL /
      /// CLERK_ISSUER), authorization policies, rate limiting, CORS, Stripe and
      /// Resend clients, and per-feature services.
      /// WS0 ships it as an intentionally empty stub so Program.cs never changes.
      /// </summary>
      public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
      {
          return services;
      }

      /// <summary>
      /// WS2 maps every Features/ endpoint group here and adds any middleware
      /// (UseCors, UseRateLimiter, ...). It receives the WebApplication —
      /// not just an IEndpointRouteBuilder — precisely so the frozen Program.cs
      /// never needs another edit. Note: ASP.NET Core auto-inserts
      /// UseAuthentication/UseAuthorization when those services are registered.
      /// </summary>
      public static WebApplication MapFeatureEndpoints(this WebApplication app)
      {
          return app;
      }
  }
  ```
- [ ] Replace `/Users/andynguyen/Desktop/Permit Torch/apps/api/Program.cs` with its FINAL form:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Setup;

  // FINAL FORM (WS0). This file is frozen: WS1 extends AddPipelineServices,
  // WS2 extends AddFeatureServices/MapFeatureEndpoints — nobody edits Program.cs.
  var builder = WebApplication.CreateBuilder(args);

  var connectionString = builder.Configuration["DATABASE_URL"]
      ?? "Host=localhost;Port=5432;Database=permittorch;Username=postgres;Password=postgres";
  builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

  builder.Services.AddPipelineServices(builder.Configuration);
  builder.Services.AddFeatureServices(builder.Configuration);

  var app = builder.Build();

  app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
  app.MapFeatureEndpoints();

  app.Run();

  public partial class Program { }
  ```
- [ ] Run to verify pass: `dotnet test PermitTorch.sln --filter "FullyQualifiedName~HealthEndpointTests"` — green. Then the full suite: `dotnet test PermitTorch.sln` — all green.
- [ ] Sanity-run the API and hit the endpoint:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch/apps/api"
  dotnet run --project PermitTorch.Api.csproj --urls http://localhost:5000 &
  sleep 5 && curl -s http://localhost:5000/api/health
  # expected output: {"status":"ok"}
  kill %1
  ```
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git add apps/api/Program.cs apps/api/Setup apps/api/tests
  git commit -m "Finalize Program.cs with health endpoint and stub pipeline and feature setup extensions"
  ```

---

### Task 15: `.env.example` and GitHub Actions CI

**Files:**
- Create: `/Users/andynguyen/Desktop/Permit Torch/.env.example`
- Create: `/Users/andynguyen/Desktop/Permit Torch/.github/workflows/ci.yml`

**Interfaces:**
- Consumes: master doc §9 (env var list); workspace scripts (Task 1/4); `apps/api/PermitTorch.sln` (Task 11).
- Produces: CI that gates every PR/push — `api` job (dotnet build+test, Docker available for Testcontainers) and `web` job (pnpm install, typecheck all packages, vitest).

**Steps:**

- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/.env.example` (every var from master doc §9, grouped by consumer; values are placeholders — real secrets NEVER get committed):
  ```bash
  # ── apps/api ────────────────────────────────────────────────
  # Npgsql connection string
  DATABASE_URL=Host=localhost;Port=5432;Database=permittorch;Username=postgres;Password=postgres
  # Apify Actor API access
  APIFY_TOKEN=
  APIFY_ACTOR_ID=
  # Clerk JWT validation
  CLERK_SECRET_KEY=
  CLERK_JWKS_URL=
  CLERK_ISSUER=
  # Stripe billing
  STRIPE_SECRET_KEY=
  STRIPE_WEBHOOK_SECRET=
  STRIPE_PRICE_STARTER=
  STRIPE_PRICE_PRO=
  STRIPE_PRICE_TERRITORY=
  # Resend digest + transactional email
  RESEND_API_KEY=
  EMAIL_FROM=
  # Sentry errors (api + web)
  SENTRY_DSN=

  # ── apps/web ────────────────────────────────────────────────
  # API client base URL; NEXT_PUBLIC_API_MOCK=1 serves fixtures (WS4 dev mode)
  NEXT_PUBLIC_API_URL=http://localhost:5000
  NEXT_PUBLIC_API_MOCK=1
  # Clerk (CLERK_SECRET_KEY above is shared by web server-side)
  NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=
  # PostHog analytics
  NEXT_PUBLIC_POSTHOG_KEY=
  ```
- [ ] Create `/Users/andynguyen/Desktop/Permit Torch/.github/workflows/ci.yml`:
  ```yaml
  name: CI

  on:
    push:
      branches: [main]
    pull_request:

  jobs:
    api:
      name: API build and test
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: "10.0.x"
        - name: Build
          run: dotnet build apps/api/PermitTorch.sln --configuration Release
        - name: Test
          run: dotnet test apps/api/PermitTorch.sln --configuration Release --no-build --logger "trx" --verbosity normal

    web:
      name: Web typecheck and test
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: pnpm/action-setup@v4
        - uses: actions/setup-node@v4
          with:
            node-version: 22
            cache: pnpm
        - name: Install
          run: pnpm install --frozen-lockfile
        - name: Typecheck
          run: pnpm -r typecheck
        - name: Unit tests
          run: pnpm -r test
  ```
  Notes: `ubuntu-latest` ships Docker, so the Testcontainers migration test runs in the `api` job unmodified. `pnpm/action-setup@v4` reads the pnpm version from the root `package.json` `packageManager` field if present — add `"packageManager": "pnpm@<your local major.minor.patch from pnpm --version>"` to the root `package.json` now so local and CI agree.
- [ ] Add the `packageManager` field to `/Users/andynguyen/Desktop/Permit Torch/package.json` (example — substitute the exact local version printed by `pnpm --version`):
  ```json
  "packageManager": "pnpm@10.14.0"
  ```
- [ ] Verify locally that exactly what CI runs passes:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  dotnet build apps/api/PermitTorch.sln --configuration Release
  dotnet test apps/api/PermitTorch.sln --configuration Release --no-build
  pnpm install --frozen-lockfile
  pnpm -r typecheck
  pnpm -r test
  ```
  All five commands exit 0.
- [ ] Commit:
  ```bash
  git add .env.example .github package.json
  git commit -m "Add environment variable template and GitHub Actions CI"
  ```

---

### Task 16: Final verification, push to main, and worktree creation

**Files:**
- Create: none (verification + git operations only)

**Interfaces:**
- Consumes: everything above.
- Produces: `main` in the exact state WS1–WS4 branch from, plus the four worktrees from master doc §1.

**Steps:**

- [ ] Full clean verification of both apps from scratch:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git status --short          # expect EMPTY output — nothing uncommitted
  pnpm install --frozen-lockfile
  pnpm -r typecheck           # types + web: 0 errors
  pnpm -r test                # web: 7 tests green
  cd apps/web && NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_Y2xlcmsuZXhhbXBsZS5jb20k pnpm build && cd ../..
  dotnet build apps/api/PermitTorch.sln --configuration Release   # 0 warnings-as-errors, 0 errors
  dotnet test apps/api/PermitTorch.sln --configuration Release --no-build  # all green (Docker running)
  ```
- [ ] Confirm the frozen-file inventory exists exactly as the ownership table expects:
  ```bash
  ls apps/web/middleware.ts apps/web/lib/api.ts apps/web/lib/fixtures/index.ts \
     apps/api/Program.cs apps/api/Setup/PipelineSetup.cs apps/api/Setup/FeaturesSetup.cs \
     apps/api/Data/Entities.cs apps/api/Data/Enums.cs apps/api/Data/AppDbContext.cs \
     packages/types/src/index.ts .github/workflows/ci.yml .env.example
  ```
  Every path listed; no `No such file` output.
- [ ] Push `main` (WS0 is serial and lands directly on main):
  ```bash
  git push origin main
  ```
  Then confirm both CI jobs pass on GitHub (`gh run watch` or check the Actions tab).
- [ ] Create the four parallel worktrees exactly as master doc §1 specifies:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git worktree add ../pt-pipeline  -b ws/pipeline
  git worktree add ../pt-api       -b ws/api
  git worktree add ../pt-marketing -b ws/marketing
  git worktree add ../pt-dashboard -b ws/dashboard
  ```
- [ ] Verify: `git worktree list` shows the main checkout plus `../pt-pipeline` (ws/pipeline), `../pt-api` (ws/api), `../pt-marketing` (ws/marketing), `../pt-dashboard` (ws/dashboard), all at the same commit as `main`. WS0 is complete — WS1–WS4 may start in parallel.
