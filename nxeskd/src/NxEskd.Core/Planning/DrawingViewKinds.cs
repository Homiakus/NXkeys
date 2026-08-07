namespace NxEskd.Core.Planning;

public static class DrawingViewKinds
{
    public const string Base = "base";
    public const string Projected = "projected";
    public const string Section = "section";
    public const string HalfSection = "half_section";
    public const string SteppedSection = "stepped_section";
    public const string Detail = "detail";
    public const string FlatPattern = "flat_pattern";

    private static readonly HashSet<string> SupportedSet = new(StringComparer.OrdinalIgnoreCase)
    {
        Base,
        Projected,
        Section,
        HalfSection,
        SteppedSection,
        Detail,
        FlatPattern
    };

    public static IReadOnlyList<string> RuntimeSupported { get; } =
        SupportedSet.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool IsRuntimeSupported(string? value)
        => !string.IsNullOrWhiteSpace(value) && SupportedSet.Contains(value);

    public static bool IsSection(string? value)
        => value is not null &&
           (value.Equals(Section, StringComparison.OrdinalIgnoreCase)
            || value.Equals(HalfSection, StringComparison.OrdinalIgnoreCase)
            || value.Equals(SteppedSection, StringComparison.OrdinalIgnoreCase));

    public static bool IsDependent(string? value)
        => value is not null &&
           (value.Equals(Projected, StringComparison.OrdinalIgnoreCase)
            || value.Equals(Detail, StringComparison.OrdinalIgnoreCase)
            || IsSection(value));
}
