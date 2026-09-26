export interface HowItWorksStep {
  step: number;
  title: string;
  summary: string;   // homepage strip
  detail: string;    // expanded on /how-it-works
}

export const HOW_IT_WORKS_STEPS: HowItWorksStep[] = [
  {
    step: 1,
    title: "We monitor public records",
    summary: "PermitTorch watches permit and inspection feeds across your market, every day.",
    detail:
      "Cities publish building permits and inspection results constantly — but across dozens of portals, formats, and update schedules. PermitTorch pulls from every active source in your market on a daily cycle, keeps a health check on each one, and shows you exactly when the data was last updated. If a source goes stale, we say so instead of pretending it is current.",
  },
  {
    step: 2,
    title: "We identify the fire work",
    summary: "Sprinkler, alarm, suppression, kitchen systems, failed inspections — classified automatically.",
    detail:
      "Most permits are noise for a fire contractor: plumbing, roofing, fences. PermitTorch classifies each record against fire-specific rules — sprinkler scope, alarm scope, suppression systems, kitchen hood systems, fire inspections, and code violations — so your feed contains only work you could actually bid.",
  },
  {
    step: 3,
    title: "We score every opportunity",
    summary: "A 0–100 score built from real signals: value, scope, timing, and who is already on the job.",
    detail:
      "Every lead gets a deterministic 0–100 score assembled from named signals — new commercial construction, detected fire scope, how recently it was filed, project value, square footage, and whether a fire contractor is already listed. No black box: each point traces to a signal you can read, so you can trust the ranking or overrule it.",
  },
  {
    step: 4,
    title: "You get actionable leads",
    summary: "A ranked list with the address, the permit, and the reason it matters. Chase the good ones first.",
    detail:
      "Open your dashboard or your morning digest and work top-down: address, permit number, filing date, estimated value, and a one-line reason this lead matters. Save the ones you are chasing, mark them contacted, export to CSV, and click through to the official government record any time.",
  },
];
