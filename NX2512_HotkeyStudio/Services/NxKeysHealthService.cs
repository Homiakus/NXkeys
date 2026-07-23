using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
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
            "nxkeys_toolbar.tbr",
            "rbn_nxkeys.rtb"
        };

        public static NxKeysHealthReport Check(Config config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.ApplyDefaults();

            NxKeysHealthReport report = new NxKeysHealthReport
            {
                ManagedRoot = config.Deployment.ManagedRoot
            };

            report.MenuFiles = FindNxKeysMenuFiles(config.Deployment.ManagedRoot);
            report.StaleFiles = report.MenuFiles.Where(f => !f.IsExpectedVersion).ToList();
            report.MenuScriptVersionOk = report.StaleFiles.Count == 0 &&
                                         report.MenuFiles
                                             .Where(f => f.Path.StartsWith(config.Deployment.ManagedRoot, StringComparison.OrdinalIgnoreCase))
                                             .All(f => f.IsExpectedVersion);

            ReadBridgeState(report);
            ReadNxProcesses(report);
            ReadManagedPackageState(config, report);

            return report;
        }

        private static List<NxKeysMenuFileStatus> FindNxKeysMenuFiles(string managedRoot)
        {
            List<NxKeysMenuFileStatus> files = new List<NxKeysMenuFileStatus>();
            foreach (string root in CandidateMenuRoots(managedRoot))
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(path);
                    if (!NxKeysMenuNames.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    NxKeysMenuFileStatus status = ReadMenuStatus(path);
                    if (status != null) files.Add(status);
                }
            }
            return files
                .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> CandidateMenuRoots(string managedRoot)
        {
            if (!string.IsNullOrWhiteSpace(managedRoot)) yield return managedRoot;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            yield return Path.Combine(localAppData, "Siemens");
            yield return Path.Combine(appData, "Siemens");
        }

        private static NxKeysMenuFileStatus ReadMenuStatus(string path)
        {
            try
            {
                string text;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream, true))
                {
                    text = reader.ReadToEnd();
                }

                Match match = Regex.Match(text, @"(?m)^VERSION\s+(\d+)\s*$");
                int version = match.Success && int.TryParse(match.Groups[1].Value, out int v) ? v : 0;
                return new NxKeysMenuFileStatus
                {
                    Path = path,
                    Version = version,
                    IsExpectedVersion = version == MenuScriptDefaults.ExpectedVersionForPath(path),
                    IsStale = version != MenuScriptDefaults.ExpectedVersionForPath(path)
                };
            }
            catch
            {
                return new NxKeysMenuFileStatus
                {
                    Path = path,
                    Version = 0,
                    IsExpectedVersion = false,
                    IsStale = true
                };
            }
        }

        private static void ReadBridgeState(NxKeysHealthReport report)
        {
            string root = NxCommandBridgeClient.BridgeRoot;
            string pending = Path.Combine(root, "pending");
            string completed = Path.Combine(root, "completed");
            string failed = Path.Combine(root, "failed");
            string statusPath = Path.Combine(root, "status.json");

            report.BridgeLoaded = IsLiveBridgeStatus(statusPath);
            report.PendingCount = CountFiles(pending, "*.json");
            report.CompletedCount = CountFiles(completed, "*.json");
            report.FailedCount = CountFiles(failed, "*.json");
            report.LastFailures = RecentResultLines(failed, 6);
        }

        private static void ReadNxProcesses(NxKeysHealthReport report)
        {
            foreach (string processName in new[] { "ugraf", "run_nx", "nx" })
            {
                foreach (Process proc in Process.GetProcessesByName(processName))
                {
                    using (proc)
                    {
                        string path = string.Empty;
                        try { path = proc.MainModule?.FileName ?? string.Empty; } catch { }
                        bool isNx = processName == "ugraf" || processName == "run_nx" ||
                                    path.IndexOf(@"\Siemens\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    path.IndexOf(@"\DesigncenterNX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    path.IndexOf(@"\NXBIN\", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!isNx) continue;
                        report.NxRunning = true;
                        report.NxProcesses.Add($"{proc.ProcessName}[{proc.Id}] {path}");
                    }
                }
            }
        }

        private static bool IsLiveBridgeStatus(string statusPath)
        {
            if (!File.Exists(statusPath)) return false;
            try
            {
                using (FileStream stream = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (JsonDocument doc = JsonDocument.Parse(stream))
                {
                    if (doc.RootElement.TryGetProperty("process_id", out JsonElement pidElement) &&
                        pidElement.TryGetInt32(out int pid) &&
                        pid > 0)
                    {
                        try
                        {
                            Process proc = Process.GetProcessById(pid);
                            using (proc)
                            {
                                return !proc.HasExited;
                            }
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
            }
            catch { }
            return true;
        }

        private static void ReadManagedPackageState(Config config, NxKeysHealthReport report)
        {
            string root = config.Deployment.ManagedRoot;
            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] required =
            {
                "NX2512_HotkeyStudio.exe",
                "NX2512_HotkeyStudio.dll",
                Path.Combine("custom", "application", "NX2512_CommandBridge.dll"),
                Path.Combine("custom", "application", "nxkeys_command_bridge.men"),
                Path.Combine("custom", "startup", config.Deployment.OverlayFilename),
                Path.Combine("custom", "startup", "nxkeys_ribbon.rtb"),
                Path.Combine("custom", "startup", "nxkeys_toolbar.tbr")
            };

            foreach (string relative in required)
            {
                string path = Path.Combine(root, relative);
                if (!File.Exists(path)) report.MissingManagedFiles.Add(path);
            }

            CompareHashIfPresent(Path.Combine(appDir, "NX2512_HotkeyStudio.exe"), Path.Combine(root, "NX2512_HotkeyStudio.exe"), report);
            CompareHashIfPresent(Path.Combine(appDir, "NX2512_HotkeyStudio.dll"), Path.Combine(root, "NX2512_HotkeyStudio.dll"), report);

            string bridgeDll = Path.Combine(root, "custom", "application", "NX2512_CommandBridge.dll");
            report.BridgeDllLocked = File.Exists(bridgeDll) && IsLocked(bridgeDll);
            report.ManagedPackageOk = report.MissingManagedFiles.Count == 0 && report.HashMismatches.Count == 0;
        }

        private static void CompareHashIfPresent(string source, string installed, NxKeysHealthReport report)
        {
            if (!File.Exists(source) || !File.Exists(installed)) return;
            try
            {
                string sourceHash = BackupEngine.ComputeSha256(source);
                string installedHash = BackupEngine.ComputeSha256(installed);
                if (!string.Equals(sourceHash, installedHash, StringComparison.OrdinalIgnoreCase))
                {
                    report.HashMismatches.Add(installed);
                }
            }
            catch { }
        }

        private static bool IsLocked(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int CountFiles(string directory, string pattern)
        {
            return Directory.Exists(directory) ? Directory.GetFiles(directory, pattern).Length : 0;
        }

        private static List<string> RecentResultLines(string directory, int count)
        {
            if (!Directory.Exists(directory)) return new List<string>();
            return new DirectoryInfo(directory)
                .GetFiles("*.result.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(count)
                .Select(f =>
                {
                    string first = string.Empty;
                    try { first = File.ReadLines(f.FullName).FirstOrDefault() ?? string.Empty; } catch { }
                    return $"{f.Name}: {first}";
                })
                .ToList();
        }
    }
}
