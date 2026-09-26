"use client";
import { useState } from "react";
import { BellOff, CalendarDays, Sunrise, type LucideIcon } from "lucide-react";
import { toast } from "sonner";
import type { DigestFrequency } from "@permittorch/types";
import { updateEmailPreferences } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { cn } from "@/lib/utils";

const OPTIONS: Array<{ value: DigestFrequency; label: string; description: string; icon: LucideIcon }> = [
  { value: "DAILY", label: "Daily", icon: Sunrise,
    description: "Every morning at 6:00 AM — best for being first to new permits." },
  { value: "WEEKLY", label: "Weekly", icon: CalendarDays,
    description: "Monday mornings — a summary of the week’s opportunities." },
  { value: "NONE", label: "Off", icon: BellOff,
    description: "No email digest. You can still check leads any time." },
];

export function DigestForm({ initialFrequency }: { initialFrequency: DigestFrequency }) {
  const getToken = useApiToken();
  const [frequency, setFrequency] = useState<DigestFrequency>(initialFrequency);
  const [saving, setSaving] = useState(false);

  const choose = async (value: DigestFrequency) => {
    if (value === frequency) return;
    const previous = frequency;
    setFrequency(value); // optimistic
    setSaving(true);
    try {
      await updateEmailPreferences(value, await getToken());
      toast.success(value === "NONE" ? "Email digest turned off" : `Digest set to ${value.toLowerCase()}`);
    } catch (err) {
      setFrequency(previous);
      reportMutationError(err, "Could not update email preferences");
    } finally {
      setSaving(false);
    }
  };

  return (
    <fieldset disabled={saving} className="space-y-3" aria-busy={saving}>
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
              onChange={() => choose(value)} className="sr-only" />
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
