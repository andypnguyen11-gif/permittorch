# Lead Outcome Tags and First-Seen Date Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let each user tag a lead with an outcome, give super admins a report comparing those tags with score bands, and show the "first seen by PermitTorch" date wherever it means something.

**Architecture:** A new vertical slice `Features/LeadOutcomes/` owns the table's endpoints, the per-user read helper and the admin report. The first-seen rule moves into one domain helper that scoring and lead reads both call. The web app renders `myOutcome` and `firstSeenAt` and calls two routes; it holds no rules.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core + Npgsql (snake_case), xUnit with Testcontainers; Next.js App Router, shadcn/ui (`dropdown-menu`), Vitest + Testing Library; Playwright.

**Spec:** `docs/superpowers/specs/2026-10-21-lead-outcome-tags-design.md`

## Global Constraints

- Enums: integer columns; SCREAMING_SNAKE strings on the wire via `ApiJson`. Wire values: `WORTH_CONTACTING`, `ALREADY_AWARDED`, `WRONG_TRADE`, `TOO_LATE`.
- Display labels, exactly: "Worth contacting", "Already awarded", "Wrong trade", "Too late". Clear action: "Clear".
- Bands: `min(score_at_tag / 10, 9)` with integer division; rows 90–100 down to 0–9.
- `MinAnswersPerBand = 10`. Below it a band shows counts, no share, and "Too few answers to read". At 0 answers, no share.
- `UserRole.SuperAdmin` tags are stored and excluded from every report count. `Admin` and `Member` count.
- First seen: `Permit.FirstSeenAt` when `Source.RecencyFromFirstSeenSince` is set and `FirstSeenAt >= RecencyFromFirstSeenSince`; otherwise null. The rescoring job's rolling-window check is not part of this helper.
- The client never sends a score. Every set and change copies the lead's current `LeadScore` into `score_at_tag`.
- Tags never change the score, the order, or which leads appear. No reads return another user's tag.
- Commit messages: imperative, no task numbers, no PR numbers, no Co-Authored-By or generated-by lines.

## Review Focus

1. **Rapid repeat clicks on the tag menu** (two tags chosen before the first call lands): the last choice must win and the label must never show a tag the server rejected. Test in Task 6.
2. **Two first-time sets racing** (two tabs, same user, same lead): the second insert hits the unique index (23505). The endpoint must catch it, reload the row and apply the change as an update, returning 200, never 500. A true race is impractical to stage in a test, so Task 2 tests the update path on a pre-existing row and the catch is reviewed by reading.
3. **A tagged lead that leaves the user's markets or becomes not-fire-work**: PUT and DELETE return 404; the stored tag remains and is still counted in the report. Test in Tasks 2 and 3.
4. **Score 100 and score 0** must land in 90–100 and 0–9. Test in Task 3.
5. **A tag menu inside a stretched-link row**: opening it, picking an item, or pressing Escape must not navigate to the lead. Test in Task 6.

---

### Task 1: First-seen rule and DTO fields

