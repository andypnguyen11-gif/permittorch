// The version of the terms a user is asked to agree to. It is the date the terms page last
// changed, with ".2", ".3" and so on for a later change on the same day, and it must equal
// Terms.CurrentVersion in the API
// (apps/api/Features/Account/Terms.cs): the API refuses an agreement to any other version.
// Change both whenever the wording of the terms or the privacy policy changes.
export const TERMS_VERSION = "2026-09-29.2";
export const TERMS_UPDATED = "September 29, 2026";
