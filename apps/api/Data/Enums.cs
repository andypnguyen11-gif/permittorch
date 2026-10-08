namespace PermitTorch.Api.Data;

public enum HealthStatus { Healthy, Warning, Stale, Failed, Disabled }
public enum FireCategory { FireSprinkler, FireAlarm, FireSuppression, KitchenSuppression, FireInspection, ViolationCorrection, GeneralFireProtection }
public enum ParticipantRole { Owner, Applicant, Contractor, GeneralContractor }
public enum UserRole { Member, Admin, SuperAdmin }          // SuperAdmin = PermitTorch staff
public enum PlanTier { Starter, Pro, Territory }
public enum SavedLeadStatus { Saved, Contacted }
public enum DigestFrequency { None, Daily, Weekly }
public enum RemovalKind { Phone, Email, Name, Record }
public enum PermitStatusKind { New, Active, Inspection, Failed, Closed, Unknown }
// Who the record names as contractor, read from the permit itself. FireContractorNamed means the
// fire work on this permit is most likely awarded; OtherContractorNamed (usually the GC) means
// the fire sub is not visible yet. NotApplicable: an inspection or violation with no name, which
// says nothing. Stored null on leads not yet scored by a release that knows the status.
public enum ContractorStatus { NotApplicable, NoContractorListed, OtherContractorNamed, FireContractorNamed }
// What a permit's record link opens: the city's own page for the record, the ArcGIS attribute
// listing for the row, or the open-data API's answer for the row.
public enum RecordLinkKind { Page, Rest, Data }
// Whether the permit itself is the fire work (a sprinkler, alarm or suppression permit, whose
// named contractor does that work) or a building permit that only mentions it. Resolved from the
// city's own permit type by the provider; null on permits stored before it was known.
public enum PermitScope { FireWorkPermit, BuildingPermit }
// Where a lead stands in "who should a contractor call first today", best first. The feed sorts on
// it before freshness. Building permits whose record says the fire work is still ahead lead; a
// permit that is the fire work itself ranks below them, as it is normally pulled by the installer.
// NotFireWork: the record describes no fire-protection work, and the lead is hidden.
public enum LeadStanding
{
    FireWorkAhead, FireWorkMentioned, FireWorkPermitNoContractor, InspectionOrViolation,
    FireWorkPermitContractorNamed, FireFirmNamed, Closed, NotFireWork,
}
