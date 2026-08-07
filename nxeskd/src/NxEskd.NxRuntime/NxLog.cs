using NXOpen;

namespace NxEskd.NxRuntime;

internal sealed class NxLog
{
    private readonly ListingWindow _listing;
    private readonly List<string> _lines = [];

    public NxLog(Session session)
    {
        _listing = session.ListingWindow;
        try { _listing.Open(); } catch { }
    }

    public IReadOnlyList<string> Lines => _lines;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        _lines.Add(line);
        try { _listing.WriteLine(line); } catch { }
    }
}
