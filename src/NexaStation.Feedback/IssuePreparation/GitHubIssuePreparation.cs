using System.Text;
using NexaStation.Recovery;

namespace NexaStation.Feedback.IssuePreparation;

public sealed record GitHubIssuePreparationRequest(
    string Version,
    string ReleaseChannel,
    string WindowsVersion,
    string Category,
    string ExpectedBehavior,
    string ActualBehavior,
    string ReproductionSteps,
    string SafeModeResult,
    string? DiagnosticCorrelationId,
    bool BundleCreated);

public sealed record GitHubIssuePreparationResult(string Markdown, Uri IssuePage, string PrivacyReminder);

public interface IGitHubIssuePreparationService
{
    GitHubIssuePreparationResult Prepare(GitHubIssuePreparationRequest request);
}

public sealed class GitHubIssuePreparationService(ISensitiveDataRedactor redactor) : IGitHubIssuePreparationService
{
    public GitHubIssuePreparationResult Prepare(GitHubIssuePreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        StringBuilder markdown = new();
        markdown.AppendLine("## NexaStation alpha report");
        markdown.AppendLine();
        Append(markdown, "Version", request.Version);
        Append(markdown, "Release channel", request.ReleaseChannel);
        Append(markdown, "Windows version", request.WindowsVersion);
        Append(markdown, "Category", request.Category);
        markdown.AppendLine();
        Section(markdown, "Expected behavior", request.ExpectedBehavior);
        Section(markdown, "Actual behavior", request.ActualBehavior);
        Section(markdown, "Reproduction steps", request.ReproductionSteps);
        Section(markdown, "Safe Mode result", request.SafeModeResult);
        Append(markdown, "Diagnostic correlation ID", request.DiagnosticCorrelationId ?? "Not provided");
        markdown.AppendLine();
        markdown.AppendLine(request.BundleCreated ? "A local reproduction bundle was created and can be attached manually after reviewing it." : "No reproduction bundle was created.");
        return new(redactor.Redact(markdown.ToString()), new Uri("https://github.com/"), "Do not attach ROMs, games, photos, credentials, private paths, or a full database. Review every attachment before uploading.");
    }

    private static void Append(StringBuilder builder, string label, string value) => builder.Append("- **").Append(label).Append(":** ").AppendLine(value);
    private void Section(StringBuilder builder, string title, string value)
    {
        builder.Append("### ").AppendLine(title);
        builder.AppendLine(redactor.Redact(value));
        builder.AppendLine();
    }
}
