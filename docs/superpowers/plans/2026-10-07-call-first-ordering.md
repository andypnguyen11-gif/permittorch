# Call-First Lead Ordering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The lead list answers "which permit should a contractor call first today": building permits whose record says the fire work is still ahead come first, ordered by freshness, then a named general contractor before nobody listed, then project value. Permits that are the fire work itself rank below. Records that describe no fire-protection work are hidden.

**Architecture:** The Apify provider resolves each record's city permit type against a per-source list and hands the domain one fact: `PermitScope.FireWorkPermit` or `PermitScope.BuildingPermit`. A new domain reader, `FireWorkReader`, reads the description and decides whether the record says the fire work is ahead, only mentions it, or describes no fire work. The scoring engine combines scope, reading, contractor status and permit status into a persisted `LeadStanding`, plus the last activity date and a reason that quotes the record. The feed and the digest sort on those stored columns. Weights are unchanged.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 with Npgsql, xUnit with Testcontainers Postgres, Next.js 15, Vitest.

**Spec:** The owner's decisions in the 2026-10-07 session, restated as the rules below. Evidence: a simulation of these exact rules against production (all 14,730 leads), summarised in "Revised top 15".

## Global Constraints

- Do not change scoring weights. The 0–100 score and its `LeadSignal`s stay as they are, apart from fire-work permits that name a contractor becoming `FireContractorNamed`.
- The domain never sees a city's permit-type spelling. Only `Infrastructure/Apify` reads it.
- Adding a city's fire-work permit types is a change to `source-permit-types.json` only.
- Reasons say what the record shows. Use "no fire-protection firm named", never "not a fire-protection firm". Never "awarded", "unassigned", "job open". Quotes are the record's own words, lowercased and whitespace-collapsed.
- A reason never contains a person's or company's name. Names can be removed on request, and reasons are stored text.
- Reasons use absolute dates ("Issued Oct 6, 2026."), never relative ones. They are stored and must not go stale.
- An admin's manual category (`CategoryOverridden`) is never hidden as not fire work.
- `NormalizedPermit` and `ScoreResult` are locked positional shapes. New members are trailing and optional.
- EF Core parameterized queries only. No new infrastructure.
- Commit messages: imperative, no task numbers, no pull request numbers, no co-author line.
- Run API tests with `dotnet test apps/api/PermitTorch.sln` (Docker running). Web tests: `pnpm --filter web test`.

## Ordering rules (the spec)

`LeadStanding`, best first. The feed sorts by standing, then `LastActivityOn` descending (nulls last), then `OtherContractorNamed` before anything else, then `EstimatedValue` descending (nulls last), then `LeadScore` descending, then `Id`.

| Value | Standing | When |
|---|---|---|
| 0 | `FireWorkAhead` | Not a fire-work permit, no fire firm named, not closed, record says the fire work is ahead |
| 1 | `FireWorkMentioned` | Same, but the record only mentions fire work |
| 2 | `FireWorkPermitNoContractor` | The permit is the fire work and names no contractor |
| 3 | `InspectionOrViolation` | An inspection or violation record |
| 4 | `FireWorkPermitContractorNamed` | The permit is the fire work and names a contractor (or a withheld one) |
| 5 | `FireFirmNamed` | Not a fire-work permit, and the named contractor is a fire-protection firm |
| 6 | `Closed` | Permit status is Closed |
| 7 | `NotFireWork` | The record describes no fire-protection work. Hidden everywhere |

Precedence when several apply: `NotFireWork` (unless the category is manual) → `Closed` → `InspectionOrViolation` → fire-work permit (2 or 4) → `FireFirmNamed` → 0 or 1.

## Revised top 15 (simulated on production, 2026-10-07)

All are building permits with a contractor listed and no fire-protection firm named. Reasons are exactly what the engine will store.

