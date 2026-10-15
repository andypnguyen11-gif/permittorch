# In-app feedback — design

Date: 2026-10-15
Status: Draft, awaiting owner approval

## Goal

PermitTorch is free during the pilot. Signed-in users need a quick way to send feedback — ideas,
problems, and requests for data or features — so the owner can improve the product. The owner reads
feedback by email; there is no admin screen.

## Scope

In scope:

- A "Send feedback" item in the app sidebar, opening a dialog on the current page.
- One API endpoint that stores the submission, then emails it to a configured address.
- An hourly retry for notifications that failed to send.
- One sentence in the privacy policy covering stored feedback.

Out of scope:

- Anonymous feedback. Nothing on the landing page, no public `/feedback` page, no footer link, no
  login-page link. People who cannot sign in use support@permittorch.com, which is already listed on
  the Privacy, Terms and Locations pages and forwards to the owner through Porkbun.
- An admin screen, replies from inside the app, attachments, screenshots.
- User data export. "Data request" means asking for a feature or report.
- A Playwright spec. End-to-end tests stay on the critical flows; API and Vitest tests cover this.

## Who can submit

Any signed-in account, with or without a subscription. The endpoint uses the existing `"User"`
authorization policy. A request without a valid login gets 401 and stores nothing.

The sender's identity comes only from the login. The request body carries no user id and no email;
any such fields are ignored. The notification's reply-to is the account's email.

## Data

New table `feedback` (EF Core entity `Feedback`, migration `AddFeedback`):

| Column        | Type          | Notes                                                          |
|---------------|---------------|----------------------------------------------------------------|
| `id`          | uuid, PK      |                                                                |
| `created_at`  | timestamptz   | UTC                                                            |
| `user_id`     | uuid, FK      | `app_users.id`, required                                       |
| `type`        | text enum     | `Idea`, `Problem`, `DataRequest`, `Other`                      |
| `message`     | text          | Required, trimmed, 1–5000 characters                           |
| `page_path`   | text, null    | Path only, max 500; null when absent or invalid                |
| `notified_at` | timestamptz, null | Null until the notification email succeeds                 |

Index on `(notified_at, created_at)` filtered to `notified_at IS NULL`, for the retry sweep.

## API

`POST /api/feedback` in `apps/api/Features/Feedback/FeedbackEndpoints.cs`.

Request body: `{ "type": "Idea" | "Problem" | "DataRequest" | "Other", "message": string, "pagePath": string? }`

Validation (400 on failure, using `ApiErrors.BadRequest`):

- `type` is required and must be one of the four values.
- `message` is required; after trimming it must be 1–5000 characters.

Page path rule (`FeedbackPagePath.Normalize`). A path is kept only if all hold:

- starts with exactly one `/` (so `//evil` fails),
- has no scheme or host (`https://x` fails),
- contains no `\` (browsers treat `/\evil` like `//evil`),
- contains no `?`, `#` or control characters,
- is at most 500 characters.

A path that fails is stored as null. The message is still accepted — a bad path never rejects
feedback.

Rate limit: new policy `feedback`, a fixed window of 5 requests per 10 minutes, partitioned by the
login's `sub` claim (`"sub:" + sub`). Defined next to the existing `sample-leads` policy in
`Setup/FeaturesSetup.cs`. Rejections use the existing 429 response.

Success: 202 Accepted. The response does not depend on whether the email sent.

## Delivery

Approach: save first, send immediately, retry hourly.

1. The endpoint saves the row with `notified_at = null`.
2. It calls `FeedbackNotifier.SendAsync(feedback)`. On success it sets `notified_at` and saves.
3. If Resend throws, the endpoint logs the failure (id only) and still returns 202.
4. `DigestBackgroundService` gains a feedback step: each hourly run calls
   `FeedbackNotifier.RetryPendingAsync`, which sends rows with `notified_at IS NULL` and
   `created_at` older than 5 minutes (so it does not race the send made on submit). The step has its
   own try/catch so a digest failure never blocks it, and vice versa. Note: `Digests:Enabled=false`
   also stops feedback retries; the send on submit still happens.

Idempotency: every send uses Resend's `Idempotency-Key: feedback:{id}`. The email is built only from
the saved row and the account it references — no send time or other changing value — so a retry is
identical to the first attempt. This covers the case where the email succeeds and saving
`notified_at` fails: the next hourly retry reuses the key and Resend does not deliver twice.

Resend keeps idempotency keys for 24 hours. A duplicate is possible only if saving `notified_at`
keeps failing for more than 24 hours; the owner accepts that.

A Resend 409 for a request still in progress under the same key counts as "try next hour": the row
keeps `notified_at = null` and the sweep does not fail.

