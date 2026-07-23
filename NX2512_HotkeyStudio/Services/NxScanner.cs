using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class ScanResult
    {
        public List<string> DiscoveredRoots { get; } = new List<string>();
        public List<string> MenuFiles { get; } = new List<string>();
        public List<string> RoleFiles { get; } = new List<string>();
        public List<string> LauncherFiles { get; } = new List<string>();
        public CatalogIndex Catalog { get; } = new CatalogIndex();
        public List<string> Warnings { get; } = new List<string>();
    }

    public static class NxScanner
    {
        public static ScanResult Scan(Config config, string documentationCatalogDir = null)
        {
            var result = new ScanResult();

            List<string> roots = DiscoverRoots(config);
            foreach (string r in roots)
            {
                if (!result.DiscoveredRoots.Contains(r, StringComparer.OrdinalIgnoreCase))
                {
                    result.DiscoveredRoots.Add(r);
                }
            }

            // 1. Scan live menu files across discovered roots. Parsing happens after cache lookup.
            int maxDepth = config?.Scan?.MaxDepth ?? 8;
            int maxFiles = config?.Scan?.MaxFiles ?? 25000;
            int fileCount = 0;

            HashSet<string> menuExts = new HashSet<string>(config?.Scan?.MenuExtensions ?? new List<string> { ".men", ".tbr", ".rtb", ".gly", ".abr" }, StringComparer.OrdinalIgnoreCase);
            HashSet<string> roleExts = new HashSet<string>(config?.Scan?.RoleExtensions ?? new List<string> { ".mtx" }, StringComparer.OrdinalIgnoreCase);
            HashSet<string> launcherExts = new HashSet<string>(config?.Scan?.LauncherExtensions ?? new List<string> { ".bat", ".cmd", ".ps1" }, StringComparer.OrdinalIgnoreCase);

            foreach (string root in result.DiscoveredRoots)
            {
                if (!Directory.Exists(root)) continue;

                ScanDirectoryRecursive(root, 0, maxDepth, ref fileCount, maxFiles, path =>
                {
                    string ext = Path.GetExtension(path);
                    if (menuExts.Contains(ext))
                    {
                        result.MenuFiles.Add(path);
                    }
                    else if (roleExts.Contains(ext))
                    {
                        result.RoleFiles.Add(path);
                    }
                    else if (launcherExts.Contains(ext))
                    {
                        result.LauncherFiles.Add(path);
                    }
                });

                if (fileCount >= maxFiles)
                {
                    result.Warnings.Add($"Scan stopped after reaching max_files limit of {maxFiles}");
                    break;
                }
            }

            result.Catalog.Commands.Clear();
            result.Catalog.CrosswalkEntries.Clear();
            if (config?.Performance?.CatalogCacheEnabled == true && TryLoadCatalogCache(config, result))
            {
                result.Warnings.Add("Catalog cache hit.");
                return result;
            }

            if (!string.IsNullOrWhiteSpace(documentationCatalogDir) && Directory.Exists(documentationCatalogDir))
            {
                CsvCatalogLoader.LoadFromDirectory(result.Catalog, documentationCatalogDir);
            }
            else
            {
                string defaultDocDir = @"c:\Users\KDFX Modes\Desktop\NX2512_Full_Function_API_Catalog_20260722_061449";
                if (Directory.Exists(defaultDocDir))
                {
                    CsvCatalogLoader.LoadFromDirectory(result.Catalog, defaultDocDir);
                }
            }
            foreach (string menuFile in result.MenuFiles)
            {
                MenuScriptParser.ParseFile(result.Catalog, menuFile);
            }
            if (config?.Performance?.CatalogCacheEnabled == true)
            {
                TryWriteCatalogCache(config, result);
            }

            return result;
        }

        private static bool TryLoadCatalogCache(Config config, ScanResult result)
        {
            try
            {
                string signature = ComputeCatalogSignature(result.MenuFiles);
                string path = CatalogCachePath(config, signature);
                if (!File.Exists(path)) return false;
                CatalogCachePayload payload = JsonSerializer.Deserialize<CatalogCachePayload>(File.ReadAllText(path));
                if (payload == null || payload.Signature != signature || payload.NXVersion != config.Profile.NXVersion || payload.Commands == null) return false;
                foreach (CommandItem item in payload.Commands)
                {
                    result.Catalog.AddOrMerge(item);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryWriteCatalogCache(Config config, ScanResult result)
        {
            try
            {
                string signature = ComputeCatalogSignature(result.MenuFiles);
                string path = CatalogCachePath(config, signature);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                CatalogCachePayload payload = new CatalogCachePayload
                {
                    NXVersion = config.Profile.NXVersion,
                    Signature = signature,
                    CreatedUtc = DateTime.UtcNow.ToString("O"),
                    Commands = result.Catalog.Commands.Values.ToList()
                };
                File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch
            {
                // Cache is an optimization only.
            }
        }

        private static string CatalogCachePath(Config config, string signature)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "cache");
            string version = string.IsNullOrWhiteSpace(config?.Profile?.NXVersion) ? "unknown" : config.Profile.NXVersion.Replace('\\', '_').Replace('/', '_').Replace(':', '_').Replace(' ', '_');
            return Path.Combine(root, $"catalog-{version}-{signature.Substring(0, 16)}.json");
        }

        private static string ComputeCatalogSignature(IEnumerable<string> paths)
        {
            var lines = new List<string>();
            foreach (string path in paths ?? Enumerable.Empty<string>())
            {
                try
                {
                    FileInfo info = new FileInfo(path);
                    lines.Add($"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
                }
                catch
                {
                    lines.Add($"{path}|missing");
                }
            }
            lines.Sort(StringComparer.OrdinalIgnoreCase);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private sealed class CatalogCachePayload
        {
            public string NXVersion { get; set; } = string.Empty;
            public string Signature { get; set; } = string.Empty;
            public string CreatedUtc { get; set; } = string.Empty;
            public List<CommandItem> Commands { get; set; } = new List<CommandItem>();
        }

        public static List<string> DiscoverRoots(Config config)
        {
            var roots = new List<string>();

            string[] envVars = { "UGII_BASE_DIR", "UGII_ROOT_DIR", "UGII_USER_PROFILE_DIR", "UGII_SITE_DIR", "UGOPEN", "UGII_CUSTOM_DIRECTORY_FILE" };
            foreach (string varName in envVars)
            {
                string val = Environment.GetEnvironmentVariable(varName);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    val = Config.ExpandPath(val);
                    if (File.Exists(val)) val = Path.GetDirectoryName(val);
                    if (!string.IsNullOrEmpty(val) && Directory.Exists(val)) roots.Add(val);
                }
            }

            if (config?.Scan?.Roots != null)
            {
                foreach (string r in config.Scan.Roots)
                {
                    if (!string.IsNullOrWhiteSpace(r) && Directory.Exists(r)) roots.Add(r);
                }
            }

            string[] subDirs = { "Siemens", "Unigraphics Solutions" };
            string[] sysEnvs = { "LOCALAPPDATA", "APPDATA", "ProgramFiles", "ProgramFiles(x86)" };
            foreach (string sysEnv in sysEnvs)
            {
                string baseDir = Environment.GetEnvironmentVariable(sysEnv);
                if (string.IsNullOrWhiteSpace(baseDir)) continue;

                foreach (string sub in subDirs)
                {
                    string candidate = Path.Combine(baseDir, sub);
                    if (Directory.Exists(candidate)) roots.Add(candidate);
                }
            }

            return DedupePaths(roots);
        }

        private static void ScanDirectoryRecursive(string currentDir, int depth, int maxDepth, ref int fileCount, int maxFiles, Action<string> fileAction)
        {
            if (depth > maxDepth || fileCount >= maxFiles) return;

            try
            {
                foreach (string file in Directory.EnumerateFiles(currentDir))
                {
                    fileCount++;
                    fileAction(file);
                    if (fileCount >= maxFiles) return;
                }

                foreach (string subDir in Directory.EnumerateDirectories(currentDir))
                {
                    ScanDirectoryRecursive(subDir, depth + 1, maxDepth, ref fileCount, maxFiles, fileAction);
                    if (fileCount >= maxFiles) return;
                }
            }
            catch
            {
                // Ignore permission or file access errors during scan
            }
        }

        private static List<string> DedupePaths(IEnumerable<string> paths)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string p in paths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                string clean = Path.GetFullPath(p);
                if (seen.Add(clean))
                {
                    result.Add(clean);
                }
            }
            return result;
        }
    }
}
