// Default 1200x630 social card, served by app/(marketing)/og-image.png/route.ts.
// Deliberately NOT a Next opengraph-image convention file: inside the route group
// that would publish /opengraph-image-<hash>, which the auth middleware redirects.
import { ImageResponse } from "next/og";
import { SITE_NAME, SITE_TAGLINE } from "@/lib/seo";

export const alt = `${SITE_NAME} — ${SITE_TAGLINE}`;
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

const ORANGE = "#c2410c"; // orange-700: brand accent with AA contrast on white

export function renderOgCard() {
  return new ImageResponse(
    (
      <div style={{
        width: "100%", height: "100%", display: "flex", flexDirection: "column",
        justifyContent: "center", padding: "80px", background: "#ffffff",
        borderTop: `24px solid ${ORANGE}`, fontFamily: "sans-serif",
      }}>
        <div style={{ display: "flex", alignItems: "center", gap: "24px" }}>
          <svg width="96" height="96" viewBox="0 0 24 24" fill={ORANGE}>
            <path d="M12 2c.4 2.9-1.4 4.7-3 6.5C7.4 10.3 6 12.3 6 15a6 6 0 0 0 12 0c0-2.3-1-4.3-2.4-6C14.1 7.1 12.6 5.1 12 2Zm0 19a4 4 0 0 1-4-4c0-1.7.8-3 2-4.3.3 1.2 1 2.3 2 3.3 1-1 1.7-2.1 2-3.3 1.2 1.3 2 2.6 2 4.3a4 4 0 0 1-4 4Z" />
          </svg>
          <div style={{ display: "flex", fontSize: 88, fontWeight: 700, color: "#171717" }}>
            Permit<span style={{ color: ORANGE }}>Torch</span>
          </div>
        </div>
        <div style={{ display: "flex", marginTop: 40, fontSize: 48, color: "#404040", lineHeight: 1.2 }}>
          {SITE_TAGLINE}.
        </div>
        <div style={{ display: "flex", marginTop: 24, fontSize: 32, color: "#525252" }}>
          Scored, explainable leads for fire-protection contractors.
        </div>
      </div>
    ),
    size,
  );
}
