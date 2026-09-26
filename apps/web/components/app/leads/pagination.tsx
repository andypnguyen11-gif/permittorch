import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { LeadsQuery } from "@/lib/api";
import { cn } from "@/lib/utils";
import { buildLeadsSearch } from "./query";

export type PageItem = number | "gap";

/** First, last, and the current page ±1, with gaps where pages are skipped. */
export function pageWindow(page: number, lastPage: number): PageItem[] {
  const wanted = new Set([1, lastPage, page - 1, page, page + 1]);
  if (page <= 2) wanted.add(2);
  const pages = [...wanted].filter((p) => p >= 1 && p <= lastPage).sort((a, b) => a - b);
  const out: PageItem[] = [];
  pages.forEach((p, i) => {
    if (i > 0 && p - pages[i - 1] > 1) out.push("gap");
    out.push(p);
  });
  return out;
}

const box = "inline-flex h-8 min-w-8 items-center justify-center rounded-lg border px-2 text-sm tabular-nums outline-none focus-visible:ring-3 focus-visible:ring-ring/50";

export function LeadsPagination({ query, total }: { query: LeadsQuery; total: number }) {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 25;
  const lastPage = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  const href = (p: number) => `/app/leads${buildLeadsSearch({ ...query, page: p })}`;
  const arrow = (p: number, label: string, Icon: typeof ChevronLeft, enabled: boolean) =>
    enabled ? (
      <Link aria-label={label} href={href(p)}
        className={cn(box, "border-border bg-white text-stone-600 hover:border-orange-300 hover:text-orange-600")}>
        <Icon className="size-4" aria-hidden />
      </Link>
    ) : (
      <span aria-hidden className={cn(box, "border-border bg-white text-stone-300")}>
        <Icon className="size-4" />
      </span>
    );

  return (
    <nav aria-label="Pagination" className="flex flex-wrap items-center justify-between gap-3 text-sm text-stone-500">
      <span>{total === 0 ? "No results" : `Showing ${from} to ${to} of ${total} results`}</span>
      {lastPage > 1 && (
        <div className="flex items-center gap-1.5">
          {arrow(page - 1, "Previous page", ChevronLeft, page > 1)}
          {pageWindow(page, lastPage).map((p, i) =>
            p === "gap" ? (
              <span key={`gap-${i}`} className="px-1 text-stone-400" aria-hidden>…</span>
            ) : p === page ? (
              <span key={p} aria-current="page"
                className={cn(box, "border-orange-300 bg-orange-50 font-semibold text-orange-600")}>
                {p}
              </span>
            ) : (
              <Link key={p} href={href(p)} aria-label={`Page ${p}`}
                className={cn(box, "border-border bg-white text-stone-600 hover:border-orange-300 hover:text-orange-600")}>
                {p}
              </Link>
            ),
          )}
          {arrow(page + 1, "Next page", ChevronRight, page < lastPage)}
        </div>
      )}
    </nav>
  );
}