| # | Source | Score | Reason |
|---|---|---|---|
| 1 | Philadelphia | 100 | A contractor is listed; no fire-protection firm named. The record says "shall be fully sprinklered". Issued Oct 6, 2026. |
| 2 | Philadelphia | 100 | A contractor is listed; no fire-protection firm named. The record says "building to fully sprinklered". Issued Oct 6, 2026. |
| 3 | Philadelphia | 85 | A contractor is listed; no fire-protection firm named. The record says "separate permits required for any mep and fire suppression work". Issued Oct 6, 2026. |
| 4 | Philadelphia | 85 | … "separate permits required for any mep or fire suppression work". Issued Oct 6, 2026. |
| 5 | Philadelphia | 85 | … "separate permits required for mep and fire suppression work". Issued Oct 6, 2026. |
| 6 | Philadelphia | 80 | … "fire alarm work will be on a separate permit". Issued Oct 6, 2026. |
| 7 | Mesa | 95 | … "deferred fire sprinklers". Issued Oct 5, 2026. $748K declared value. |
| 8 | Philadelphia | 95 | … "building to be fully sprinklered". Inspected Oct 5, 2026. |
| 9 | Philadelphia | 95 | … "building to be fully sprinklered". Inspected Oct 5, 2026. |
| 10 | Philadelphia | 85 | … "separate permits required for electrical and fire suppression work". Issued Oct 2, 2026. |
| 11 | Philadelphia | 85 | … "building to be fully sprinklered". Issued Oct 2, 2026. |
| 12 | Philadelphia | 70 | … "to be fully sprinklered". Inspected Oct 2, 2026. |
| 13 | Mesa | 100 | … "deferred fire sprinklers". Issued Oct 1, 2026. $823K declared value. |
| 14 | Philadelphia | 85 | … "separate permits required for mep and fire suppression work". Issued Oct 1, 2026. |
| 15 | Philadelphia | 85 | … "building to be fully sprinklered". Issued Oct 1, 2026. |

Where the other groups land:

| Group | Result |
|---|---|
| NYC plumber- or installer-named sprinkler filings (1,346) | Standing 4; best rank 7,747 |
| Fire-department permits naming no one (Colorado Springs, Omaha, Atlanta, Tulsa: 1,088) | Standing 2; best rank 2,700. Reason: "This is the fire-sprinkler permit, and it names no contractor." |
| Hot work (56) | 55 hidden. The one kept is a Baltimore building with an impaired sprinkler system |
| Lawn sprinklers (25), "not sprinklered" with no fire work (30 of 31), existing sprinklers with no new fire work (78) | Hidden |
| Electrical-service permits with no fire-alarm-system or fire-pump work | Hidden. Those that install a fire alarm system stay, as standing 1 |

Standing counts: Ahead 402 · Mentioned 2,276 · fire-work permit no contractor 5,044 · fire-work permit contractor named 3,439 · fire firm named 276 · closed 2,810 · hidden 483.

## Review Focus

1. **A description with no ` | ` parts** (SF violations, Virginia Beach). Scope resolves from the all-fire list or defaults to building; nothing throws. Test in Task 1.
2. **A manually categorised lead whose text reads as not fire work.** It stays visible. Test in Task 3.
3. **A lead with no activity dates.** `LastActivityOn` is null and it sorts after dated leads in its standing. Test in Task 3 and Task 5.
4. **A withheld contractor on a fire-work permit.** Standing 4, and the reason holds no name. Test in Task 3.
5. **A future filed or issued date.** It is not activity: not used for `LastActivityOn` or the reason. Test in Task 3.

## File Structure

| File | Responsibility |
|---|---|
| `apps/api/Infrastructure/Apify/source-permit-types.json` | Create. Per-source list: sources whose every record is fire work, and each other source's fire-work type values |
| `apps/api/Infrastructure/Apify/SourcePermitTypes.cs` | Create. Loads the list, resolves a record's `PermitScope` |
| `apps/api/Data/Enums.cs` | Modify. Add `PermitScope`, `LeadStanding` |
| `apps/api/Domain/Normalization/PermitNormalizer.cs` | Modify. Trailing `PermitScope? Scope` on `NormalizedPermit`; set from `SourcePermitTypes` |
| `apps/api/Domain/Scoring/FireWorkReader.cs` | Create. Reads a description: ahead / mentioned / not fire work, with the quote |
| `apps/api/Domain/Scoring/ScoringEngine.cs` | Modify. Contractor status on fire-work permits, standing, last activity, new reason |
| `apps/api/Domain/Scoring/StoredPermit.cs`, `StoredScore.cs` | Modify. Carry scope; write standing and last activity |
| `apps/api/Data/Entities.cs` + new migration | Modify. `Permit.Scope`, `FireOpportunity.Standing`, `FireOpportunity.LastActivityOn`, index |
| `apps/api/Jobs/IngestionJob.cs`, `Jobs/RescoringJob.cs` | Modify. Persist the new fields; rescoring re-resolves scope from the stored description |
| `apps/api/Features/Leads/LeadQueries.cs` | Modify. Hide `NotFireWork`; new `OrderForFeed` |
| `apps/api/Features/Markets/MarketsEndpoints.cs` | Modify. Public counts skip `NotFireWork` |
| `apps/api/Features/EmailDigests/DigestService.cs` | Modify. Same order, standings 0–2, no score gate |
| `apps/web/app/app/alerts/page.tsx` | Modify. Digest description copy |
| `docs/deploy.md` | Modify. This release needs one full rescore |

