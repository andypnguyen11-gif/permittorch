// Serves the default social card at /og-image.png. Paths containing a dot are
// excluded from the auth middleware matcher, so crawlers always reach it.
import { renderOgCard } from "@/components/marketing/og-card";

export const dynamic = "force-static";

export function GET() {
  return renderOgCard();
}
