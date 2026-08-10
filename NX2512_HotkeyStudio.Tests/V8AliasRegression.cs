using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NX2512_HotkeyStudio.Models;

internal static class V8AliasRegression
{
    [ModuleInitializer]
    internal static void Verify()
    {
        string profilePath = FindRepositoryFile(Path.Combine("config", "nx2512-v8-profile.json"));
        Config config = Config.Load(profilePath);

        LeaderSequenceItem layerSettings = config.LeaderKey.Sequences.FirstOrDefault(item =>
            string.Equals(item.ModuleID, "v8_m", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Command?.ID, "UG_LAYER_SETTINGS", StringComparison.OrdinalIgnoreCase) &&
            item.Path != null &&
            item.Path.SequenceEqual(new[] { "M", "L", "S" }, StringComparer.OrdinalIgnoreCase));

        if (layerSettings == null)
            throw new InvalidOperationException(
                "Modeling secondary alias M->L->S must remain Manage -> Layer -> Settings inside v8_m.");

        // The module prefix is injected automatically by LeaderKeyEngine. The user
        // therefore presses CapsLock, then the semantic path M -> L -> S; M must not
        // be stripped as though it were a duplicate Modeling prefix.
        if (!string.Equals(layerSettings.Sequence, "M M L S", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Expected runtime DFA sequence 'M M L S' (automatic Modeling prefix + Manage path), got: " +
                layerSettings.Sequence);

        // workspace_key belongs to an already-open Workspace. It must never become
        // a terminal module-root action, otherwise M cannot also be the Manage root
        // for M->L, M->M, M->N and other nested operations.
        bool terminalManage = config.LeaderKey.Sequences.Any(item =>
            string.Equals(item.ModuleID, "v8_m", StringComparison.OrdinalIgnoreCase) &&
            item.Path != null && item.Path.Count == 1 &&
            string.Equals(item.Path[0], "M", StringComparison.OrdinalIgnoreCase));
        if (terminalManage)
            throw new InvalidOperationException(
                "Modeling Manage key M must be a submenu root, not a terminal workspace-local command.");
    }

    private static string FindRepositoryFile(string relativePath)
    {
        string current = Directory.GetCurrentDirectory();
        for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            string candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current);
        }
        throw new FileNotFoundException("Repository file was not found.", relativePath);
    }
}