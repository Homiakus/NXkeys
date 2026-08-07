namespace NxEskd.Core.Layout;

public readonly record struct Point2(double X, double Y);

public readonly record struct Rect2(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Right => X + Width;
    public double Bottom => Y;
    public double Top => Y + Height;
    public Point2 Center => new(X + Width / 2.0, Y + Height / 2.0);

    public bool Intersects(Rect2 other, double gap = 0)
        => Left - gap < other.Right && Right + gap > other.Left && Bottom - gap < other.Top && Top + gap > other.Bottom;

    public bool Contains(Rect2 other, double gap = 0)
        => other.Left >= Left + gap && other.Right <= Right - gap && other.Bottom >= Bottom + gap && other.Top <= Top - gap;

    public Rect2 MoveTo(double x, double y) => new(x, y, Width, Height);
}

public sealed record LayoutItem(
    string Id,
    string Kind,
    Rect2 Bounds,
    int Priority,
    string? ParentId = null,
    string? PreferredAnchor = null,
    string? Relation = null);

public sealed record LayoutResult(
    IReadOnlyDictionary<string, Rect2> Placements,
    IReadOnlyList<string> Unresolved,
    int Iterations);