**Files:**
- Create: `apps/api/Domain/Scoring/FirstSeen.cs`
- Modify: `apps/api/Domain/Scoring/StoredPermit.cs:30-32`
- Modify: `apps/api/Features/Leads/LeadQueries.cs` (`ToRow`, `ToSummary`, `LeadRow`)
- Modify: `apps/api/Features/Leads/LeadsEndpoints.cs` (`GetLead` row projection and `LeadDetailDto` construction)
- Modify: `apps/api/Features/Shared/Contracts.cs` (`LeadSummaryDto`, `LeadDetailDto`)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/FirstSeenTests.cs`, `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/LeadFirstSeenTests.cs`

**Interfaces:**
- Produces: `public static DateTime? FirstSeen.Of(DateTime firstSeenAt, DateTime? goLiveCutoff)` in namespace `PermitTorch.Api.Domain.Scoring`.
- Produces: `LeadRow` gains trailing `DateTime PermitFirstSeenAt, DateTime? GoLiveCutoff` (plain columns: `o.Permit.FirstSeenAt`, `o.Permit.Source.RecencyFromFirstSeenSince`).
- Produces: `LeadSummaryDto` gains trailing `DateTime? FirstSeenAt = null, LeadOutcomeKind? MyOutcome = null`. `LeadDetailDto` gains the same two trailing parameters after `Source`. (`LeadOutcomeKind` is created in Task 2; in this task add the enum to `apps/api/Data/Enums.cs` now so the DTO compiles: `public enum LeadOutcomeKind { WorthContacting, AlreadyAwarded, WrongTrade, TooLate }`.)
- Produces: `LeadQueries.ToSummary(LeadRow row, DateTime nowUtc, LeadOutcomeKind? myOutcome = null)`.

- [ ] **Step 1: Write the failing tests**

`FirstSeenTests`: `Of(t, null)` is null; `Of(cutoff.AddSeconds(-1), cutoff)` is null; `Of(cutoff, cutoff)` equals `cutoff`; `Of(cutoff.AddDays(1), cutoff)` equals that value.

`LeadFirstSeenTests` (`[Collection("api")]`, seed like `SavedLeadsEndpointTests`): one source with `RecencyFromFirstSeenSince = cutoff` and permits at `cutoff - 1 day` and exactly `cutoff`; one source with no cutoff. Assert on `GET /api/leads` items and `GET /api/leads/{id}`: `firstSeenAt` is null for the before-cutoff and no-cutoff leads, and equals the permit's `FirstSeenAt` for the at-cutoff lead. Assert `GET /api/saved-leads` carries `lead.firstSeenAt` too.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~FirstSeen"`
Expected: build error, `FirstSeen` does not exist.

- [ ] **Step 3: Implement**

`FirstSeen.Of` returns `goLiveCutoff is { } c && firstSeenAt >= c ? firstSeenAt : null`. `StoredPermit.ForScoring` sets `AppearedInDataAt = FirstSeen.Of(p.FirstSeenAt, source.RecencyFromFirstSeenSince)`. `ToRow` and the `GetLead` row select the two plain columns only; no method call inside any EF projection, because `ToRow` is composed into other queries. `ToSummary` computes `FirstSeen.Of(row.PermitFirstSeenAt, row.GoLiveCutoff)` after the query has returned, and passes `myOutcome` through. `GetLead` passes `summary.FirstSeenAt, summary.MyOutcome` to `LeadDetailDto`. Leave `RescoringJob` line 102 untouched.

- [ ] **Step 4: Run tests**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~FirstSeen|FullyQualifiedName~ScoringEngineSourceSettings|FullyQualifiedName~Rescoring"`
Expected: PASS.

- [ ] **Step 5: Commit** — `Carry the first-seen date on lead reads`

### Task 2: Lead outcome table and endpoints

**Files:**
- Modify: `apps/api/Data/Entities.cs` (add `LeadOutcome`), `apps/api/Data/AppDbContext.cs`
- Create: migration `AddLeadOutcomes` (`dotnet ef migrations add AddLeadOutcomes --project apps/api`)
- Create: `apps/api/Features/LeadOutcomes/LeadOutcomeEndpoints.cs`, `apps/api/Features/LeadOutcomes/MyOutcomes.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs` (register), `LeadsEndpoints.cs` (`GetLeads`, `GetLead`), `SavedLeadsEndpoints.cs` (`GetSavedLeads`) to fill `MyOutcome`
- Modify: `apps/api/tests/PermitTorch.Api.Tests/Data/MigrationTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/LeadOutcomes/LeadOutcomeEndpointTests.cs`

**Interfaces:**
- Consumes: `LeadOutcomeKind`, `ToSummary(row, nowUtc, myOutcome)` from Task 1.
- Produces: entity `LeadOutcome { Guid Id; Guid UserId; Guid FireOpportunityId; LeadOutcomeKind Outcome; int ScoreAtTag; DateTime CreatedAt; DateTime UpdatedAt; }`, `DbSet<LeadOutcome> LeadOutcomes`, table `lead_outcomes`, unique `(user_id, fire_opportunity_id)`, index `fire_opportunity_id`, both FKs cascade.
- Produces: `public sealed record SetLeadOutcomeRequest(string? Outcome)`; `public sealed record LeadOutcomeDto(LeadOutcomeKind Outcome, DateTime UpdatedAt)`.
- Produces: `public static Task<Dictionary<Guid, LeadOutcomeKind>> MyOutcomes.ForAsync(AppDbContext db, Guid userId, IReadOnlyCollection<Guid> opportunityIds, CancellationToken ct)`.
- Routes: `PUT /api/leads/{id:guid}/outcome`, `DELETE /api/leads/{id:guid}/outcome`, group `RequireAuthorization("User").AddEndpointFilter<TermsAcceptedFilter>()`.

