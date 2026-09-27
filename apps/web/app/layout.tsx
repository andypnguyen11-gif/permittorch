import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AnalyticsProvider } from "@/components/app/analytics-provider";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-sans" });

export const metadata: Metadata = {
  title: {
    default: "PermitTorch — Fire Protection Permit Leads",
    template: "%s | PermitTorch",
  },
  description:
    "PermitTorch turns public permit data into scored, explainable sales leads for fire protection contractors.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="min-h-screen bg-background font-sans text-foreground antialiased">
        <AnalyticsProvider>{children}</AnalyticsProvider>
      </body>
    </html>
  );
}
