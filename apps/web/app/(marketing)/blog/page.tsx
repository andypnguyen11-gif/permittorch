import Link from "next/link";
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/seo";
import { blogPosts } from "@/components/marketing/blog-posts";

export const metadata: Metadata = buildMetadata({
  title: "Resources for Fire Protection Contractors",
  description:
    "Practical guides on finding fire protection work through permit data: lead generation, prospecting, and reading public records like a salesperson.",
  path: "/blog",
});

const dateFmt = new Intl.DateTimeFormat("en-US", {
  year: "numeric", month: "long", day: "numeric", timeZone: "UTC",
});

export default function BlogIndexPage() {
  const posts = [...blogPosts].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt));
  return (
    <div className="mx-auto max-w-3xl px-4 py-20 sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">Resources</h1>
      <p className="mt-4 text-lg text-neutral-600">
        Practical guides on turning public permit data into fire protection work.
      </p>
      <div className="mt-12 space-y-10">
        {posts.map((p) => (
          <article key={p.slug}>
            <p className="text-sm text-neutral-400">{dateFmt.format(new Date(p.publishedAt))}</p>
            <h2 className="mt-1 text-2xl font-semibold">
              <Link href={`/blog/${p.slug}`} className="hover:text-orange-600">{p.title}</Link>
            </h2>
            <p className="mt-2 leading-relaxed text-neutral-600">{p.description}</p>
            <Link href={`/blog/${p.slug}`} className="mt-2 inline-block text-sm font-medium text-orange-600">
              Read the guide →
            </Link>
          </article>
        ))}
      </div>
    </div>
  );
}
