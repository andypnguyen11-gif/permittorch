import type { PreSprinklerContact, PreSprinklerLead } from "@permittorch/types";

export interface PreSprinklerGroup {
  key: string;
  /** Set only when more than one lead shares the address and the first contact. */
  heading: string | null;
  leads: PreSprinklerLead[];
}

const norm = (value: string | null | undefined) => (value ?? "").trim().replace(/\s+/g, " ").toLowerCase();

function contactKey(contact: PreSprinklerContact | undefined): string | null {
  if (!contact) return null;
  const who = norm(contact.name) || norm(contact.company);
  if (!who) return null;
  return [contact.role, norm(contact.name), norm(contact.company), norm(contact.phone), norm(contact.email)].join("|");
}

export function contactWho(contact: PreSprinklerContact): string {
  if (contact.name && contact.company) return `${contact.name} (${contact.company})`;
  return contact.name ?? contact.company ?? "";
}

/**
 * Leads that share an address and the same first contact are listed under one heading (a tract
 * builder's lots at one address, for example). Order is kept: a group sits where its first lead
 * was. A lead without a street or without a first contact is never grouped.
 */
export function groupPreSprinklerLeads(leads: PreSprinklerLead[]): PreSprinklerGroup[] {
  const groups: PreSprinklerGroup[] = [];
  const byKey = new Map<string, PreSprinklerGroup>();
  for (const lead of leads) {
    const street = norm(lead.street);
    const contact = contactKey(lead.contacts[0]);
    const key = street && contact ? `${street}|${norm(lead.city)}|${contact}` : `lead:${lead.id}`;
    const existing = byKey.get(key);
    if (existing) {
      existing.leads.push(lead);
      continue;
    }
    const group: PreSprinklerGroup = { key, heading: null, leads: [lead] };
    byKey.set(key, group);
    groups.push(group);
  }
  for (const group of groups) {
    if (group.leads.length < 2) continue;
    const first = group.leads[0];
    const place = [first.street, first.city].filter(Boolean).join(", ");
    const contact = first.contacts[0];
    const role = contact.roleLabel ?? contact.role.toLowerCase();
    group.heading = `${place} · ${group.leads.length} permits · ${role}: ${contactWho(contact)}`;
  }
  return groups;
}
