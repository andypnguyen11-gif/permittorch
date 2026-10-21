# Lead outcome tags and the first-seen date

Date: 2026-10-21
Status: accepted 2026-10-21 with the owner's changes (non-goal narrowed, tag menu sits above the
row link on both tables, saved list shows the label and menu, both DTOs carry the new fields)
Repos: `Permit Torch` only. The scraper does not change: the first-seen clock is already stamped by
this app at import, and outcome tags are user input the scraper never sees.

## Goal

Pilot feedback (Reddit, 2026-10-21) asked for two things:

1. Let contractors mark a lead **already awarded**, **wrong trade**, **too late** or **worth
   contacting**, then compare those answers with the score bands. The question to answer is whether
   an 85 beats a 60 in practice, not whether the rules sound reasonable.
2. Keep "first seen by PermitTorch" separate from the permit's own dates, which matters most in the
   markets that publish monthly.

## Non-goals

- Tags never change the score, the order of the feed, or which leads appear. The label is meant to
  show. Any weight change is a decision the owner makes after reading the report.
- No filter to hide tagged leads. It can follow once the pilot shows people tag.
- Tags do not appear in the CSV export, in digests, or on any public page.
- Subscribers never see the report, anyone else's tags, or totals. A small pilot would let one
  contractor infer a competitor's judgment.
- Saved and Contacted on saved leads stay as they are. Tags are separate from saving.

## Part 1: outcome tags

### Data

New table `lead_outcomes`, entity `LeadOutcome`:

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | client-generated, like every other entity |
| `user_id` | uuid | FK `users`, cascade delete |
| `fire_opportunity_id` | uuid | FK `fire_opportunities`, cascade delete |
| `outcome` | integer enum `LeadOutcomeKind`, like every other enum column; SCREAMING_SNAKE on the wire | `WorthContacting`, `AlreadyAwarded`, `WrongTrade`, `TooLate` |
| `score_at_tag` | int | the lead's `LeadScore` when the tag was set or last changed |
| `created_at` | timestamptz | first set |
| `updated_at` | timestamptz | last changed |

Unique index on `(user_id, fire_opportunity_id)`: one tag per user per lead. Index on
`fire_opportunity_id` for the report join.

### API

Both routes sit in the `Leads` feature, behind the `User` policy and `TermsAcceptedFilter`, like the
rest of `/api/leads`.

- `PUT /api/leads/{id}/outcome` with body `{ "outcome": "WRONG_TRADE" }`. Upsert. The server reads the
  lead through `LeadQueries.ForEntitledMarkets` exactly as saving a lead does; a lead outside the
  user's markets is `404 Lead not found`. A missing or unknown outcome is `400`. The client never
  sends a score: the server copies the lead's current `LeadScore` into `score_at_tag` on every set
  and every change. Returns `200` with `{ outcome, updatedAt }`.
- `DELETE /api/leads/{id}/outcome`. Removes the caller's tag. `204` whether or not one existed, after
  the same entitlement check.

A user only ever reads or writes rows where `user_id` is their own id.

### Reads

`LeadSummaryDto` and `LeadDetailDto` both gain `MyOutcome` (`myOutcome: LeadOutcome | null` in
`@permittorch/types`), the caller's own tag. The detail record is separate from the summary, not an
extension of it, so the field is added to each. It is filled for `GET /api/leads`,
`GET /api/leads/{id}` and the saved-leads list. No read ever returns another user's tag.

### Web

- A small tag menu on each lead row, on the leads page, on the overview's compact table and on the
  saved list, and on the lead detail page. Each table row is one stretched link (`after:absolute
  after:inset-0` on the title), so the menu is positioned above that link (`relative z-10`) and
  choosing a tag never opens the lead. Choosing a tag sets it, choosing the
  current one again or "Clear" removes it. The menu updates optimistically and rolls back on error.
- The chosen tag shows as a quiet label next to the score badge. Labels: "Worth contacting",
  "Already awarded", "Wrong trade", "Too late". These are the user's own words about the lead, so
  "Already awarded" is acceptable here; the app's own copy still says what the record shows.
