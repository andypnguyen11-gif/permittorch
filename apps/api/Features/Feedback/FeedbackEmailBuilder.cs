using System.Net;
using System.Text;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;

namespace PermitTorch.Api.Features.Feedback;

/// <summary>The email that tells the owner about new feedback. Built only from the saved row
/// and its user — nothing about the moment of sending — so a retry produces the exact same
/// email under the same Resend idempotency key and is never delivered twice.</summary>
public static class FeedbackEmailBuilder
{
    public const int SubjectPreviewLength = 60;

    public static string IdempotencyKey(Guid feedbackId) => $"feedback:{feedbackId}";

    public static string TypeLabel(FeedbackType type) => type switch
    {
        FeedbackType.Idea => "Idea",
        FeedbackType.Problem => "Problem",
        FeedbackType.DataRequest => "Data request",
        _ => "Other",
    };

    public static EmailMessage Build(FeedbackSubmission feedback, AppUser user, string notifyTo) =>
        new(notifyTo, Subject(feedback), Html(feedback, user), IdempotencyKey(feedback.Id), ReplyTo: user.Email);

    private static string Subject(FeedbackSubmission feedback)
    {
        var preview = feedback.Message.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        if (preview.Length > SubjectPreviewLength)
        {
            var cut = SubjectPreviewLength;
            if (char.IsHighSurrogate(preview[cut - 1])) cut--;   // never split an emoji
            preview = preview[..cut] + "…";
        }
        return $"[Feedback · {TypeLabel(feedback.Type)}] {preview}";
    }

    private static string Html(FeedbackSubmission feedback, AppUser user)
    {
        var html = new StringBuilder();
        html.Append("<h2>New feedback</h2><table>");
        Row(html, "Type", TypeLabel(feedback.Type));
        Row(html, "From", user.Email);
        if (!string.IsNullOrWhiteSpace(user.Organization?.Name)) Row(html, "Company", user.Organization.Name);
        if (feedback.PagePath is not null) Row(html, "Page", feedback.PagePath);
        // Minutes only: the first send holds .NET ticks, a retry reads Postgres microseconds.
        Row(html, "Sent", feedback.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        html.Append("</table><p>").Append(Encode(feedback.Message)).Append("</p>");
        return html.ToString();
    }

    private static void Row(StringBuilder html, string label, string value) =>
        html.Append("<tr><td><strong>").Append(label).Append("</strong></td><td>")
            .Append(Encode(value)).Append("</td></tr>");

    private static string Encode(string value) =>
        WebUtility.HtmlEncode(value.Replace("\r\n", "\n")).Replace("\n", "<br>");
}
