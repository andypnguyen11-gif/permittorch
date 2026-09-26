import type { SavedLeadItem } from "@permittorch/types";
import { mockLeads } from "./leads";
import { daysAgo, hoursAgo } from "./time";

const lead = (id: string) => mockLeads.find((l) => l.id === id)!;

export const mockSavedLeads: SavedLeadItem[] = [
  { id: "saved-001", status: "SAVED", createdAt: hoursAgo(5), lead: lead("lead-001") },
  { id: "saved-002", status: "CONTACTED", createdAt: daysAgo(1), lead: lead("lead-007") },
  { id: "saved-003", status: "SAVED", createdAt: daysAgo(2), lead: lead("lead-019") },
];
