namespace PermitTorch.Api.Features.Feedback;

/// <summary>The app page a piece of feedback was sent from. Only a plain same-site path is
/// kept — one leading slash, no scheme or host, no query or fragment — so nothing stored can
/// point a reader somewhere else. Anything else is dropped, never a reason to refuse feedback.</summary>
public static class FeedbackPagePath
{
    public const int MaxLength = 500;

    public static string? Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxLength) return null;
        // "//host" is protocol-relative; "/\host" is read the same way by browsers.
        if (path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal)) return null;
        foreach (var c in path)
            if (c is '\\' or '?' or '#' || char.IsControl(c)) return null;
        return path;
    }
}
