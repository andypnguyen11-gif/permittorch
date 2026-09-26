// Fixture timestamps are computed relative to "now" at module evaluation so
// isNew flags, age filters, and "Updated N minutes ago" stay truthful in dev.
export const minutesAgo = (n: number): string =>
  new Date(Date.now() - n * 60_000).toISOString();
export const hoursAgo = (n: number): string => minutesAgo(n * 60);
export const daysAgo = (n: number): string => hoursAgo(n * 24);
