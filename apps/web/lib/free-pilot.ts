/**
 * NEXT_PUBLIC_FREE_PILOT=1 (or "true"): the marketing pages say PermitTorch is free during the
 * pilot and send sign-up CTAs to /signup instead of a paid plan. It is inlined at build time, so
 * the static /pricing page needs no API call; set it together with the API's Access__FreeForAll.
 * Read at call time so tests can stub it.
 */
export function isFreePilot(): boolean {
  const value = process.env.NEXT_PUBLIC_FREE_PILOT?.trim().toLowerCase();
  return value === "1" || value === "true";
}
