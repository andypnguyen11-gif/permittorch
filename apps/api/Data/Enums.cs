namespace PermitTorch.Api.Data;

public enum HealthStatus { Healthy, Warning, Stale, Failed, Disabled }
public enum FireCategory { FireSprinkler, FireAlarm, FireSuppression, KitchenSuppression, FireInspection, ViolationCorrection, GeneralFireProtection }
public enum ParticipantRole { Owner, Applicant, Contractor, GeneralContractor }
public enum UserRole { Member, Admin, SuperAdmin }          // SuperAdmin = PermitTorch staff
public enum PlanTier { Starter, Pro, Territory }
public enum SavedLeadStatus { Saved, Contacted }
public enum DigestFrequency { None, Daily, Weekly }
public enum PermitStatusKind { New, Active, Inspection, Failed, Closed, Unknown }