---

### Task 1: Per-source fire-work permit types

**Files:**
- Create: `apps/api/Infrastructure/Apify/source-permit-types.json`, `apps/api/Infrastructure/Apify/SourcePermitTypes.cs`
- Modify: `apps/api/Data/Enums.cs`, `apps/api/Domain/Normalization/PermitNormalizer.cs`, `apps/api/PermitTorch.Api.csproj` (embed the JSON)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/SourcePermitTypesTests.cs`, `apps/api/tests/PermitTorch.Api.Tests/Domain/PermitNormalizerTests.cs`

**Interfaces:**
- Produces: `public enum PermitScope { FireWorkPermit, BuildingPermit }` in `PermitTorch.Api.Data`.
- Produces: `public static class SourcePermitTypes { public static PermitScope? Resolve(string? sourceId, string? description); }`. Null only when `sourceId` is blank.
- Produces: `NormalizedPermit` trailing member `PermitScope? Scope = null`.

JSON content (values from production, 2026-10-07):

```json
{
  "allFireWork": [
    "nyc-dobnow-permits", "sf-fire-permits", "sf-fire-inspections", "sf-fire-violations",
    "detroit-bseed-fire-alarm-permits", "sacramento-fire-permits-current", "sacramento-fire-permits-archive",
    "virginia-beach-fire-permits", "cosprings-fire-permits", "omaha-fire-permits", "atlanta-fire-permits",
    "tulsa-fire-permits", "seattle-trade-permits", "kcmo-issued-permits"
  ],
  "fireWorkTypes": {
    "philly-permits": ["Fire Suppression Permit"],
    "boston-building-permits": ["Electrical Fire Alarms", "Fire Alarm", "Fire Protection/Sprinkler"],
    "miami-building-permits": ["ELECTRICAL FOR FIRE ALARM", "EXISTING FIRE ALARM SYSTEM", "NEW FIRE ALARM SYSTEM",
      "EXISTING FIRE SPRINKLER SYSTEM", "NEW FIRE SPRINKLER SYSTEM", "FIRE PUMP", "JOCKEY PUMP", "STANDPIPE",
      "FIRE DEPARTMENT CONNECTION", "HOOD FIRE SUPPRESSION SYSTEM - MECHANICAL", "WATER SUPPLY / FIRELINE"],
    "portland-bds-permits": ["Fire Systems Permit"],
    "charlotte-accela-permits": ["Fire"],
    "louisville-construction-permits": ["Range Hood Suppression"],
    "austin-construction-permits": ["Fireline"],
    "sanantonio-permits": ["New Sprinkler Installation", "Fire pump TOPS", "Fire alarm power"]
  }
}
```

Resolution rule: a source in `allFireWork` → `FireWorkPermit`. Otherwise split the description on `" | "`, drop the first part (the work description), split each remaining part on `"|"`, trim, and compare case-insensitively with the source's `fireWorkTypes`. Any match → `FireWorkPermit`. A source with no entry, or no match → `BuildingPermit`. The scraper appends the city's own type fields to the description after `" | "`, which is why only trailing parts count.

- [ ] **Step 1: Write the failing tests** in `SourcePermitTypesTests`:
  - `Philly_FireSuppressionPermit_IsFireWork`: `Resolve("philly-permits", "FOR THE INSTALLATION OF 91 NEW PENDENT SPRINKLERS | Fire Suppression Permit | Addition and/or Alterations")` → `FireWorkPermit`.
  - `Philly_BuildingAndElectricalPermits_AreBuilding`: `"… | Commercial Building Permit | New Construction"` and `"… | Electrical Permit | New Construction"` → `BuildingPermit`.
  - `Boston_WorkTypeOrPermitType_IsFireWork`: `"Installation of Fire Alarm | Fire Alarm | Electrical Fire Alarms"` and `"… | Fire Protection/Sprinkler | Long Form/Alteration Permit"` → `FireWorkPermit`; `"… | Renovations - Interior NSC | Short Form Bldg Permit"` → `BuildingPermit`.
  - `Miami_PipeListedItems_MatchIndividually`: `"NEW CONSTRUCTION | BACKFLOW PREVENTER|NEW FIRE SPRINKLER SYSTEM|STANDPIPE"` → `FireWorkPermit`; `"NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM"` → `BuildingPermit`.
  - `AllFireWorkSource_IsFireWork_EvenWithoutParts`: `Resolve("sf-fire-violations", "Fire alarm not maintained")` → `FireWorkPermit`.
  - `TypeNameInWorkDescription_DoesNotCount`: `Resolve("philly-permits", "Fire Suppression Permit to follow | Commercial Building Permit | Addition and/or Alteration")` → `BuildingPermit`.
  - `MatchIsCaseInsensitive`: `"… | fire suppression permit | …"` → `FireWorkPermit`.
  - `UnknownSource_IsBuilding` and `BlankSource_IsNull`.
  - `EverySourceIdInTheList_IsLowercaseKebab`: every key and `allFireWork` entry matches `^[a-z0-9]+(-[a-z0-9]+)*$`.
  - In `PermitNormalizerTests`: `Normalize_SetsScopeFromSourceAndDescription` (a raw record with `Source.SourceId = "nyc-dobnow-permits"` → `Scope == FireWorkPermit`).
- [ ] **Step 2: Run them, expect compile failure.** `dotnet test apps/api/PermitTorch.sln --filter "SourcePermitTypesTests|Normalize_SetsScope"`.
- [ ] **Step 3: Implement.** Embed the JSON as a manifest resource and parse it once with `System.Text.Json` into a lazy static. Add the enum, the trailing `Scope` member, and set `Scope: SourcePermitTypes.Resolve(raw.Source?.SourceId, raw.Description)` in `Normalize`.
- [ ] **Step 4: Run the tests, expect PASS.**
- [ ] **Step 5: Commit** `Resolve whether a permit is the fire work from each source's permit types`.

