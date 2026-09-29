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
// What a permit's record link opens: the city's own page for the record, the ArcGIS attribute
// listing for the row, or the open-data API's answer for the row.
public enum RecordLinkKind { Page, Rest, Data }
