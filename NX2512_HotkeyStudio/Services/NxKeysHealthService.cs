using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public static class NxKeysHealthService
    {
        private static readonly string[] NxKeysMenuNames =
        {
            "nxkeys_generated.men",
            "nxkeys_command_bridge.men",
            "nxkeys_ribbon.rtb",
            "nxkeys_toolbar.tbr"
        };

        public static NxKeysHealthReport Check(Config config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.ApplyDefaults();
            var report = new NxKeysHealthReport { ManagedRoot = config.Deployment.ManagedRoot };

            report.MenuFiles = FindNxKeysMenuFiles(config.Deployment.ManagedRoot);
            report.StaleFiles = report.MenuFiles.Where(file => !file.IsExpectedVersion).ToList();
            report.MenuScriptVersionOk = report.MenuFiles.Count > 0 && report.StaleFiles.Count == 0;

            ReadBridgeState(report);
            foreach (NxProcessInfo process in NxRuntimeService.FindRunningProcesses())
            {
                report.NxRunning = true;
                report.NxProcesses.Add(process.ToString());
            }
            ReadManagedPackageState(config, report);
            return report;
        }

        private static List<NxKeysMenuFileStatus> FindNxKeysMenuFiles(string managedRoot)
        {
            var result = new List<NxKeysMenuFileStatus>();
            if (string.IsNullOrWhiteSpace(managedRoot) || !Directory.Exists(managedRoot)) return result;
            try
            {
                foreach (string path in Directory.GetFiles(managedRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (!NxKeysMenuNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)) continue;
                    result.Add(ReadMenuStatus(path));
                }
            }
            catch { }
            return result.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static NxKeysMenuFileStatus ReadMenuStatus(string path)
        {
            try
            {
                string text = File.ReadAllText(path);
                Match match = Regex.Match(text, @"(?m)^VERSION\s+(\d+)\s*$");
                int version = match.Success && int.TryParse(match.Groups[1].Value, out int parsed) ? parsed : 0;
                int expected = MenuScriptDefaults.ExpectedVersionForPath(path);
                return new NxKeysMenuFileStatus { Path = path, Version = version, IsExpectedVersion = version == expected, IsStale = version != expected };
            }
            catch
            {
                return new NxKeysMenuFileStatus { Path = path, Version = 0, IsExpectedVersion = false, IsStale = true };
            }
        }

        private static void ReadBridgeState(NxKeysHealthReport report)
        {
            string root = NxCommandBridgeClient.BridgeRoot;
            report.BridgeLoaded = IsLiveBridgeStatus(Path.Combine(root, "status.json"));
            report.PendingCount = CountFiles(Path.Combine(root, "pending"), "*.json");
            report.CompletedCount = CountFiles(Path.Combine(root, "completed"), "*.json");
            report.FailedCount = CountFiles(Path.Combine(root, "failed"), "*.json");
            report.LastFailures = RecentResultLines(Path.Combine(root, "failed"), 6);
        }

        private static bool IsLiveBridgeStatus(string statusPath)
        {
            if (!File.Exists(statusPath)) return false;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(statusPath)))
                {
                    if (!document.RootElement.TryGetProperty("process_id", out JsonElement pidValue) || !pidValue.TryGetInt32(out int pid)) return false;
                    using (Process process = Process.GetProcessById(pid)) return !process.HasExited;
                }
            }
            catch { return false; }
        }

        private static void ReadManagedPackageState(Config config, NxKeysHealthReport report)
        {
            string root = Path.GetFullPath(config.Deployment.ManagedRoot);
            string manifestPath = Path.Combine(root, "package-manifest.json");
            string bridgeDll = Path.Combine(root, "custom", "application", "NX2512_CommandBridge.dll");
            report.BridgeDllLocked = File.Exists(bridgeDll) && IsLocked(bridgeDll);

            PackageManifest manifest = null;
            try { if (File.Exists(manifestPath)) manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(manifestPath)); }
            catch { report.HashMismatches.Add(manifestPath + " (повреждённый манифест)"); }

            if (manifest?.Files == null || manifest.Files.Count == 0)
            {
                report.MissingManagedFiles.Add(manifestPath);
                report.ManagedPackageOk = false;
                return;
            }

            foreach (PackageFileEntry entry in manifest.Files)
            {
                string path = Path.GetFullPath(Path.Combine(root, entry.RelativePath ?? string.Empty));
                if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    report.HashMismatches.Add(path + " (выход за managed_root)");
                    continue;
                }
                if (!File.Exists(path))
                {
                    if (entry.Required) report.MissingManagedFiles.Add(path);
                    continue;
                }
                try
                {
                    string actual = BackupEngine.ComputeSha256(path);
                    if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase)) report.HashMismatches.Add(path);
                }
                catch { report.HashMismatches.Add(path + " (ошибка чтения)"); }
            }

            report.ManagedPackageOk = report.MissingManagedFiles.Count == 0 && report.HashMismatches.Count == 0;
        }

        private static bool IsLocked(string path)
        {
            try { using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } return false; }
            catch (IOException) { return true; }
            catch { return false; }
        }

        private static int CountFiles(string directory, string pattern) => Directory.Exists(directory) ? Directory.GetFiles(directory, pattern).Length : 0;

        private static List<string> RecentResultLines(string directory, int count)
        {
            if (!Directory.Exists(directory)) return new List<string>();
            return new DirectoryInfo(directory).GetFiles("*.result.txt")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(count)
                .Select(file => file.Name + ": " + SafeFirstLine(file.FullName))
                .ToList();
        }

        private static string SafeFirstLine(string path)
        {
            try { return File.ReadLines(path).FirstOrDefault() ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