### Task 2: Fire-work reader

**Files:**
- Create: `apps/api/Domain/Scoring/FireWorkReader.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/FireWorkReaderTests.cs`

**Interfaces:**
- Consumes: `PermitScope` (Task 1).
- Produces: `public enum FireWorkVerdict { Ahead, Mentioned, NotFireWork }`, `public sealed record FireWorkReading(FireWorkVerdict Verdict, string? Quote)`, and `public static class FireWorkReader { public static FireWorkReading Read(string? description, PermitScope? scope); }`.

Rules, applied to the whole description in this order (all case-insensitive):

1. Not fire work, any scope:
   - `hot\s*work`, when no fire-protection term (rule 4) appears.
   - `lawn\s*sprinkl|landscape\s*sprinkl|irrigation`, when none of `fire\s*sprinkl|fire\s*alarm|standpipe` appears.
   - A negation with no ahead phrase: `\bno\s+(fire\s*alarm|sprinkler)s?\s+(work\s+)?(on|under|in)\s+this\s+permit` or `(fire\s*alarm|sprinkler)[^.]{0,30}\bnot\s+(included|part\s+of)\b`.
2. A fire-work permit (`scope == FireWorkPermit`) stops here: `Mentioned`, quoting the first fire-protection term's clause.
3. Ahead, first match wins. Quote the match, lowercased with whitespace collapsed:
   - `deferred[^.|]{0,60}?(13r\s+|nfpa\s*13\s+)?(fire\s+sprinklers?|fire\s+alarms?|sprinklers?)`
   - `(shall|to|will)\s+be\s+fully\s+sprinklered|building\s+to\s+(be\s+)?fully\s+sprinklered`
   - `separate\s+permits?\s+(is\s+|are\s+)?required\s+for[^.|]{0,60}?(fire\s+suppression|sprinkler|fire\s+alarm)(\s+work)?`
   - `(sprinkler|fire\s+alarm|fire\s+suppression)[^.|]{0,40}?(under|by|on)\s+(a\s+)?separate\s+permit`