Missing configuration: if `FEEDBACK_NOTIFY_EMAIL` is unset, the row is saved, nothing is sent, and a
warning is logged — `Feedback {Id} saved but FEEDBACK_NOTIFY_EMAIL is unset` — never including the
message text. The retry sweep skips sending in the same way.

## Email

Built by `FeedbackEmailBuilder` from the row and its user:

- To: `FEEDBACK_NOTIFY_EMAIL`.
- From: the existing `EMAIL_FROM` (`PermitTorch <leads@permittorch.com>`).
- Reply-To: the account's email.
- Subject: `[Feedback · Data request] <first 60 characters of the message>…` (type shown as a
  readable label; the ellipsis only when truncated).
- Body: type, account email, organization name if any, page path if any, submitted time (from
  `created_at`), and the message. Every user-supplied value is HTML-encoded; line breaks are kept.
- No `List-Unsubscribe` headers — this is a notification to the owner, not a bulk mail.

`ResendEmailClient` change: `EmailMessage` gains an optional `ReplyTo`, and `UnsubscribeUrl`
becomes optional. Unsubscribe headers are sent only when a URL is given, so digests are unchanged.

## Configuration

- `FEEDBACK_NOTIFY_EMAIL` — added to `EmailOptions` as `FeedbackNotifyEmail`, read in
  `FeaturesSetup.cs`. Production value: `support@permittorch.com`. Recorded in the deploy notes and
  the API env example.

## Web

`apps/web/components/feedback/feedback-form.tsx` — the form:

- Type: four large choices — Idea · Problem · Data request · Other.
- Message: textarea with a `n / 5000` counter.
- Submit is disabled until a type is chosen and the message is non-blank; disabled while sending.
- On success: "Thanks — we read every message."
- On 429: "You've sent a few already — try again in a few minutes."
- On 400: the API's message, inline.
- On any other error: "Couldn't send — try again." The typed text is kept.
- Full-width fields and button on phones.

`apps/web/components/feedback/feedback-dialog.tsx` — a shadcn `Dialog` around the form, full-width on
phones. It sends `usePathname()` (which carries no query string) as `pagePath`, with the token from
`useApiToken`.

`apps/web/components/app/sidebar.tsx` — a "Send feedback" item pinned above the account block. The
phone menu reuses the sidebar; choosing the item there closes the menu sheet before the dialog opens.

`apps/web/lib/api.ts` — `submitFeedback(token, body)` posting to `/api/feedback`, with a mock-mode
branch and fixture so the existing mock contract test passes.

`apps/web/app/(marketing)/privacy/page.tsx` — one added sentence: feedback you send from the app is
stored with your account and used only to read and reply to it.

## Tests

API (xUnit, existing `ApiFactory`):

1. Without a login: 401, no row stored.
2. Valid submission: row saved with `user_id` from the login. Extra `userId` or `email` fields in the
   body are ignored.
3. Validation: missing or unknown type, empty or whitespace-only message, and a 5001-character
   message each return 400.
4. Page path theory: `/app/leads/123` and `/pricing` are kept; `https://x`, `//evil`, `/\evil`,
   `/app?x=1`, `/app#a`, a control character, and a 501-character path are stored as null with the
   message still accepted.
5. Email: sent to the configured address with reply-to set to the account email; subject format;
   `<script>` in the message is encoded; no unsubscribe headers; building twice from the same row
   gives identical output.
6. `FEEDBACK_NOTIFY_EMAIL` unset: row saved, nothing sent, warning logged without the message text.
7. Resend throws on submit: 202 returned, `notified_at` stays null.
8. Retry: sends pending rows older than 5 minutes and sets `notified_at`; skips sent rows and rows
   newer than 5 minutes; a 409 "in progress" leaves the row pending without failing the sweep.
   Regression: after a successful send where saving `notified_at` failed, the next retry sends with
   the same `feedback:{id}` key and an identical payload.
9. `ResendEmailClient`: digests still carry unsubscribe headers; a message without an unsubscribe URL
   omits them; reply-to is sent when given.
10. Rate limit: the sixth request in the window from one user returns 429; a second user is
    unaffected.

Web (Vitest):

1. Feedback form: four types and the counter render; submit disabled until type and message are
   filled; success state; 429 wording; 400 inline; generic error keeps the text; the request carries
   the path without a query string.
2. Sidebar: "Send feedback" sits above the account block and opens the dialog; in the phone menu the
   sheet closes first.
3. `lib/api.ts`: `submitFeedback` posts to `/api/feedback` with the bearer token and has a mock
   branch.
4. Privacy page: the new sentence is present.

## Deploy

- Set `FEEDBACK_NOTIFY_EMAIL=support@permittorch.com` on the Railway `api` service.
- The migration runs with the normal deploy.
