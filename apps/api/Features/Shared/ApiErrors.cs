namespace PermitTorch.Api.Features.Shared;

/// <summary>Every non-2xx body is the LOCKED shape { "error": string } (master §6).</summary>
public static class ApiErrors
{
    public static IResult BadRequest(string message) => Error(message, StatusCodes.Status400BadRequest);
    public static IResult NotFound(string message) => Error(message, StatusCodes.Status404NotFound);
    public static IResult Forbidden(string message) => Error(message, StatusCodes.Status403Forbidden);
    public static IResult Conflict(string message) => Error(message, StatusCodes.Status409Conflict);

    private static IResult Error(string message, int statusCode) =>
        Results.Json(new ErrorResponse(message), ApiJson.Options, statusCode: statusCode);
}