4. Not fire work (not a fire-work permit, nothing ahead):
   - `\bnot\s+sprinklered|\bnon-?sprinklered|\bunsprinklered`, with no fire-work action: `(install|add|relocat|modif|replac|extend|new)\w*\s+[^.]{0,30}(sprinkler|fire\s*alarm|standpipe)`.
   - `existing\s+(building\s+)?(is\s+)?(fully\s+)?sprinklered|building\s+is\s+fully\s+sprinklered|existing\s+sprinkler\s+system\s+to\s+remain`, with no fire-work action.
   - No fire-protection term at all. Terms: `(?<!lawn )(?<!landscape )sprinkl|fire\s*alarm|fire\s*detection|\bfacp\b|nfpa\s*(13|72)|standpipe|fire\s*pump|suppress|kitchen\s*hood|\bhood\b|\bansul\b|fire\s*(service\s*)?line\b|fire\s*protection`.
   - Electrical service with no alarm-system work: `\d+\s*-?\s*amp\s+service|service\s+upgrade|meter\s+(socket|bank|base)|electrical\s+service` and none of `fire\s*alarm\s+(system|panel|control)|install\w*\s+(a\s+|the\s+)?(new\s+|complete\s+)?fire\s*alarm|nfpa\s*72|\bfacp\b|fire\s*pump|sprinkler\s+(system|heads?)`.
5. Otherwise `Mentioned`. The quote is the clause holding the first fire-protection term: bounded by `.`, `;`, `|` or a line break, at most 80 characters, lowercased, whitespace collapsed.

- [ ] **Step 1: Write the failing tests.** Each uses the production text and asserts the verdict, plus the exact quote for `Ahead`:
  - `Mesa_DeferredFireSprinklers_IsAhead`: `"Tenant improvement … New plumbing fixtures. Deferred fire sprinklers. Results in CofO. | COM"`, building → `Ahead`, `"deferred fire sprinklers"`.
  - `Mesa_Deferred13R_IsAhead` → quote `"deferred 13r fire sprinklers"`.
  - `Philly_ShallBeFullySprinklered_IsAhead` → `"shall be fully sprinklered"`.
  - `Philly_BuildingToFullySprinklered_IsAhead` → `"building to fully sprinklered"`.
  - `Philly_SeparatePermitsForFireSuppression_IsAhead`: `"SEPARATE PERMITS REQUIRED FOR MEP AND FIRE SUPPRESSION WORK."` → `"separate permits required for mep and fire suppression work"`.
  - `FireAlarmOnSeparatePermit_IsAhead`: `"fire alarm work will be on a separate permit"`.
  - `HotWork_IsNotFireWork_EvenOnFireWorkPermit`: `"hot work operations, welder, cut, weld, grind, braze, solder"`, scope `FireWorkPermit` → `NotFireWork`.
  - `HotWorkWithImpairedSprinklers_IsKept`: the Baltimore text (`"…Firewatch and Hot Work code compliance due to impairment of water supply to installed sprinkler system…"`) → `Mentioned`.
  - `LawnSprinkler_IsNotFireWork`: `"NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM"` → `NotFireWork`.
  - `NotSprinklered_IsNotFireWork`: `"… EXISTING BUILDING IS NOT SPRINKLERED. ALL WORK TO BE DONE PER APPROVED PLANS …"` → `NotFireWork`.
  - `ExistingSprinklersNoNewWork_IsNotFireWork`: `"LEVEL II INTERIOR ALTERATIONS … EXISTING BUILDING FULLY SPRINKLERED."` → `NotFireWork`.
  - `ExistingSprinklersWithRelocation_IsMentioned`: `"… EXISTING BUILDING FULLY SPRINKLERED. RELOCATE SPRINKLER HEADS AS REQUIRED."` → `Mentioned`.
  - `NoFireAlarmOnThisPermit_IsNotFireWork` and `FireAlarmNotIncluded_IsNotFireWork` (`"(fire alarm system is not included in the scope of work)"`).
  - `ElectricalServiceOnly_IsNotFireWork`: `"Install 400 amp service equipment with grounding. Wiring throughout. Install receptacles, light fixtures, emergency lighting, smoke detectors | Electrical Permit | Addition and/or Alteration"` → `NotFireWork`.
  - `ElectricalServiceWithFireAlarmSystem_IsMentioned`: `"… 4 gang meter bank as per 2017 NEC. Install fire alarm as per 2016 NFPA 72. | Electrical Permit | New Construction"` → `Mentioned`.
  - `SmokeDetectorOnly_IsNotFireWork`: `"INSTALL HARDWIRE SMOKE DETECTOR | Electrical | Apartment"` → `NotFireWork`.
  - `FireWorkPermit_PlainType_IsMentioned`: `"Fire Sprinkler Permit | New Installation of Sprinkler System"`, scope `FireWorkPermit` → `Mentioned`.
  - `NullDescription_IsNotFireWork_ForBuilding_AndMentioned_ForFireWork`.
