# Removal list: design

Date: 2026-09-29. Scope: the API pipeline, the admin area of the web app, one sentence on the privacy page. The scraper is unchanged.

## Why

The privacy page promises a person named on a permit that we remove their phone number, email address, name or a whole record within 30 days of asking, and sooner where a law sets a shorter time. Nothing in the product does this:

- A value cleared by hand in the database comes back with the next scrape of that permit. `SyncParticipant` fills a contact again whenever the record publishes one.
- No account in production is an admin, so nobody could act on a request without a database session.

Production holds 180 phone numbers and 107 email addresses on 11,047 participants (2026-09-29).

## What the owner decided

- The owner enters removals on an admin page, not through Claude.
- A removed value is removed from storage, not hidden on display. Leads are read in five places (feed, detail, export, saved leads, digest); hiding would have to be right in all of them.
- Customers see a blank, as if the record never published the value. No "removed on request" label.
- A removal applies to anyone named on a permit, a company included.

## The four kinds

| Kind | The admin enters | Matches | Effect on a match |
|---|---|---|---|
| Phone | a phone number | a participant's phone with the same key, on any record of any source | the phone is cleared |
| Email | an email address | a participant's email with the same key, on any record | the email is cleared |
| Name | a name | an owner, applicant, contractor or business name with the same key, on any record | the name is cleared, and with it that party's phone, email and licence number |
| Record | one permit, picked from a search | that permit | the permit and its lead are deleted, and it is never imported again |

### Keys

A key is what two values are compared by. The stored value is never rewritten to its key.

| Kind | Key |
|---|---|
| Phone | The digits only. A leading `1` is dropped when 11 or more digits remain. The key is the first 10 digits. `(480) 555-0142 x12` and `1-480-555-0142` have the same key. |
| Email | Trimmed and lower-cased. |
| Name | Trimmed, runs of white space made one space, upper-cased. `John  Smith` and `JOHN SMITH` have the same key. `Smith, John` has another. |
| Record | The source and the record's `external_id`. The permit's fingerprint is stored beside it. |

A name is matched as a whole value. The description of a permit is free text and is not searched. When a description names a person, the admin removes the record.

## Data

New table `removals`:

| Column | Holds |
|---|---|
| `id` | |
| `kind` | Phone, Email, Name or Record |
| `value` | what the admin entered, trimmed. For a record: the permit number, or the external id when there is none |
| `match_key` | the key above. For a record: source id, a colon, external id |
| `source_id`, `external_id`, `permit_number`, `fingerprint` | a record only, otherwise null |
| `label` | a record only: the city and state, for the admin's list. No address and no name |
| `note` | the admin's note, such as "email of 3 Oct". Up to 500 characters |
| `records_affected` | how many permits the removal changed or deleted when it was made |
| `created_at`, `created_by_user_id` | |

Unique on `kind` and `match_key`.

`permits` gains two columns, both false by default:

| Column | Meaning |
|---|---|
| `contractor_withheld` | the record names a contractor and the name is removed |
| `contractor_withheld_is_fire_trade` | that name matched the fire trade pattern |

Neither column holds personal data.

## Making a removal

One transaction:

1. The removal row is written.
2. Stored data is cleaned, by kind, as the first table says. A participant whose name is removed is deleted. A permit's `owner_name`, `applicant_name`, `contractor_name` and `business_name` are each cleared where they have the key.
3. Where a contractor name is cleared, `contractor_withheld` is set, and `contractor_withheld_is_fire_trade` is set from the name before it is cleared.
4. Where a contractor name is cleared, the permit's lead is scored again with the same scoring code as the daily rescoring. The contractor's name is the only thing a removal clears that the score reads, so no other removal can change a score.
5. For a record, the permit is deleted. Its participants, its lead, the lead's signals and every customer's saved copy of the lead go with it.

## The import

