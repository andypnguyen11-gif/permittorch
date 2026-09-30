"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import type { Market, RemovalKind, RemovalPreview, RemovalRecord } from "@permittorch/types";
import { ApiError, createRemoval, previewRemoval, searchRemovalRecords } from "@/lib/api";
import { isSessionError, reportMutationError } from "@/components/app/sign-out";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const KINDS: { kind: RemovalKind; option: string; label: string; invalid: string }[] = [
  { kind: "PHONE", option: "A phone number", label: "Phone number", invalid: "A phone number needs at least 10 digits." },
  { kind: "EMAIL", option: "An email address", label: "Email address", invalid: "Enter one email address." },
  { kind: "NAME", option: "A name", label: "Name", invalid: "A name needs 3 to 200 characters." },
  { kind: "RECORD", option: "A whole record", label: "Permit number or address", invalid: "Enter at least 3 characters." },
];

const selectClass =
  "h-10 w-full rounded-lg border border-input bg-white px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50";

const plural = (n: number) => `${n} permit${n === 1 ? "" : "s"}`;

function describe(preview: RemovalPreview): string {
  if (preview.permits === 0) {
    return "Nothing stored matches. You can still remove it, so that a later scrape cannot bring it in.";
  }
  const cities = preview.cities.map((c) => `${c.city}, ${c.state} ${c.permits}`).join(", ");
  return `This matches ${plural(preview.permits)}: ${cities}.`;
}

const recordName = (r: RemovalRecord) => r.permitNumber ?? r.address ?? "this permit";

/**
 * Makes a removal. The count of matches is shown before the admin can confirm, and is sent
 * with the removal; the API refuses it when the count is no longer true. All matching is the
 * API's: this form shows what it returns.
 */
export function RemovalForm({ markets }: { markets: Market[] }) {
  const router = useRouter();
  const getToken = useApiToken();
  const [kind, setKind] = useState<RemovalKind>("PHONE");
  const [value, setValue] = useState("");
  const [note, setNote] = useState("");
  const [market, setMarket] = useState(markets[0]?.slug ?? "");
  const [preview, setPreview] = useState<RemovalPreview | null>(null);
  const [records, setRecords] = useState<RemovalRecord[] | null>(null);
  const [picked, setPicked] = useState<RemovalRecord | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const current = KINDS.find((k) => k.kind === kind)!;
  const isRecord = kind === "RECORD";
  const ready = isRecord ? picked !== null : preview !== null;

  const forget = () => { setPreview(null); setRecords(null); setPicked(null); setError(null); };
  const reset = () => { setValue(""); setNote(""); forget(); };

  const fail = (err: unknown) => {
    if (isSessionError(err)) return reportMutationError(err, "");
    if (!(err instanceof ApiError)) return setError("Something went wrong. Please try again.");
    if (err.message === "count_changed") { forget(); return setError("The number of matches changed. Check the matches again."); }
    if (err.message === "removal_exists") return setError("This is already on the list.");
    if (err.message === "permit_not_found") { forget(); return setError("That permit is no longer stored. Search again."); }
    if (err.status === 400 && (err.message === "invalid_value" || (isRecord && err.message.startsWith("q must")))) {
      return setError(current.invalid);
    }
    setError("Something went wrong. Please try again.");
  };

  const run = async (work: (token: string) => Promise<void>) => {
    setError(null);
    setBusy(true);
    try { await work(await getToken()); } catch (err) { fail(err); } finally { setBusy(false); }
  };

  const check = () => run(async (token) => {
    if (isRecord) {
      setPicked(null);
      setRecords(await searchRemovalRecords(market, value.trim(), token));
    } else {
      setPreview(await previewRemoval(kind, value.trim(), token));
    }
  });

  const remove = () => run(async (token) => {
    const trimmedNote = note.trim();
    await createRemoval(isRecord
      ? { kind, permitId: picked!.permitId, ...(trimmedNote && { note: trimmedNote }), confirmedCount: 1 }
      : { kind, value: value.trim(), ...(trimmedNote && { note: trimmedNote }), confirmedCount: preview!.permits },
    token);
    toast.success("Removed");
    reset();
    router.refresh();
  });

  return (
    <form className="space-y-4 rounded-xl border border-border bg-white p-5 shadow-xs"
      onSubmit={(e) => { e.preventDefault(); if (value.trim()) void check(); }} noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="removal-kind">What to remove</Label>
          <select id="removal-kind" className={selectClass} value={kind} disabled={busy}
            onChange={(e) => { setKind(e.target.value as RemovalKind); reset(); }}>
            {KINDS.map((k) => <option key={k.kind} value={k.kind}>{k.option}</option>)}
          </select>
        </div>
        {isRecord && (
          <div className="space-y-1.5">
            <Label htmlFor="removal-market">Market</Label>
            <select id="removal-market" className={selectClass} value={market} disabled={busy}
              onChange={(e) => { setMarket(e.target.value); forget(); }}>
              {markets.map((m) => <option key={m.slug} value={m.slug}>{m.name}</option>)}
            </select>
          </div>
        )}
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="removal-value">{current.label}</Label>
        <Input id="removal-value" className="h-10 bg-white" value={value} disabled={busy} autoComplete="off"
          onChange={(e) => { setValue(e.target.value); forget(); }} />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="removal-note">Note</Label>
        <Input id="removal-note" className="h-10 bg-white" value={note} disabled={busy} maxLength={500}
          autoComplete="off" onChange={(e) => setNote(e.target.value)} />
        <p className="text-xs text-stone-500">For you, such as the date of the request. Customers never see it.</p>
      </div>

      {preview && <p role="status" className="rounded-lg border border-border bg-stone-50 px-3 py-2 text-sm text-stone-800">{describe(preview)}</p>}

      {records && records.length === 0 && (
        <p role="status" className="text-sm text-stone-600">No permit in this market matches.</p>
      )}
      {records && records.length > 0 && !picked && (
        <ul className="divide-y divide-border rounded-lg border border-border text-sm">
          {records.map((r) => (
            <li key={r.permitId} className="flex items-center justify-between gap-3 px-3 py-2">
              <span>
                <span className="font-medium text-stone-900">{r.permitNumber ?? "No permit number"}</span>
                <span className="text-stone-500"> · {r.address ?? "No address"} · {r.city}, {r.state}</span>
              </span>
              <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => setPicked(r)}
                aria-label={`Pick ${recordName(r)}`}>Pick</Button>
            </li>
          ))}
        </ul>
      )}
      {picked && (
        <p role="status" className="rounded-lg border border-border bg-stone-50 px-3 py-2 text-sm text-stone-800">
          This removes 1 permit: {[picked.permitNumber, picked.address, `${picked.city}, ${picked.state}`].filter(Boolean).join(", ")}.
        </p>
      )}

      {error && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
      )}

      {ready && (
        <p role="note" className="text-sm text-stone-700">
          A removal cannot be put back. What it clears or deletes stays gone, even if you take the removal off the list later.
        </p>
      )}

      <div className="flex flex-wrap gap-2">
        <Button type="submit" variant="outline" disabled={busy || value.trim() === ""}>
          {busy && !ready && <Loader2 className="animate-spin" aria-hidden />}
          {isRecord ? "Search" : "Check matches"}
        </Button>
        <Button type="button" disabled={busy || !ready} onClick={remove}>
          {busy && ready && <Loader2 className="animate-spin" aria-hidden />}
          Remove
        </Button>
      </div>
    </form>
  );
}