- [ ] **Step 2: Run, expect compile failure.** `--filter FireWorkReaderTests`.
- [ ] **Step 3: Implement `FireWorkReader.Read`** with the patterns above, compiled with `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Read from the permit record whether the fire work is still ahead`.

### Task 3: Standing, last activity and record-based reasons in the scoring engine

**Files:**
- Modify: `apps/api/Data/Enums.cs`, `apps/api/Domain/Scoring/ScoringEngine.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/ScoringEngineStandingTests.cs` (new); update reason assertions in `ScoringEngineTests.cs` and `ScoringEngineContractorStatusTests.cs`

**Interfaces:**
- Consumes: `NormalizedPermit.Scope`, `FireWorkReader.Read`.
- Produces: `public enum LeadStanding { FireWorkAhead, FireWorkMentioned, FireWorkPermitNoContractor, InspectionOrViolation, FireWorkPermitContractorNamed, FireFirmNamed, Closed, NotFireWork }`.
- Produces: `ScoreResult` trailing members `LeadStanding Standing = LeadStanding.FireWorkMentioned, DateTime? LastActivityOn = null`.
- Changes: `ContractorStatusOf(permit)` returns `FireContractorNamed` when `permit.Scope == FireWorkPermit` and a contractor is named or withheld.

Reason copy, exact. Sentence one by standing:
- 0, 1 with `OtherContractorNamed`: `A contractor is listed; no fire-protection firm named.` With `NoContractorListed`: `No contractor listed.`
- 0: then `The record says "{quote}".` 1: then `The record mentions "{quote}".`
- 2: `This is the {kind} permit, and it names no contractor.` 4: `This is the {kind} permit, and it names a contractor.` `{kind}` is `fire-sprinkler` (FireSprinkler), `fire-alarm` (FireAlarm), `fire-suppression` (FireSuppression, KitchenSuppression), otherwise `fire-protection`.
- 3: `This is a fire inspection record.` or `This is a fire code violation record.`
- 5: `A fire-protection contractor is on this permit.`
- 6: `The permit is closed.`
- 7: `The record describes no fire-protection work.` Nothing follows.

Then, for 0–6: `{Filed|Issued|Inspected} {MMM d, yyyy}.` for the latest of the filed, issued and inspection dates not after now, ties going in that order. Then ` {value} declared value.` when `EstimatedValue` is set: under $1,000 → `$950`; under $1M → `$748K` (rounded to the nearest thousand); otherwise `$3.7M` (one decimal). `LastActivityOn` is that date at 00:00 UTC.

- [ ] **Step 1: Write the failing tests** in `ScoringEngineStandingTests`. Each builds a `NormalizedPermit` and asserts `Standing` and the exact `Reason`:
  - `BuildingPermit_GcNamed_DeferredSprinklers_IsAheadWithQuote` → `FireWorkAhead`, `A contractor is listed; no fire-protection firm named. The record says "deferred fire sprinklers". Issued Oct 5, 2026. $748K declared value.`
  - `BuildingPermit_NoContractor_MentionsAlarm_IsMentioned` → `FireWorkMentioned`, reason starts `No contractor listed. The record mentions "`.
  - `FireWorkPermit_NoName_RanksBelowOpenWork` → `FireWorkPermitNoContractor`, `This is the fire-sprinkler permit, and it names no contractor. Filed Oct 6, 2026.`, `ContractorStatus == NoContractorListed`, and no `FIRE_CONTRACTOR_ASSIGNED` signal.
  - `FireWorkPermit_PlumberNamed_IsFireContractorNamed` (contractor `"MAR-SAL PLBG & HTG, INC"`) → `FireWorkPermitContractorNamed`, `ContractorStatus == FireContractorNamed`, `FIRE_CONTRACTOR_ASSIGNED` signal present, reason `This is the fire-sprinkler permit, and it names a contractor. …` and does not contain `MAR-SAL`.
  - `FireWorkPermit_WithheldContractor_IsContractorNamed_AndReasonHasNoName` (`ContractorWithheld = true`).
  - `BuildingPermit_FireFirmNamed_IsFireFirmNamed` (`"Century Fire Protection"`).
  - `Inspection_IsInspectionOrViolation`, `Closed_IsClosed`.
  - `LawnSprinkler_IsNotFireWork_AndReasonSaysSo`.
  - `ManualCategory_IsNeverNotFireWork`: lawn-sprinkler text with `ClassificationResult(…, "manual")` → `FireWorkMentioned`.
  - `NoDates_LastActivityOnIsNull_AndReasonHasNoDateSentence`.
  - `FutureIssuedDate_IsNotActivity`: filed Oct 1, issued Nov 30, now Oct 7 → `LastActivityOn == 2026-10-01`, reason holds `Filed Oct 1, 2026.`
  - `Value_Formats`: 950 → `$950`, 748_000 → `$748K`, 3_687_172 → `$3.7M`.
  - `Reason_NeverUsesBannedWording`: across every test permit above, the reason contains none of `not a fire-protection firm`, `awarded`, `unassigned`.
