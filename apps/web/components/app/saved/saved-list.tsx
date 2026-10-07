"use client";
import { useRef, useState } from "react";
import Link from "next/link";
import { Bookmark, CheckCircle2, RotateCcw, Trash2 } from "lucide-react";
import { toast } from "sonner";
import type { SavedLeadItem, SavedLeadStatus } from "@permittorch/types";
import { unsaveLead, updateSavedLead } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { reportMutationError } from "@/components/app/sign-out";
import { ScoreBadge } from "@/components/app/score-badge";
import { CategoryIcon } from "@/components/app/category-chip";
import { ContractorStatusBadge } from "@/components/app/contractor-status-badge";
import { EmptyState } from "@/components/app/leads/lead-table";
import { formatRelative, formatValueShort } from "@/components/app/format";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type Filter = "ALL" | SavedLeadStatus;
const FILTERS: Array<{ value: Filter; label: string }> = [
  { value: "ALL", label: "All" },
  { value: "SAVED", label: "To contact" },
  { value: "CONTACTED", label: "Contacted" },
];

export function SavedList({ initialItems }: { initialItems: SavedLeadItem[] }) {
  const getToken = useApiToken();
  const [items, setItems] = useState(initialItems);
  const [filter, setFilter] = useState<Filter>("ALL");
  // One in-flight mutation per item: rapid toggles would otherwise race and the
  // last response to land (not the last click) would win. The ref is the guard
  // (synchronous); the state only drives the disabled buttons.
  const inFlight = useRef(new Set<string>());
  const [pendingIds, setPendingIds] = useState<ReadonlySet<string>>(new Set());

  const begin = (id: string): boolean => {
    if (inFlight.current.has(id)) return false;
    inFlight.current.add(id);
    setPendingIds(new Set(inFlight.current));
    return true;
  };
  const end = (id: string) => {
    inFlight.current.delete(id);
    setPendingIds(new Set(inFlight.current));
  };

  const setStatus = (id: string, status: SavedLeadStatus) =>
    setItems((prev) => prev.map((i) => (i.id === id ? { ...i, status } : i)));

  const toggleStatus = async (item: SavedLeadItem) => {
    if (!begin(item.id)) return;
    const next: SavedLeadStatus = item.status === "SAVED" ? "CONTACTED" : "SAVED";
    setStatus(item.id, next); // optimistic
    try {
      await updateSavedLead(item.id, next, await getToken());
    } catch (err) {
      setStatus(item.id, item.status); // revert
      reportMutationError(err, "Could not update lead status");
    } finally {
      end(item.id);
    }
  };

  const remove = async (item: SavedLeadItem) => {
    if (!begin(item.id)) return;
    const index = items.findIndex((i) => i.id === item.id);
    setItems((prev) => prev.filter((i) => i.id !== item.id)); // optimistic
    try {
      await unsaveLead(item.id, await getToken());
      toast("Lead removed from saved");
    } catch (err) {
      // revert into its original position
      setItems((prev) => [...prev.slice(0, index), item, ...prev.slice(index)]);
      reportMutationError(err, "Could not remove lead");
    } finally {
      end(item.id);
    }
  };

  if (items.length === 0) {
    return (
      <EmptyState
        icon={Bookmark}
        title="No saved leads yet"
        description="Save promising leads from the feed to track who you’ve contacted."
        action={<Link href="/app/leads" className={buttonVariants()}>Browse leads</Link>}
      />
    );
  }

  const count = (f: Filter) => (f === "ALL" ? items.length : items.filter((i) => i.status === f).length);
  const visible = filter === "ALL" ? items : items.filter((i) => i.status === filter);

  return (
    <div className="space-y-4">
      <div className="inline-flex rounded-lg border border-border bg-white p-1" role="group" aria-label="Filter saved leads">
        {FILTERS.map((f) => (
          <button key={f.value} type="button" aria-pressed={filter === f.value}
            onClick={() => setFilter(f.value)}
            className={cn(
              "flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
              filter === f.value ? "bg-orange-50 text-orange-700" : "text-stone-500 hover:text-stone-900",
            )}>
            {f.label}{" "}
            <span className={cn("rounded-full px-1.5 text-xs tabular-nums",
              filter === f.value ? "bg-orange-100" : "bg-stone-100")}>
              {count(f.value)}
            </span>
          </button>
        ))}
      </div>

      {visible.length === 0 ? (
        <p className="rounded-xl border border-dashed border-stone-300 bg-white py-10 text-center text-sm text-stone-500">
          Nothing here yet.
        </p>
      ) : (
        <ul className="divide-y divide-border overflow-hidden rounded-xl border border-border bg-white shadow-xs">
          {visible.map((item) => (
            <li key={item.id} className="flex flex-wrap items-center gap-4 p-4 hover:bg-stone-50/60">
              <ScoreBadge score={item.lead.score} />
              <CategoryIcon category={item.lead.category} className="hidden sm:flex" />
              <div className="min-w-0 flex-1">
                <Link href={`/app/leads/${item.lead.id}`}
                  className="rounded font-medium text-stone-900 outline-none hover:text-orange-600 focus-visible:ring-3 focus-visible:ring-ring/50">
                  {item.lead.title}
                </Link>
                <p className="truncate text-sm text-stone-500">
                  {item.lead.address ?? "Address unavailable"} · {item.lead.city}, {item.lead.state} · {formatValueShort(item.lead.estimatedValue)}
                </p>
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-xs text-stone-400">Saved {formatRelative(item.createdAt)}</p>
                  <ContractorStatusBadge status={item.lead.contractorStatus} className="h-4.5 px-1.5 text-[10px]" />
                </div>
              </div>
              <Badge className={item.status === "CONTACTED"
                ? "bg-green-100 text-green-700"
                : "bg-orange-100 text-orange-700"}>
                {item.status === "CONTACTED" ? "Contacted" : "Saved"}
              </Badge>
              <div className="flex items-center gap-1">
                <Button variant="outline" size="sm" disabled={pendingIds.has(item.id)}
                  aria-busy={pendingIds.has(item.id)} onClick={() => toggleStatus(item)}>
                  {item.status === "SAVED"
                    ? <CheckCircle2 aria-hidden />
                    : <RotateCcw aria-hidden />}
                  {item.status === "SAVED" ? "Mark contacted" : "Mark saved"}
                </Button>
                <Button variant="ghost" size="sm" className="text-stone-500 hover:text-red-600"
                  disabled={pendingIds.has(item.id)} onClick={() => remove(item)}>
                  <Trash2 aria-hidden />
                  Remove
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
