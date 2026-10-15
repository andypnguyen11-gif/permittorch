"use client";
import { useState } from "react";
import { BellOff, CalendarDays, Sunrise, type LucideIcon } from "lucide-react";
import { toast } from "sonner";
import type { DigestFrequency, Market } from "@permittorch/types";
import { updateEmailPreferences } from "@/lib/api";
import { track } from "@/lib/analytics";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { SectionCard } from "@/components/app/section-card";
import { cn } from "@/lib/utils";

const OPTIONS: Array<{ value: DigestFrequency; label: string; description: string; icon: LucideIcon }> = [
  { value: "DAILY", label: "Daily", icon: Sunrise,
    description: "Every morning — best for being first to new permits." },
  { value: "WEEKLY", label: "Weekly", icon: CalendarDays,
    description: "Monday mornings — a summary of the week’s opportunities." },
  { value: "NONE", label: "Off", icon: BellOff,
    description: "No email digest. You can still check leads any time." },
];

/**
 * Digest frequency plus, when `markets` is given, which of the user's markets the digest
 * covers. Each change saves right away; frequency and markets are always sent together.
 * During the free pilot no email goes out until at least one market is picked; on a paid
 * plan an empty pick means every market on the plan.
 */
export function DigestForm({ initialFrequency, markets, initialMarketSlugs = [], freeAccess = false }: {
  initialFrequency: DigestFrequency;
  markets?: Market[];
  initialMarketSlugs?: string[];
  freeAccess?: boolean;
}) {
  const getToken = useApiToken();
  const [frequency, setFrequency] = useState<DigestFrequency>(initialFrequency);
  const [selected, setSelected] = useState<string[]>(initialMarketSlugs);
  const [saving, setSaving] = useState(false);
  const pickMarkets = markets !== undefined && markets.length > 0;

  const save = async (nextFrequency: DigestFrequency, nextSlugs: string[]) => {
    const token = await getToken();
    // Without a picker the saved markets are left alone.
    return pickMarkets
      ? updateEmailPreferences(nextFrequency, token, nextSlugs)
      : updateEmailPreferences(nextFrequency, token);
  };

  const choose = async (value: DigestFrequency) => {
    // Ignore input while a save is in flight (instead of disabling the fieldset,
    // which would drop focus and hide the options from assistive tech).
    if (saving || value === frequency) return;
    const previous = frequency;
    setFrequency(value); // optimistic
    setSaving(true);
    try {
      await save(value, selected);
      if (value !== "NONE") track("digest_enabled", { frequency: value });
      toast.success(value === "NONE" ? "Email digest turned off" : `Digest set to ${value.toLowerCase()}`);
    } catch (err) {
      setFrequency(previous);
      reportMutationError(err, "Could not update email preferences");
    } finally {
      setSaving(false);
    }
  };

  const toggleMarket = async (slug: string) => {
    if (saving) return;
    const previous = selected;
    const next = selected.includes(slug) ? selected.filter((s) => s !== slug) : [...selected, slug];
    setSelected(next); // optimistic
    setSaving(true);
    try {
      await save(frequency, next);
      toast.success("Digest markets saved");
    } catch (err) {
      setSelected(previous);
      reportMutationError(err, "Could not update email preferences");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <FrequencyOptions frequency={frequency} saving={saving} onChoose={choose} />
      {pickMarkets && (
        <SectionCard title="Markets in your digest">
          <fieldset className="space-y-3" aria-busy={saving}>
            <legend className="sr-only">Markets in your digest</legend>
            {frequency !== "NONE" && selected.length === 0 && (
              <p role="status" className="rounded-lg border border-orange-200 bg-orange-50 px-3 py-2 text-sm text-stone-700">
                {freeAccess
                  ? "No email is sent until you pick at least one market."
                  : "No market picked, so the digest covers every market on your plan."}
              </p>
            )}
            <ul className="grid max-h-80 gap-2 overflow-y-auto sm:grid-cols-2">
              {markets.map((m) => (
                <li key={m.slug}>
                  <label className="flex cursor-pointer items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm text-stone-700 hover:border-stone-300 has-[:focus-visible]:ring-3 has-[:focus-visible]:ring-ring/50">
                    <input type="checkbox" name="digest-market" value={m.slug}
                      checked={selected.includes(m.slug)} onChange={() => toggleMarket(m.slug)}
                      className="size-4 accent-orange-500" />
                    {m.name}
                  </label>
                </li>
              ))}
            </ul>
          </fieldset>
        </SectionCard>
      )}
    </div>
  );
}

function FrequencyOptions({ frequency, saving, onChoose }: {
  frequency: DigestFrequency; saving: boolean; onChoose: (value: DigestFrequency) => void;
}) {
  return (
    <fieldset className="space-y-3" aria-busy={saving}>
      <legend className="sr-only">Digest frequency</legend>
      {OPTIONS.map(({ value, label, description, icon: Icon }) => {
        const checked = frequency === value;
        return (
          <label key={value}
            className={cn(
              "flex cursor-pointer items-start gap-4 rounded-xl border bg-white p-4 transition-colors has-[:focus-visible]:ring-3 has-[:focus-visible]:ring-ring/50",
              checked ? "border-orange-400 bg-orange-50/40 ring-1 ring-orange-400" : "border-border hover:border-stone-300",
            )}>
            <input type="radio" name="digest-frequency" value={value} checked={checked}
              onChange={() => onChoose(value)} className="sr-only" />
            <span className={cn("flex size-9 shrink-0 items-center justify-center rounded-lg",
              checked ? "bg-orange-500 text-white" : "bg-stone-100 text-stone-500")}>
              <Icon className="size-4" aria-hidden />
            </span>
            <span className="flex-1">
              <span className="block font-medium text-stone-900">{label}</span>
              <span className="block text-sm text-stone-500">{description}</span>
            </span>
            <span aria-hidden className={cn("mt-1 flex size-4 items-center justify-center rounded-full border",
              checked ? "border-orange-500" : "border-stone-300")}>
              {checked && <span className="size-2 rounded-full bg-orange-500" />}
            </span>
          </label>
        );
      })}
    </fieldset>
  );
}