- The removals are read at the start of a run and again every 100 records.
- At the end of a run, every removal made since the run began is applied to stored data once more. A removal made while a run was storing records holds when the run ends.
- Each record is checked after it is normalized and before anything is stored.
- A record whose source and external id are on the list is skipped. So is a record of that source with a listed fingerprint, because the same permit can come back under a new id. The fingerprint counts only when the record has an address and the two permit numbers cannot disagree, which is the rule the import already uses for that case. A skipped record is logged and is counted neither as imported nor as a failure.
- A listed phone or email is dropped from the party it came with. A listed name is dropped with that party's contact details. A listed business name is dropped.
- A dropped contractor name sets the two withheld flags on the normalized record.
- When a record names a contractor that is not on the list, both flags are cleared.

## Scoring

The score stays what the record supports:

| Stored state | Signal |
|---|---|
| contractor name present | unchanged |
| no contractor name, not withheld | `NO_CONTRACTOR_LISTED`, as today |
| withheld, not fire trade | no contractor signal |
| withheld, fire trade | `FIRE_CONTRACTOR_ASSIGNED`, as if the name were present |

So a removal never adds points to a lead, and a job that is already awarded stays marked down.

## Admin API

All under `/api/admin/removals`, SuperAdmin only. A member gets 403 and an anonymous caller 401, like the other admin routes.

| Route | Does |
|---|---|
| `POST /preview` | Takes a kind and a value. Returns how many permits match, split by city. Changes nothing. |
| `GET /records?market=&q=` | For the Record kind: permits of one market whose permit number or address contains `q`. At most 20, with permit number, address, city and filed date. |
| `POST` | Makes the removal. Takes the kind, the value or the permit's id, the note, and `confirmedCount`. |
| `GET` | The removals, newest first, paged. |
| `DELETE /{id}` | Undoes a removal. |

Rules at the boundary:

- A phone must hold at least 10 digits. An email must be one plain address. A name must be 3 to 200 characters after trimming.
- `confirmedCount` must equal the number of permits that match at that moment. If it does not, the call is refused with 409 and nothing changes. The admin has always seen the count they confirm.
- A second removal with the same kind and key is refused with 409.

## Undo

Deleting a removal stops it applying to later imports. It does not put anything back. A value returns only when the scraper delivers that record again, and the daily run delivers new records only. The page says so before the admin confirms. On the page the button reads "Take off the list", because "undo" promises more than it does.

## Admin page

`/app/admin/removals`, linked in the sidebar as "Removals", shown to a SuperAdmin only.

- A form: kind, value, note.
- For a record: a market, a search box and a list of matches to pick from.
- A preview before confirming: "This matches 3 permits: Mesa, AZ 2, Austin, TX 1."
- A list of removals: date, kind, value, note, permits affected, a button that takes the removal off the list.
- Before confirming, and at the top of the page, it says that a removal cannot be put back.

The page holds no matching rule. It shows what the API returns.

## Privacy page

One sentence is added to section 3:

> To keep it removed, we keep what you asked us to remove on a private list that our daily import checks. Customers never see that list.

This changes the wording, so the terms version changes with it and every user is asked to agree again.

## The owner's account

Both production accounts have the role Member. The owner's account is given the role SuperAdmin by one statement on the production database, recorded in `docs/deploy.md`. The check account stays a Member.

## Tests

| Layer | Covers |
|---|---|
| Unit (xUnit) | The three keys, with the examples above. The check of one normalized record against a list, per kind. The four rows of the scoring table. |
| Integration (xUnit, real Postgres) | Each admin route, with 401 and 403. A removal cleans stored data for each kind. `confirmedCount` that is wrong changes nothing. A record removal deletes the lead and its saved copies. |
| Import (xUnit) | A removed phone, email and name stay removed after the same record is imported again. A removed record is skipped by id and by fingerprint. A later record naming another contractor clears the withheld flags. |
| Web (Vitest) | The page shows the preview count before it can confirm, lists removals, and asks before an undo. |

The regression test for the defect this fixes: clear a phone by a removal, import the same record again, and the phone is still absent.

## Not in this design

- Removal by property address.
- Searching descriptions for a name.
- Telling customers about a removal, or a log of who exported what.
- Hiding the contact details of every individual, as opposed to a company.
