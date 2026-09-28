using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;

namespace PermitTorch.Api.Domain.Scoring;

/// <summary>Rebuilds the scoring input from a persisted permit (the engine only reads these
/// fields). Shared by the daily rescoring pass and admin reclassification so both score a
/// stored permit identically.</summary>
public static class StoredPermit
{
    public static NormalizedPermit ToNormalized(Permit p) => new(
        ExternalId: p.ExternalId, Jurisdiction: string.Empty, PermitNumber: p.PermitNumber,
        PermitType: p.PermitType, Description: p.Description, Status: p.Status, RawStatus: p.RawStatus,
        Address: p.Address, City: p.City, State: p.State, Zip: p.Zip,
        Latitude: p.Latitude, Longitude: p.Longitude, FiledDate: p.FiledDate, IssuedDate: p.IssuedDate,
        EstimatedValue: p.EstimatedValue, SquareFootage: p.SquareFootage, OwnerName: p.OwnerName,
        ContractorName: p.ContractorName, SourceUrl: p.SourceUrl, Fingerprint: p.Fingerprint,
        RecordType: p.RecordType, WorkType: p.WorkType, ExpirationDate: p.ExpirationDate,
        InspectionDate: p.InspectionDate, BusinessName: p.BusinessName, PropertyType: p.PropertyType);
}
