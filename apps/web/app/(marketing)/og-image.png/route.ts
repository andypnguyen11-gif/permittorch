// Serves the default social card at /og-image.png. The file-convention route for
// app/(marketing)/opengraph-image.tsx is published as /opengraph-image-<hash>
// (route-group suffix), which is not on the auth middleware's public-path list,
// so crawlers would be redirected to /login. Paths containing a dot are excluded
// from the middleware matcher, so this URL is always publicly reachable.
import OpengraphImage from "../opengraph-image";

export const dynamic = "force-static";

export function GET() {
  return OpengraphImage();
}
