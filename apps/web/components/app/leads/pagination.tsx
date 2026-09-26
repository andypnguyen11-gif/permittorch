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

export function lastPageFor(total: number, pageSize: number): number {
  return Math.max(1, Math.ceil(total / Math.max(1, pageSize)));
}

/**
 * Driven by what the API actually returned (page, pageSize, total), not by the
 * request — the API may clamp or default either. The leads page redirects an
 * out-of-range page to the last page before rendering; the clamp below keeps
 * "Showing X to Y of Z" sane regardless.
 */
export function LeadsPagination({ query, page: rawPage, pageSize: rawPageSize, total }: {
  query: LeadsQuery; page: number; pageSize: number; total: number;
}) {
  const pageSize = Math.max(1, rawPageSize);
  const lastPage = lastPageFor(total, pageSize);
  const page = Math.min(Math.max(1, rawPage), lastPage);
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