- [ ] **Step 1: Write the failing tests**

`LeadOutcomeEndpointTests`, seeded like `SavedLeadsEndpointTests` (entitled lead at score 88, foreign lead at 90, plus a second user in the same market):
- `Put_sets_then_changes_the_tag_and_stores_the_server_score`: PUT `WRONG_TRADE` → 200, body `outcome == "WRONG_TRADE"`; row `ScoreAtTag == 88`. Update the opportunity's `LeadScore` to 61 in the DB, PUT `WORTH_CONTACTING` → row `Outcome == WorthContacting`, `ScoreAtTag == 61`, `UpdatedAt` advanced, still one row.
- `Put_ignores_a_score_in_the_body`: PUT `{ outcome: "TOO_LATE", scoreAtTag: 5 }` → row `ScoreAtTag == 88`.
- `A_rescore_does_not_move_a_stored_score`: tag, then set `LeadScore = 40` → row still 88.
- `Delete_clears_the_tag_and_is_204_when_none_exists`: DELETE twice, both 204, no row.
- `Unknown_or_missing_outcome_is_400`: `{ outcome: "MAYBE" }`, `{}`.
- `Outside_the_users_markets_is_404`: PUT and DELETE on the foreign lead → 404, no row created.
- `A_not_fire_work_lead_is_404_and_its_tag_is_kept`: tag, set `Standing = LeadStanding.NotFireWork`, PUT → 404 and DELETE → 404, row still present with its original outcome.
- `Reads_return_only_the_callers_tag`: user A tags `ALREADY_AWARDED`, user B tags nothing. A's `GET /api/leads`, `GET /api/leads/{id}`, and saved list (after saving) show `myOutcome == "ALREADY_AWARDED"`; B's show `myOutcome == null`.
- `Put_on_an_existing_row_never_conflicts`: seed a `LeadOutcome` row directly, PUT → 200 (covers Review Focus 2's update path).

`MigrationTests.Migrations_apply_cleanly_and_create_expected_indexes`: add `Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("lead_outcomes") && d.Contains("user_id") && d.Contains("fire_opportunity_id"));`

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~LeadOutcome|FullyQualifiedName~MigrationTests"`
Expected: build error, `LeadOutcome` does not exist.

- [ ] **Step 3: Implement entity, model config and migration**

Config mirrors `SavedLead` plus `.OnDelete(DeleteBehavior.Cascade)` on both FKs and `e.HasIndex(x => x.FireOpportunityId)`.

- [ ] **Step 4: Implement the endpoints**

PUT: parse `body.Outcome` with `Wire.TryParse<LeadOutcomeKind>` → else `ApiErrors.BadRequest("outcome must be WORTH_CONTACTING, ALREADY_AWARDED, WRONG_TRADE or TOO_LATE")`. Load the lead through `LeadQueries.ForEntitledMarkets(db, marketIds).Where(o => o.Id == id).Select(o => (int?)o.LeadScore)`; null → `ApiErrors.NotFound("Lead not found")`. Find the caller's row. Insert sets `Id = Guid.NewGuid()`, `CreatedAt = UpdatedAt = DateTime.UtcNow`; update changes `Outcome`, `ScoreAtTag` and `UpdatedAt` only. Never leave a default `DateTime`, which Npgsql rejects. On `DbUpdateException` with `PostgresException { SqlState: "23505" }`, clear the tracker, reload the row and apply the update once. DELETE: same entitlement read, then `ExecuteDeleteAsync` on the caller's row, `Results.NoContent()`.

- [ ] **Step 5: Fill `MyOutcome` on reads**

`GetLeads` and `GetSavedLeads` call `MyOutcomes.ForAsync` with the page's ids and pass the value into `ToSummary`. `GetLead` does the same for one id.

- [ ] **Step 6: Run tests**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests`
Expected: PASS, whole suite.

- [ ] **Step 7: Commit** — `Let users tag a lead with an outcome`

### Task 3: Admin outcome report

**Files:**
- Create: `apps/api/Features/LeadOutcomes/OutcomeReport.cs`
- Modify: `apps/api/Features/LeadOutcomes/LeadOutcomeEndpoints.cs` (admin route), `apps/api/Features/Shared/Contracts.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/LeadOutcomes/OutcomeReportTests.cs` (pure banding), `.../LeadOutcomes/OutcomeReportEndpointTests.cs`

**Interfaces:**
- Produces: `public const int OutcomeReport.MinAnswersPerBand = 10;`
- Produces: `public static int OutcomeReport.BandOf(int score)` → `Math.Clamp(score, 0, 100) / 10` capped at 9.
- Produces: `public static LeadOutcomeReportDto OutcomeReport.Build(IEnumerable<(int ScoreAtTag, LeadOutcomeKind Outcome)> answers)`.
- Produces DTOs: `public sealed record OutcomeBandDto(int Low, int High, int WorthContacting, int AlreadyAwarded, int WrongTrade, int TooLate, int Total, double? WorthContactingShare);` `public sealed record LeadOutcomeReportDto(IReadOnlyList<OutcomeBandDto> Bands, int MinAnswersPerBand);` Bands are always ten, ordered `Low` 90 down to 0; `High` is 100 for the top band, else `Low + 9`.
- Route: `GET /api/admin/lead-outcomes?marketId={guid?}` on its own `MapGroup("/api/admin").RequireAuthorization("SuperAdmin")`, with no `TermsAcceptedFilter`, like the other admin routes.

- [ ] **Step 1: Write the failing tests**

`OutcomeReportTests`: `BandOf(100) == 9`, `BandOf(90) == 9`, `BandOf(89) == 8`, `BandOf(0) == 0`. `Build` with nine answers in a band → `Total == 9`, `WorthContactingShare == null`; with ten answers, four worth contacting → share `0.4`; an empty band → all zeros, share null; ten bands always returned in order.

`OutcomeReportEndpointTests`: Member → 403, Admin → 403, SuperAdmin → 200. Seed tags from a Member, an Admin and a SuperAdmin on leads in two markets: SuperAdmin's tag is absent from counts, Admin's and Member's present; `?marketId=` narrows to one market; an unknown `marketId` returns ten zero bands. A tag on a not-fire-work lead is still counted.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~OutcomeReport"`
Expected: build error.

- [ ] **Step 3: Implement**

The endpoint queries `db.LeadOutcomes` joined to `AppUsers` where `Role != UserRole.SuperAdmin`, optionally filtered by `FireOpportunity.Permit.Source.MarketId == marketId`, projects `(ScoreAtTag, Outcome)`, and returns `OutcomeReport.Build(...)`. No `ForEntitledMarkets`: the report counts every stored answer.

- [ ] **Step 4: Run tests** — `dotnet test apps/api/tests/PermitTorch.Api.Tests` → PASS.

- [ ] **Step 5: Commit** — `Add an admin report of lead outcomes by score band`

### Task 4: Shared types and API client

**Files:**
- Modify: `packages/types/src/index.ts`, `apps/web/lib/api.ts`, `apps/web/lib/fixtures/leads.ts`, `apps/web/lib/fixtures/saved.ts`
- Test: `apps/web/__tests__/lib/api.test.ts`

**Interfaces:**
- Produces in `@permittorch/types`: `export type LeadOutcome = "WORTH_CONTACTING" | "ALREADY_AWARDED" | "WRONG_TRADE" | "TOO_LATE";` `LeadSummary` gains `firstSeenAt: string | null; myOutcome: LeadOutcome | null;` (LeadDetail inherits). `export interface OutcomeBand { low: number; high: number; worthContacting: number; alreadyAwarded: number; wrongTrade: number; tooLate: number; total: number; worthContactingShare: number | null; }` `export interface LeadOutcomeReport { bands: OutcomeBand[]; minAnswersPerBand: number; }`
- Produces in `lib/api.ts`: `setLeadOutcome(leadId: string, outcome: LeadOutcome, token: string): Promise<void>` (PUT), `clearLeadOutcome(leadId: string, token: string): Promise<void>` (DELETE), `getLeadOutcomeReport(token: string, marketId?: string): Promise<LeadOutcomeReport>`.

- [ ] **Step 1: Failing tests** in `api.test.ts`: `setLeadOutcome` sends `PUT /api/leads/<id>/outcome` with body `{"outcome":"WRONG_TRADE"}`; `clearLeadOutcome` sends `DELETE`; `getLeadOutcomeReport(t, "m1")` requests `/api/admin/lead-outcomes?marketId=m1`.
- [ ] **Step 2: Run** `pnpm --dir apps/web test api.test` → FAIL.
- [ ] **Step 3: Implement**; add `firstSeenAt: null, myOutcome: null` to every fixture lead.
- [ ] **Step 4: Run** `pnpm typecheck && pnpm --dir apps/web test` → PASS.
- [ ] **Step 5: Commit** — `Add lead outcome and first-seen fields to the web contract`

### Task 5: First-seen display

**Files:**
- Modify: `apps/web/components/app/leads/lead-table.tsx` (Filed cell), `apps/web/app/app/leads/[id]/page.tsx:122`
- Test: `apps/web/__tests__/app/lead-table.test.tsx`, `apps/web/__tests__/app/lead-detail-page.test.tsx`

**Interfaces:** Consumes `LeadSummary.firstSeenAt` from Task 4.

- [ ] **Step 1: Failing tests.** Lead table, both `compact` and full: with `firstSeenAt: "2026-10-03T…"` the Filed cell contains "First seen" and `formatDate` of it; with null it does not contain "First seen". Detail page: shows a "First seen" field with the calendar date when set; omits it when null; never renders "First discovered".
- [ ] **Step 2: Run** `pnpm --dir apps/web test lead-table lead-detail-page` → FAIL.
- [ ] **Step 3: Implement.** Third line in the Filed cell: `<div className="text-xs text-stone-500">First seen {formatDate(lead.firstSeenAt)}</div>` when set. Replace the "First discovered" `Field` with `{lead.firstSeenAt && <Field label="First seen" value={formatDate(lead.firstSeenAt)} />}`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `Show when PermitTorch first saw a permit`

### Task 6: Tag control on tables, saved list and detail page

**Files:**
- Create: `apps/web/components/app/lead-outcome/outcome-labels.ts`, `apps/web/components/app/lead-outcome/outcome-control.tsx`
- Modify: `lead-table.tsx` (Score cell), `components/app/saved/saved-list.tsx` (beside `ScoreBadge`), `app/app/leads/[id]/page.tsx` (beside `SaveButton`)
- Test: `apps/web/__tests__/app/outcome-control.test.tsx`, plus additions to `lead-table.test.tsx`, `saved-list.test.tsx`, `lead-detail-page.test.tsx`

**Interfaces:**
- Consumes: `setLeadOutcome`, `clearLeadOutcome`, `LeadOutcome` (Task 4); `useApiToken`, `reportMutationError` (existing).
- Produces: `export const OUTCOME_LABELS: Record<LeadOutcome, string>` with the Global Constraints labels in order worth contacting, already awarded, wrong trade, too late. `export function OutcomeControl({ leadId, outcome }: { leadId: string; outcome: LeadOutcome | null })`, a `"use client"` component.

Behaviour: the trigger is the label chip when tagged and a small outlined "Tag" button when not, `aria-label="Lead outcome"`. It opens a shadcn `DropdownMenu` listing the four labels with the current one checked, plus "Clear" when tagged. Choosing the current tag or "Clear" clears it. Updates are optimistic and roll back with `reportMutationError(err, "Could not update the lead tag")`. A ref tracks the latest requested value; a response for an older request is ignored, and a failure reverts to the last confirmed value. The trigger's wrapper is `relative z-[1]` (the sticky header is `z-10` and must paint over it while scrolling) and calls `e.stopPropagation()` on click, so the stretched link underneath never fires.

- [ ] **Step 1: Failing tests** in `outcome-control.test.tsx` (mock `@/lib/api`, `useApiToken`):
  - Untagged shows "Tag"; choosing "Wrong trade" calls `setLeadOutcome(id, "WRONG_TRADE", token)` and shows "Wrong trade".
  - Choosing the current tag calls `clearLeadOutcome`; so does "Clear".
  - A rejected call reverts the label and calls `reportMutationError`.
  - Choosing "Too late" then "Worth contacting" before the first resolves ends on "Worth contacting" even if the first resolves last.
  - Rendered inside a `LeadTable` row: opening the menu and choosing an item does not trigger navigation (assert the mocked `next/navigation` router and the row link's click handler were not called; `fireEvent.click` on the trigger does not reach the `<a>`).
  - Saved list and detail page render the control with the item's `myOutcome`.
- [ ] **Step 2: Run** `pnpm --dir apps/web test outcome-control lead-table saved-list lead-detail-page` → FAIL.
- [ ] **Step 3: Implement** the labels file and the control; place it under the `ScoreBadge` in the table's Score cell, beside the `ScoreBadge` in the saved list, beside `SaveButton` on the detail page.
- [ ] **Step 4: Run** `pnpm typecheck && pnpm --dir apps/web test` → PASS.
- [ ] **Step 5: Commit** — `Add the lead tag menu to lead lists and the lead page`

### Task 7: Admin report page

**Files:**
- Create: `apps/web/app/app/admin/outcomes/page.tsx`, `apps/web/components/app/admin/outcome-report-table.tsx`
- Modify: `apps/web/components/app/sidebar.tsx` (`ADMIN_NAV`: `{ href: "/app/admin/outcomes", label: "Lead tags", icon: Tags }` after Removals)
- Test: `apps/web/__tests__/app/outcome-report.test.tsx`, `apps/web/__tests__/app/sidebar.test.tsx`

**Interfaces:** Consumes `getLeadOutcomeReport`, `getMarkets`, `requireSuperAdmin`, `handleApiError`, `BAND_LABELS` and `scoreBand` from `score-badge`/`format`.

Page: title "Lead tags", one-line description "How contractors tagged leads, by the score each lead had when it was tagged. Staff tags are not counted.", a market select that sets `?marketId=` (searchParams), and the table. Table columns: Score band, Badge, Worth contacting, Already awarded, Wrong trade, Too late, Total, Share worth contacting. The Badge column is `BAND_LABELS[scoreBand(band.low)]`. The share cell is a whole percent when `worthContactingShare` is not null; "Too few answers to read" when `0 < total < minAnswersPerBand`; "—" when `total == 0`.

- [ ] **Step 1: Failing tests:** ten rows render with 90–100 first; share renders "40%" for 0.4; a band with total 9 shows "Too few answers to read"; a band with total 0 shows "—"; the 70–79 row's badge reads "High" and 60–69 reads "Monitor"; the admin sidebar includes "Lead tags".
- [ ] **Step 2: Run** `pnpm --dir apps/web test outcome-report sidebar` → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `pnpm typecheck && pnpm --dir apps/web test` → PASS.
- [ ] **Step 5: Commit** — `Add the admin lead tags report page`

### Task 8: End-to-end and final verification

**Files:**
- Create: `e2e/tests/lead-outcome.spec.ts` (follow `e2e/tests/saved.spec.ts` for login and lead seeding helpers)

- [ ] **Step 1: Write the test:** sign in, open `/app/leads`, open the first row's "Lead outcome" menu, choose "Wrong trade", wait for the `PUT /api/leads/<id>/outcome` response, assert the URL is still `/app/leads`, reload, and see "Wrong trade" on that row. Then choose "Clear" and see "Tag" after reload.
- [ ] **Step 2: Run** `pnpm e2e lead-outcome` → PASS. If the local stack cannot sign in (placeholder Firebase keys), record that the spec was written but not run, and say so in the PR.
- [ ] **Step 3: Run everything:** `dotnet test apps/api/tests/PermitTorch.Api.Tests && pnpm typecheck && pnpm test` → all PASS.
- [ ] **Step 4: Commit** — `Cover lead tagging end to end`
