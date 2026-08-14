namespace NxEskd.Core.Layout;

public sealed class LayoutSolver
{
    public LayoutResult Solve(
        Rect2 workArea,
        IEnumerable<Rect2> reserved,
        IEnumerable<LayoutItem> items,
        double gap,
        int maxIterations = 200,
        IReadOnlyDictionary<string, Rect2>? fixedPlacements = null)
    {
        var reservedList = reserved.ToList();
        var ordered = OrderDependencyFirst(items);
        var placements = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase);
        var knownPlacements = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase);
        if (fixedPlacements is not null)
            foreach (var pair in fixedPlacements) knownPlacements[pair.Key] = pair.Value;

        var unresolved = new List<string>();
        var iterations = 0;

        foreach (var item in ordered)
        {
            var relational = IsStrictProjection(item);
            var candidates = GenerateCandidates(workArea, item, gap, knownPlacements).ToList();
            var placed = false;
            var itemIterations = 0;
            foreach (var candidate in candidates)
            {
                iterations++;
                itemIterations++;
                if (itemIterations > maxIterations) break;
                if (!workArea.Contains(candidate)) continue;
                if (reservedList.Any(r => r.Intersects(candidate, gap))) continue;
                if (knownPlacements.Any(pair => pair.Value.Intersects(candidate, gap))) continue;
                placements[item.Id] = candidate;
                knownPlacements[item.Id] = candidate;
                placed = true;
                break;
            }

            if (!placed)
            {
                unresolved.Add(item.Id);
                if (relational && !string.IsNullOrWhiteSpace(item.ParentId)
                    && !knownPlacements.ContainsKey(item.ParentId))
                {
                    // A projected view without a resolved parent must never be placed as an unrelated free rectangle.
                    continue;
                }
            }
        }

        return new LayoutResult(placements, unresolved, iterations);
    }

    private static IReadOnlyList<LayoutItem> OrderDependencyFirst(IEnumerable<LayoutItem> items)
    {
        var ranked = items
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.Bounds.Width * item.Bounds.Height)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var byId = ranked.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<LayoutItem>();

        void Visit(LayoutItem item)
        {
            if (state.TryGetValue(item.Id, out var current))
            {
                if (current == 2 || current == 1) return;
            }

            state[item.Id] = 1;
            if (!string.IsNullOrWhiteSpace(item.ParentId)
                && byId.TryGetValue(item.ParentId, out var parent))
                Visit(parent);
            state[item.Id] = 2;
            result.Add(item);
        }

        foreach (var item in ranked) Visit(item);
        return result;
    }

    private static IEnumerable<Rect2> GenerateCandidates(
        Rect2 area,
        LayoutItem item,
        double gap,
        IReadOnlyDictionary<string, Rect2> knownPlacements)
    {
        var w = item.Bounds.Width;
        var h = item.Bounds.Height;
        if (IsStrictProjection(item))
        {
            if (string.IsNullOrWhiteSpace(item.ParentId)
                || !knownPlacements.TryGetValue(item.ParentId, out var parent))
                yield break;

            foreach (var candidate in ProjectionCandidates(parent, w, h, gap, item.Relation!))
                yield return candidate;
            yield break;
        }

        var anchors = new Dictionary<string, Rect2>(StringComparer.OrdinalIgnoreCase)
        {
            ["center"] = new(area.Center.X - w / 2, area.Center.Y - h / 2, w, h),
            ["center_left"] = new(area.Left + gap, area.Center.Y - h / 2, w, h),
            ["center_right"] = new(area.Right - w - gap, area.Center.Y - h / 2, w, h),
            ["top_left"] = new(area.Left + gap, area.Top - h - gap, w, h),
            ["top_right"] = new(area.Right - w - gap, area.Top - h - gap, w, h),
            ["bottom_left"] = new(area.Left + gap, area.Bottom + gap, w, h),
            ["bottom_right"] = new(area.Right - w - gap, area.Bottom + gap, w, h),
            ["bottom_center"] = new(area.Center.X - w / 2, area.Bottom + gap, w, h),
            ["top_center"] = new(area.Center.X - w / 2, area.Top - h - gap, w, h)
        };

        if (!string.IsNullOrWhiteSpace(item.PreferredAnchor)
            && anchors.TryGetValue(item.PreferredAnchor, out var preferred))
            yield return preferred;
        foreach (var candidate in anchors.Values.Distinct()) yield return candidate;

        var spanX = Math.Max(0.0, area.Width - w);
        var spanY = Math.Max(0.0, area.Height - h);
        var minStep = Math.Max(gap, 2.5);
        var step = Math.Max(minStep, Math.Max(spanX, spanY) / 16.0);
        for (var y = area.Top - h; y >= area.Bottom - 1e-6; y -= step)
            for (var x = area.Left; x <= area.Right - w + 1e-6; x += step)
                yield return new Rect2(x, y, w, h);
    }

    private static IEnumerable<Rect2> ProjectionCandidates(
        Rect2 parent,
        double width,
        double height,
        double gap,
        string relation)
    {
        var normalized = relation.Trim().ToLowerInvariant();
        var step = Math.Max(gap, 2.5);
        for (var index = 1; index <= 24; index++)
        {
            var distance = step * index;
            yield return normalized switch
            {
                "top" or "up" => new Rect2(
                    parent.Center.X - width / 2,
                    parent.Top + distance,
                    width,
                    height),
                "bottom" or "down" => new Rect2(
                    parent.Center.X - width / 2,
                    parent.Bottom - height - distance,
                    width,
                    height),
                "left" => new Rect2(
                    parent.Left - width - distance,
                    parent.Center.Y - height / 2,
                    width,
                    height),
                _ => new Rect2(
                    parent.Right + distance,
                    parent.Center.Y - height / 2,
                    width,
                    height)
            };
        }
    }

    private static bool IsStrictProjection(LayoutItem item)
        => item.Kind.Equals("projected_view", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(item.ParentId)
           && !string.IsNullOrWhiteSpace(item.Relation);
}