- [ ] **Step 2: Run, expect failure.** `--filter "ScoringEngine"`.
- [ ] **Step 3: Implement.** Compute the reading, contractor status, standing (precedence above), `LastActivityOn` (reuse `LatestActivity`, truncated to `.Date`), and replace `BuildReason` with the copy above. Update the existing tests' expected reasons in `ScoringEngineTests.Reason_IsOneSentenceFromTopSignals`, `Reason_HandlesSingleAndDoubleSignalCounts`, and the two `Reason` assertions in `ScoringEngineContractorStatusTests` to the new copy for their inputs. Rename the two `ScoringEngineTests` methods to describe the new behaviour.
- [ ] **Step 4: Run all `Domain` tests, expect PASS.**
- [ ] **Step 5: Commit** `Rank leads by whether the record says the fire work is still ahead`.

### Task 4: Persist scope, standing and last activity

**Files:**
- Modify: `apps/api/Data/Entities.cs`, `apps/api/Data/AppDbContext.cs` (index), `apps/api/Domain/Scoring/StoredPermit.cs`, `apps/api/Domain/Scoring/StoredScore.cs`, `apps/api/Jobs/IngestionJob.cs`, `apps/api/Jobs/RescoringJob.cs`
- Create: migration `AddLeadStanding` via `dotnet ef migrations add AddLeadStanding --project apps/api`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Jobs/IngestionJobTests.cs`, `apps/api/tests/PermitTorch.Api.Tests/Jobs/RescoringJobTests.cs`

**Interfaces:**
- Produces: `Permit.Scope` (`PermitScope?`, column `scope`), `FireOpportunity.Standing` (`LeadStanding?`, column `standing`; null = not yet scored by this release), `FireOpportunity.LastActivityOn` (`DateTime?`, column `last_activity_on`), index `ix_fire_opportunities_standing_last_activity_on`.
- `StoredScore.Replace` also writes `Standing` and `LastActivityOn`. `StoredPermit.ToNormalized` passes `Scope`.
- `RescoringJob` loads `Permit.Source` and sets `permit.Scope = SourcePermitTypes.Resolve(permit.Source.Jurisdiction, permit.Description)` before scoring, so a change to the list takes effect at the next full pass. A change in scope, standing or last activity counts as a change.

- [ ] **Step 1: Write the failing tests.**
  - `IngestionJobTests.FireSuppressionPermitNamingContractor_IsStoredAsFireWorkPermit`: ingest a Philly record (`Source.SourceId = "philly-permits"`, description ending ` | Fire Suppression Permit | New Construction`, contractor `"B M CONSULTING SERVICES INC"`) → `permit.Scope == FireWorkPermit`, `opportunity.Standing == FireWorkPermitContractorNamed`, `opportunity.ContractorStatus == FireContractorNamed`.
  - `IngestionJobTests.MesaDeferred_IsStoredAhead_WithLastActivity`.
  - `RescoringJobTests.FullPass_BackfillsScopeAndStanding`: a stored Philly fire-suppression permit with `Scope = null` and `Standing = null` → after `RescoreOnceAsync(now, ct, fullPass: true)`, scope and standing are set and `changed == 1`.
  - `RescoringJobTests.FullPass_HidesLawnSprinkler`: a stored Miami lawn-sprinkler lead → `Standing == NotFireWork`.
- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement** the entity members, migration, index and job changes. In `IngestionJob`, set `permit.Scope = normalized.Scope ?? permit.Scope` on create and merge, and the two opportunity fields beside `ContractorStatus`.
- [ ] **Step 4: Run `Jobs` and `Data` tests (including `MigrationTests`), expect PASS.**
- [ ] **Step 5: Commit** `Store permit scope, lead standing and last activity`.

### Task 5: Feed order and hiding records that describe no fire work

**Files:**
- Modify: `apps/api/Features/Leads/LeadQueries.cs`, `apps/api/Features/Markets/MarketsEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/LeadOrderingTests.cs` (new), `apps/api/tests/PermitTorch.Api.Tests/Features/Markets/…` (existing market stats test file)

**Interfaces:**
- `ForEntitledMarkets` adds `o.Standing == null || o.Standing != LeadStanding.NotFireWork`. Every consumer (feed, detail, CSV, saved leads, digest) inherits it.
- `OrderForFeed`: `Standing` ascending with null after `Closed`, then `LastActivityOn` descending with nulls last, then `ContractorStatus == OtherContractorNamed` first, then `Permit.EstimatedValue` descending with nulls last, then `LeadScore` descending, then `Id`.
- Both market-stats queries add the same `NotFireWork` exclusion.

- [ ] **Step 1: Write the failing tests** in `LeadOrderingTests` (`GET /api/leads` as an entitled user, seeded through `TestSeed`):
  - `Feed_PutsFireWorkAheadFirst_ThenFreshness_ThenGc_ThenValue`. Seed: ahead/Oct 6/GC/no value; ahead/Oct 6/no contractor/$2M; ahead/Oct 5/GC/$9M; mentioned/Oct 7/GC; fire-work-no-contractor/Oct 7; fire-work-contractor-named/Oct 7. Expected id order is exactly that list.
  - `Feed_UndatedLeadSortsLastWithinStanding`.
  - `Feed_HidesNotFireWork_AndDetailReturns404`.
  - `Csv_HidesNotFireWork`.
  - `MarketStats_SkipNotFireWork`.
- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `Features` tests, expect PASS.** Fix any existing feed test that asserted score-first order by seeding standings that give the same order; do not weaken assertions.
- [ ] **Step 5: Commit** `Order the lead feed by open fire work, freshness, contractor and value`.

### Task 6: Digest uses the same order

**Files:**
- Modify: `apps/api/Features/EmailDigests/DigestService.cs`, `apps/web/app/app/alerts/page.tsx`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestServiceTests.cs`, `apps/web/__tests__/app/alerts*.test.tsx` (the existing alerts page test, if any; otherwise add `alerts-page.test.tsx`)

