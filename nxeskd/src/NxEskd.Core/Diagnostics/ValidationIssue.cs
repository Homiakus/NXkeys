namespace NxEskd.Core.Diagnostics;

public sealed record ValidationIssue(
    string Code,
    IssueSeverity Severity,
    string Message,
    string? JsonPath = null,
    string? SheetId = null,
    string? ObjectId = null,
    string? SuggestedFix = null,
    bool CanAutoFix = false);