- No business rules in the web app: it renders `myOutcome` and calls the two routes.

## Part 2: admin report

`GET /api/admin/lead-outcomes?marketId=` under the existing super-admin group, and a page at
`/app/admin/outcomes` linked from the admin nav.

- Ten rows, one per ten-point band of `score_at_tag`: 90–100, 80–89, … , 0–9. A band is
  `min(score_at_tag / 10, 9)` with integer division, so 89 stays in 80–89 and 100 stays in 90–100.
- Each row: a count per tag, the total, and the share tagged worth contacting, which is
  worth-contacting answers divided by all answers in that band.
- A band with no answers shows no share. A band with fewer than **10** answers (`MinAnswersPerBand`,
  a constant) shows its counts, no share, and the note "Too few answers to read".
- Tags set by `UserRole.SuperAdmin` users are stored but left out of every count, because staff are
  entitled to every market and their tags are not contractor judgment. `Admin` (a customer
  company's admin) counts like `Member`.
- Optional `marketId` narrows to leads in that market. An unknown market id gives an empty report.
- The page shows the badge band beside each row (90–100 Hot, 80–89 Very High, 70–79 High, below 70
  Monitor) so the report reads in the same terms contractors see.

## Part 3: first seen by PermitTorch

The rule is the one scoring already uses for `AppearedInDataAt` (`StoredPermit.ForScoring`): the
lead's first-seen date is `Permit.FirstSeenAt` when the permit's source has
`RecencyFromFirstSeenSince` set **and** `FirstSeenAt >= RecencyFromFirstSeenSince`. Otherwise it is
null. The cutoff instant itself counts. Before the cutoff, `FirstSeenAt` is only a backfill's import
time and says nothing.

- `LeadSummaryDto` and `LeadDetailDto` both gain `FirstSeenAt` (`firstSeenAt: string | null`),
  computed in the API with that rule, never in the web app. The cutoff rule alone is extracted into
  one helper that `ForScoring` and the reads both call. The rescoring job's window check is a
  different comparison (`FirstSeenAt` against a rolling window, not the go-live cutoff) and is not
  folded into the helper.
- Lead tables (leads page and overview compact table): the Filed cell keeps its two lines, the calendar date and the relative line ("2 days
  ago"). When `firstSeenAt` is set, a third line reads "First seen" plus the calendar date.
- Lead detail: the "First discovered" field currently shows `FireOpportunity.FirstDetectedAt`, the
  ingestion run's clock. It is replaced by "First seen" with the calendar date of `firstSeenAt`, and
  the field is left out when that is null. `firstDetectedAt` stays in the contract for the "New"
  badge; it is no longer displayed.

## Testing

API (xUnit):

- Set, change and clear a tag; a change replaces the outcome and re-reads `score_at_tag`.
- The score stored is the server's, and a later rescore does not move it.
- A user cannot tag a lead outside their markets (404), and never sees another user's tag on list,
  detail or saved reads.
- Unknown outcome values are rejected (400).
- The report is 403 for Member and Admin, 200 for SuperAdmin.
- Banding: 100 and 90 land in 90–100, 89 in 80–89, 0 in 0–9.
- SuperAdmin tags are excluded; Admin tags are counted.
- The share is absent at 0 answers and at 9, present at 10.
- `marketId` narrows the counts.
- `myOutcome` and `firstSeenAt` are present on summary, detail and saved-list reads.
- First seen: null with no cutoff, null before the cutoff, set at exactly the cutoff and after it.
- Migration test covers the new table and unique index.

Web (Vitest):

- Tag menu sets, changes and clears, and rolls back on a failed call.
- Choosing a tag in a table row does not navigate to the lead, on the leads page, the overview and
  the saved list.
- The label renders for each outcome and not when `myOutcome` is null.
- The Filed cell shows the third line only when `firstSeenAt` is set.
- The detail page shows "First seen" only when `firstSeenAt` is set, and no longer shows
  "First discovered".
- The report table renders counts, hides the share below 10 answers, and shows "Too few answers to
  read".

E2E (Playwright): tag a lead from the list, reload, and see the label persist.