**Interfaces:**
- Subscriber digest: new leads since the last send (`FirstDetectedAt > since`), `Standing` in {`FireWorkAhead`, `FireWorkMentioned`, `FireWorkPermitNoContractor`}, ordered by `LeadQueries.OrderForFeed`, top 10. The `LeadScore >= 70` gate is removed. Sample-lead digests get the same standing filter and order.
- Alerts page copy: replace `Hot leads (score 90+) called out first, with score and project value` with `Permits whose record says the fire work is still ahead come first, newest first`.

- [ ] **Step 1: Write the failing tests.** `DigestServiceTests.Digest_OrdersByStandingThenFreshness_AndSkipsAwardedAndHidden`: seed an ahead lead (score 60), a mentioned lead (score 100), a fire-work-contractor-named lead and a not-fire-work lead, all new. The sent email lists the ahead lead before the mentioned lead and holds neither of the others. Web test: the alerts page renders the new sentence.
- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Send the digest in call-first order`.

### Task 7: Verify against production data and document the release

**Files:**
- Modify: `docs/deploy.md`

- [ ] **Step 1: Run every suite.** `dotnet test apps/api/PermitTorch.sln` and `pnpm --filter web test`, both green. Run `pnpm --filter web build`.
- [ ] **Step 2: Prove the top 15 on real data, locally.** Restore a production snapshot to the local database (port 5435), apply the migration, run the API once with `Rescoring__FullPassOnStartup=true` and `Pipeline__Enabled=false`, then query the feed order with SQL. Expected: the top 15 match the "Revised top 15" table (dates may shift by a day if the snapshot is newer). NYC installer-named filings and the no-name fire-department permits rank below every standing-0 row. No hot-work, lawn-sprinkler, "not sprinklered" or electrical-service-only lead is visible.
- [ ] **Step 3: Document the release** in `docs/deploy.md`: this release needs one full rescore (`Rescoring__FullPassOnStartup=true`, removed afterwards), with the SQL from Step 2 to confirm it.
- [ ] **Step 4: Commit** `Note the full rescore the call-first ordering needs`.
