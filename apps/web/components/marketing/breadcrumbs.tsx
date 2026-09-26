import Link from "next/link";
import { SITE_URL } from "@/lib/seo";

export interface Crumb { name: string; path: string }

/** Visible breadcrumb trail; the last crumb is the current page. */
export function Breadcrumbs({ items }: { items: Crumb[] }) {
  return (
    <nav aria-label="Breadcrumb" className="text-sm text-neutral-500">
      <ol className="flex flex-wrap items-center">
        {items.map((c, i) => {
          const last = i === items.length - 1;
          return (
            <li key={c.path} className="flex items-center">
              {i > 0 && <span aria-hidden="true" className="mx-2">/</span>}
              {last
                ? <span aria-current="page" className="text-neutral-700">{c.name}</span>
                : <Link href={c.path} className="hover:text-neutral-900">{c.name}</Link>}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

export function breadcrumbJsonLd(items: Crumb[]) {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map((c, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: c.name,
      item: new URL(c.path, SITE_URL).toString(),
    })),
  };
}
