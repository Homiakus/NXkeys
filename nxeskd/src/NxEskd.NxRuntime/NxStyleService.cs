using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.NxRuntime;

internal sealed class NxStyleService(NxServiceContext context)
{
    public void ApplyDraftingPreferences()
    {
        var preferences = NxReflection.Get(context.WorkPart, "Preferences");
        var drafting = NxReflection.Get(preferences, "Drafting")
                       ?? NxReflection.Get(context.WorkPart, "DraftingPreferences");
        if (drafting is null)
        {
            context.Report.Issues.Add(new(
                "NX_DRAFTING_PREFERENCES_UNAVAILABLE",
                IssueSeverity.ManualReview,
                "NX API Drafting Preferences не найден. Runtime не может подтвердить применение параметров стиля; проверьте PRT-шаблон."));
            return;
        }

        var settings = new[]
        {
            Apply(drafting, "dimension.arrowLengthMm",
                JsonNavigator.GetDouble(context.Profile.Root, "$.draftingStyles.dimensions.arrowLengthMm", 3.5),
                "Dimension.ArrowLength", "Dimensions.ArrowLength", "ArrowLength"),
            Apply(drafting, "dimension.firstDimensionOffsetMm",
                JsonNavigator.GetDouble(context.Profile.Root, "$.draftingStyles.dimensions.firstDimensionOffsetMm", 10.0),
                "Dimension.FirstOffset", "Dimensions.FirstOffset", "FirstDimensionOffset"),
            Apply(drafting, "dimension.parallelDimensionGapMm",
                JsonNavigator.GetDouble(context.Profile.Root, "$.draftingStyles.dimensions.parallelDimensionGapMm", 7.0),
                "Dimension.LineSpacing", "Dimensions.LineSpacing", "ParallelDimensionGap"),
            Apply(drafting, "hatching.defaultAngleDeg",
                JsonNavigator.GetDouble(context.Profile.Root, "$.draftingStyles.hatching.defaultAngleDeg", 45.0),
                "Hatching.Angle", "SectionHatching.Angle", "HatchAngle"),
            Apply(drafting, "hatching.spacingMm",
                JsonNavigator.GetDouble(context.Profile.Root, "$.draftingStyles.hatching.spacingMm", 3.0),
                "Hatching.Spacing", "SectionHatching.Spacing", "HatchSpacing")
        };

        var applied = settings.Count(result => result.Applied);
        context.Report.Metrics["draftingStyles.requested"] = settings.Length;
        context.Report.Metrics["draftingStyles.applied"] = applied;
        context.Report.Metrics["draftingStyles.unconfirmed"] = settings.Length - applied;

        foreach (var result in settings.Where(result => !result.Applied))
            context.Report.Issues.Add(new(
                "NX_DRAFTING_STYLE_NOT_APPLIED",
                IssueSeverity.Warning,
                $"NX API не подтвердил параметр стиля '{result.Name}' со значением '{result.Value}'. " +
                "Фактическое значение должно быть проверено в PRT-шаблоне.",
                ObjectId: result.Name));

        context.Log.Info($"Drafting styles: подтверждено {applied} из {settings.Length}; остальные значения должны наследоваться из шаблона.");
    }

    private static StyleApplyResult Apply(object target, string name, object value, params string[] aliases)
    {
        try
        {
            return new StyleApplyResult(name, value, NxReflection.Set(target, value, aliases));
        }
        catch
        {
            return new StyleApplyResult(name, value, false);
        }
    }

    private sealed record StyleApplyResult(string Name, object Value, bool Applied);
}
