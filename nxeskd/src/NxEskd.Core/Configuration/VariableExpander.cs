using System.Text.RegularExpressions;

namespace NxEskd.Core.Configuration;

public sealed class VariableExpander
{
    private static readonly Regex Token = new(@"\$\{(?<name>[A-Za-z0-9_\.\-]+)\}", RegexOptions.Compiled);
    private readonly IReadOnlyDictionary<string, string> _variables;

    public VariableExpander(IReadOnlyDictionary<string, string> variables) => _variables = variables;

    public string Expand(string value, bool throwOnMissing = true)
    {
        return Token.Replace(value, match =>
        {
            var key = match.Groups["name"].Value;
            if (_variables.TryGetValue(key, out var result)) return result;
            var env = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(env)) return env;
            if (throwOnMissing) throw new KeyNotFoundException($"Не определена переменная ${{{key}}}.");
            return match.Value;
        });
    }

    public static Dictionary<string, string> BuildDefault(ProfileDocument profile, string? partPath = null, string? rootDirectory = null)
    {
        var partDir = !string.IsNullOrWhiteSpace(partPath)
            ? Path.GetDirectoryName(Path.GetFullPath(partPath)) ?? profile.BaseDirectory
            : profile.BaseDirectory;
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROFILE_DIR"] = profile.BaseDirectory,
            ["PART_DIR"] = partDir,
            ["LOCAL_APP_DATA"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ["DOCUMENT_DESIGNATION"] = JsonNavigator.GetString(profile.Root, "$.job.document.designation", "UNNAMED")!,
            ["DOCUMENT_NAME"] = JsonNavigator.GetString(profile.Root, "$.job.document.name", "Без наименования")!
        };
        var nxRoot = !string.IsNullOrWhiteSpace(rootDirectory)
            ? rootDirectory
            : Environment.GetEnvironmentVariable("NX_ESKD_ROOT");
        if (!string.IsNullOrWhiteSpace(nxRoot))
            vars["NX_ESKD_ROOT"] = Path.GetFullPath(nxRoot);
        else
            vars["NX_ESKD_ROOT"] = profile.BaseDirectory;
        return vars;
    }
}

