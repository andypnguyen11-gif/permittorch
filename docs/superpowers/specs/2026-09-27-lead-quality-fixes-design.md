# Lead quality fixes — design

Date: 2026-09-27. Scope: the API pipeline and the lead detail page. The scraper is unchanged.

## Why

Measured on 9,080 production leads and about 11,500 raw scraper records:

- A lead that already names a fire-protection contractor scored 90. The job was already awarded.
- `PERMIT_RECENT` fired on one lead. It reads the filed date, which 24% of leads have. 77% have an issued date.
- 7,582 stored inspection records produced 152 leads and 438 violation records produced 38, because the classifier does not know the scraper's `inspection` and `fire_code_violation` labels. 237 `standpipe` permits were dropped the same way.
- `permit_participants` is never written, so the Participants panel is always empty.
- The scraper sends `expirationDate`, `inspectionDate`, `inspectionStatus`, `businessName`, `propertyType`, `recordType` and `workType`. The app discards all of them.

## Rules

### Normalization

`NormalizedPermit` gains optional trailing fields, so every existing construction still compiles:
`RecordType`, `WorkType`, `ExpirationDate`, `InspectionDate`, `BusinessName`, `PropertyType`.

Status mapping becomes record-type aware. The first matching row wins.

| Record type | Raw value (case-insensitive) | Status |
| --- | --- | --- |
| inspection | `Open/Follow-Up Needed`, anything containing `follow` | Failed |
| inspection | `Pending`, `Scheduled` | Inspection |
| inspection | `Completed`, `Expired`, `Closed`, `Passed` | Closed |
| violation | `abated`, `rescinded`, `closed`, `resolved`, `complied` | Closed |
| violation | any other non-empty value, for example `open`, `order to abate`, `referred to hearing` | Failed |
| any | existing rules | unchanged |
| any | `open`, `approved` when no existing rule matched | Active |

For an inspection record the status text is `permitStatus` when present, otherwise `inspectionStatus`. `RawStatus` stores the text that was used.

### Classification

New hints, matched on the scraper's `fireSystemType`:

| Hint | Category | Kind |
| --- | --- | --- |
| `inspection` | FireInspection | specific |
| `fire_code_violation` | ViolationCorrection | specific |
| `standpipe` | FireSprinkler, confidence 0.85 | fallback: the description rules run first |

A resolved inspection or violation is not a sales opportunity. When the record type is `inspection` or `violation` and the status is Closed, the classifier returns no result, so no new lead is created.

### Ingestion

- When the classifier returns no result for a permit that already has an opportunity, ingestion keeps the stored category and rescores it. A lead whose inspection was completed drops in score the same day instead of going stale.
- Participants are rebuilt on every upsert from the normalized owner and contractor names: role Owner and role Contractor. Blank names are skipped.
- The six new fields are stored on `permits`, merged with the existing never-overwrite-with-null rule.

### Scoring

Weights stay in configuration. New and changed signals:

| Signal | Weight | Applies when |
| --- | --- | --- |
| `FIRE_CONTRACTOR_ASSIGNED` | -25 | The contractor name matches a fire-trade pattern: `fire`, `sprinkler`, `sprklr`, `spr.`, `alarm`, `suppression` |
| `NO_CONTRACTOR_LISTED` | +10 | Unchanged, but only for permit records. It no longer fires on inspections and violations, which never carry a contractor |
| `PERMIT_RECENT` | +15 | Filed within 72 hours. Otherwise issued within 7 days. Otherwise, for inspection and violation records, inspection dated within the last 7 days |
| `OLD_PERMIT` | -20 | The latest known activity date is older than 90 days. Activity date is the latest of filed, issued and a past inspection date |

`FAILED_INSPECTION` and `CLOSED_PERMIT` are unchanged. They now fire on inspections and violations through the status mapping above.

The daily rescoring window includes permits whose issued date or inspection date falls inside the window, not only the filed date.

### API contract

`LeadPermitDto` and the `permit` object in `@permittorch/types` gain:
`rawStatus`, `recordType`, `workType`, `expirationDate`, `inspectionDate`, `businessName`, `propertyType`. All nullable.

### Web

The lead detail page shows the new fields when present and hides each one when empty. Signals already render generically.
The marketing score weights mirror gains `FIRE_CONTRACTOR_ASSIGNED`.

## Data migration

- One migration adds six nullable columns to `permits` and fills `permit_participants` from the existing owner and contractor columns.
- Existing inspection rows have no stored status, so they cannot be classified from the database. After deploy, the recorded Apify runs are ingested again through the normal pipeline. Upserts are idempotent.
- The rescoring job runs at startup and applies the new scoring to every lead in its window.

## Decisions taken without a product owner in the loop

| Decision | Cost if wrong |
| --- | --- |
| Awarded jobs lose 25 points, not more | One config value to change |
| Issued-date recency window is 7 days | One constant to change |
| Completed inspections and abated violations create no lead | Fewer San Francisco leads; reversible by removing one rule |
| Standpipe maps to the sprinkler category | Category label only |
| Pending inspections become leads at the baseline score | Lower-value leads in San Francisco, ranked low |

## Out of scope

Buyer trade profiles, a competitor view, contact enrichment, and every scraper-side gap: empty status and zip for New York City, sparse application dates, and the owner mapping.
