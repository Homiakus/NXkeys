using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class NxProcessInfo
    {
        public int ProcessId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public override string ToString() => $"{Name}[{ProcessId}] {Path}".Trim();
    }

    public static class NxRuntimeService
    {
        private static readonly string[] ProcessNames = { "ugraf", "run_nx", "nx" };

        public static IReadOnlyList<NxProcessInfo> FindRunningProcesses()
        {
            var result = new List<NxProcessInfo>();
            foreach (string processName in ProcessNames)
            {
                Process[] processes;
                try { processes = Process.GetProcessesByName(processName); }
                catch { continue; }

                foreach (Process process in processes)
                {
                    using (process)
                    {
                        string path = string.Empty;
                        string description = string.Empty;
                        try { path = process.MainModule?.FileName ?? string.Empty; } catch { }
                        try { description = process.MainModule?.FileVersionInfo?.FileDescription ?? string.Empty; } catch { }

                        if (IsNxProcess(process.ProcessName, path, description))
                        {
                            result.Add(new NxProcessInfo
                            {
                                ProcessId = process.Id,
                                Name = process.ProcessName,
                                Path = path,
                                Description = description
                            });
                        }
                    }
                }
            }
            return result.OrderBy(x => x.ProcessId).ToList();
        }

        public static bool IsRunning() => FindRunningProcesses().Count > 0;

        public static string ResolveExecutable(Config config)
        {
            var candidates = new List<string>();
            AddCandidate(candidates, config?.Deployment?.NXExecutable);

            foreach (string variable in new[] { "UGII_ROOT_DIR", "UGII_BASE_DIR" })
            {
                string value = Environment.GetEnvironmentVariable(variable);
                AddRootCandidates(candidates, value);
            }

            if (config?.Scan?.InstallHints != null)
            {
                foreach (string root in config.Scan.InstallHints) AddRootCandidates(candidates, root);
            }

            foreach (string programFiles in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            })
            {
                if (string.IsNullOrWhiteSpace(programFiles)) continue;
                AddRootCandidates(candidates, Path.Combine(programFiles, "Siemens", "DesigncenterNX2512"));
                AddRootCandidates(candidates, Path.Combine(programFiles, "Siemens", "NX2512"));
                AddRootCandidates(candidates, Path.Combine(programFiles, "Siemens", "NX 2512"));
            }

            string preferred = config?.Profile?.NXVersion ?? "2512";
            string exact = candidates.FirstOrDefault(path => File.Exists(path) && VersionMatches(path, preferred));
            if (!string.IsNullOrWhiteSpace(exact)) return Path.GetFullPath(exact);

            string fallback = candidates.FirstOrDefault(File.Exists);
            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : Path.GetFullPath(fallback);
        }

        public static int Launch(Config config, string configPath, IEnumerable<string> nxArguments, out string error)
        {
            error = string.Empty;
            try
            {
                string executable = ResolveExecutable(config);
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                    throw new FileNotFoundException("Исполняемый файл Siemens NX не найден. Укажите deployment.nx_executable в профиле.", executable);

                string managedRoot = Path.GetFullPath(config.Deployment.ManagedRoot);
                string customDirs = Path.Combine(managedRoot, "custom_dirs.dat");
                if (!File.Exists(customDirs))
                    throw new FileNotFoundException("Файл custom_dirs.dat не найден. Сначала примените профиль NXKeys.", customDirs);

                string studioExe = Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe");
                if (File.Exists(studioExe))
                {
                    var leader = new ProcessStartInfo(studioExe)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = managedRoot,
                        CreateNoWindow = true
                    };
                    leader.ArgumentList.Add("--ensure-background");
                    leader.ArgumentList.Add("--config");
                    leader.ArgumentList.Add(configPath);
                    Process.Start(leader);
                }

                var start = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? managedRoot
                };
                start.Environment["UGII_CUSTOM_DIRECTORY_FILE"] = customDirs;
                foreach (string argument in nxArguments ?? Array.Empty<string>()) start.ArgumentList.Add(argument);

                Process process = Process.Start(start);
                if (process == null) throw new InvalidOperationException("Windows не создал процесс Siemens NX.");
                return process.Id;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return -1;
            }
        }

        private static bool IsNxProcess(string processName, string path, string description)
        {
            if (string.Equals(processName, "ugraf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "run_nx", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.Equals(processName, "nx", StringComparison.OrdinalIgnoreCase)) return false;

            string evidence = (path + " " + description).ToLowerInvariant();
            return evidence.Contains("siemens") || evidence.Contains("designcenter") ||
                   evidence.Contains("\\nxbin\\") || evidence.Contains("siemens nx");
        }

        private static void AddRootCandidates(List<string> candidates, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            string expanded = Config.ExpandPath(root);
            AddCandidate(candidates, expanded);
            AddCandidate(candidates, Path.Combine(expanded, "ugraf.exe"));
            AddCandidate(candidates, Path.Combine(expanded, "NXBIN", "ugraf.exe"));
            AddCandidate(candidates, Path.Combine(expanded, "UGII", "ugraf.exe"));
            AddCandidate(candidates, Path.Combine(expanded, "run_nx.exe"));
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string expanded = Config.ExpandPath(path.Trim().Trim('"'));
            if (!candidates.Contains(expanded, StringComparer.OrdinalIgnoreCase)) candidates.Add(expanded);
        }

        private static bool VersionMatches(string path, string preferred)
        {
            string normalizedPath = (path ?? string.Empty).ToLowerInvariant();
            string version = (preferred ?? string.Empty).ToLowerInvariant().Replace("nx", string.Empty);
            string major = version.Split('.')[0];
            return string.IsNullOrWhiteSpace(major) || normalizedPath.Contains(major);
        }
    }
}
