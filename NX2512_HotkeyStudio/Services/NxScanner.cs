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
        public string DocumentationCatalogDirectory { get; set; } = string.Empty;
    }

    public static class NxScanner
    {
        private static readonly string[] ApiCatalogFiles =
        {
            "04_nxopen_members.csv",
            "05_nxopen_entry_points.csv",
            "06_ui_commands_buttons.csv",
            "07_ufun_functions.csv",
            "08_ui_command_api_candidates.csv"
        };

        public static ScanResult Scan(Config config, string documentationCatalogDir = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var result = new ScanResult();
            foreach (string root in DiscoverRoots(config)) result.DiscoveredRoots.Add(root);

            int maxDepth = Math.Max(1, config.Scan?.MaxDepth ?? 8);
            int maxFiles = Math.Max(100, config.Scan?.MaxFiles ?? 25000);
            int visited = 0;
            var menuExtensions = new HashSet<string>(config.Scan?.MenuExtensions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var roleExtensions = new HashSet<string>(config.Scan?.RoleExtensions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var launcherExtensions = new HashSet<string>(config.Scan?.LauncherExtensions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (string root in result.DiscoveredRoots)
            {
                if (visited >= maxFiles) break;
                ScanDirectory(root, 0, maxDepth, maxFiles, config.Scan?.FollowSymlinks == true, ref visited, result, menuExtensions, roleExtensions, launcherExtensions);
            }
            if (visited >= maxFiles) result.Warnings.Add("Сканирование остановлено по лимиту max_files=" + maxFiles + ".");

            result.MenuFiles.Sort(StringComparer.OrdinalIgnoreCase);
            result.RoleFiles.Sort(StringComparer.OrdinalIgnoreCase);
            result.LauncherFiles.Sort(StringComparer.OrdinalIgnoreCase);
            result.DocumentationCatalogDirectory = ResolveCatalogDirectory(documentationCatalogDir);

            string signature = ComputeCatalogSignature(result.MenuFiles, result.DocumentationCatalogDirectory);
            if (config.Performance?.CatalogCacheEnabled == true && TryLoadCatalogCache(config, signature, result))
            {
                result.Warnings.Add("Использован проверенный кэш каталога.");
                return result;
            }

            if (!string.IsNullOrWhiteSpace(result.DocumentationCatalogDirectory))
            {
                CsvCatalogLoader.LoadFromDirectory(result.Catalog, result.DocumentationCatalogDirectory);
            }
            else
            {
                result.Warnings.Add("NX API-каталог не найден. Задайте --catalog, NXKEYS_CATALOG_DIR или поместите экспорт в %LOCALAPPDATA%\\NXKeys\\catalog.");
            }

            foreach (string menuFile in result.MenuFiles)
            {
                try { MenuScriptParser.ParseFile(result.Catalog, menuFile); }
                catch (Exception ex) { result.Warnings.Add("Не удалось разобрать MenuScript " + menuFile + ": " + ex.Message); }
            }

            if (config.Performance?.CatalogCacheEnabled == true) TryWriteCatalogCache(config, signature, result);
            return result;
        }

        public static List<string> DiscoverRoots(Config config)
        {
            var candidates = new List<string>();
            AddRange(candidates, config?.Scan?.Roots);
            AddRange(candidates, config?.Scan?.InstallHints);
            AddRange(candidates, config?.Scan?.ProfileHints);

            foreach (string variable in new[]
            {
                "UGII_BASE_DIR", "UGII_ROOT_DIR", "UGII_USER_PROFILE_DIR",
                "UGII_SITE_DIR", "UGOPEN", "UGII_CUSTOM_DIRECTORY_FILE"
            })
            {
                AddPath(candidates, Environment.GetEnvironmentVariable(variable));
            }

            foreach (string basePath in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            })
            {
                if (string.IsNullOrWhiteSpace(basePath)) continue;
                AddPath(candidates, Path.Combine(basePath, "Siemens"));
                AddPath(candidates, Path.Combine(basePath, "Unigraphics Solutions"));
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                string expanded = Config.ExpandPath(candidate);
                if (File.Exists(expanded)) expanded = Path.GetDirectoryName(expanded);
                if (string.IsNullOrWhiteSpace(expanded) || !Directory.Exists(expanded)) continue;
                string full = Path.GetFullPath(expanded);
                if (seen.Add(full)) result.Add(full);
            }
            return result;
        }

        private static void ScanDirectory(
            string directory,
            int depth,
            int maxDepth,
            int maxFiles,
            bool followSymlinks,
            ref int visited,
            ScanResult result,
            HashSet<string> menuExtensions,
            HashSet<string> roleExtensions,
            HashSet<string> launcherExtensions)
        {
            if (depth > maxDepth || visited >= maxFiles) return;
            try
            {
                var info = new DirectoryInfo(directory);
                if (!followSymlinks && (info.Attributes & FileAttributes.ReparsePoint) != 0) return;

                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    if (++visited > maxFiles) return;
                    string extension = Path.GetExtension(file);
                    if (menuExtensions.Contains(extension)) AddUnique(result.MenuFiles, file);
                    else if (roleExtensions.Contains(extension)) AddUnique(result.RoleFiles, file);
                    else if (launcherExtensions.Contains(extension) && LauncherLooksRelevant(file)) AddUnique(result.LauncherFiles, file);
                }

                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    if (visited >= maxFiles) return;
                    ScanDirectory(child, depth + 1, maxDepth, maxFiles, followSymlinks, ref visited, result, menuExtensions, roleExtensions, launcherExtensions);
                }
            }
            catch (UnauthorizedAccessException ex) { result.Warnings.Add("Нет доступа к " + directory + ": " + ex.Message); }
            catch (IOException ex) { result.Warnings.Add("Ошибка чтения " + directory + ": " + ex.Message); }
        }

        private static bool LauncherLooksRelevant(string path)
        {
            try
            {
                using (var reader = new StreamReader(path, Encoding.Default, true))
                {
                    for (int line = 0; line < 200 && !reader.EndOfStream; line++)
                    {
                        string value = (reader.ReadLine() ?? string.Empty).ToLowerInvariant();
                        if (value.Contains("ugii_") || value.Contains("ugraf.exe") || value.Contains("run_nx.exe")) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static string ResolveCatalogDirectory(string explicitPath)
        {
            foreach (string candidate in new[]
            {
                explicitPath,
                Environment.GetEnvironmentVariable("NXKEYS_CATALOG_DIR")
            })
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    string expanded = Config.ExpandPath(candidate);
                    if (Directory.Exists(expanded) && HasCatalogFiles(expanded)) return Path.GetFullPath(expanded);
                }
            }

            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "catalog");
            if (!Directory.Exists(root)) return string.Empty;
            try
            {
                return Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                    .Where(HasCatalogFiles)
                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static bool HasCatalogFiles(string directory)
        {
            return ApiCatalogFiles.Any(name => File.Exists(Path.Combine(directory, name)));
        }

        private static string ComputeCatalogSignature(IEnumerable<string> menuFiles, string catalogDirectory)
        {
            var records = new List<string>();
            foreach (string path in menuFiles ?? Enumerable.Empty<string>()) AddSignatureRecord(records, path);
            if (!string.IsNullOrWhiteSpace(catalogDirectory))
            {
                foreach (string file in ApiCatalogFiles) AddSignatureRecord(records, Path.Combine(catalogDirectory, file));
            }
            records.Add("scanner-schema=2");
            records.Sort(StringComparer.OrdinalIgnoreCase);
            using (var sha = SHA256.Create())
            {
                return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", records)))).ToLowerInvariant();
            }
        }

        private static void AddSignatureRecord(List<string> records, string path)
        {
            try
            {
                var info = new FileInfo(path);
                records.Add(Path.GetFullPath(path) + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks);
            }
            catch { records.Add(path + "|missing"); }
        }

        private static bool TryLoadCatalogCache(Config config, string signature, ScanResult result)
        {
            try
            {
                string path = CatalogCachePath(config, signature);
                if (!File.Exists(path)) return false;
                CatalogCachePayload payload = JsonSerializer.Deserialize<CatalogCachePayload>(File.ReadAllText(path));
                if (payload == null || payload.Signature != signature || payload.NXVersion != config.Profile.NXVersion) return false;
                foreach (CommandItem command in payload.Commands ?? new List<CommandItem>()) result.Catalog.AddOrMerge(command);
                foreach (ApiCrosswalkItem item in payload.CrosswalkEntries ?? new List<ApiCrosswalkItem>())
                {
                    result.Catalog.CrosswalkEntries.Add(item);
                    if (result.Catalog.Commands.TryGetValue(item.ButtonID ?? string.Empty, out CommandItem command)) command.ApiCandidates.Add(item);
                }
                return result.Catalog.Commands.Count > 0;
            }
            catch { return false; }
        }

        private static void TryWriteCatalogCache(Config config, string signature, ScanResult result)
        {
            try
            {
                string path = CatalogCachePath(config, signature);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var payload = new CatalogCachePayload
                {
                    NXVersion = config.Profile.NXVersion,
                    Signature = signature,
                    CreatedUtc = DateTime.UtcNow.ToString("O"),
                    Commands = result.Catalog.Commands.Values.ToList(),
                    CrosswalkEntries = result.Catalog.CrosswalkEntries.ToList()
                };
                AtomicFileWriter.WriteAllText(path, JsonSerializer.Serialize(payload), true);
            }
            catch { }
        }

        private static string CatalogCachePath(Config config, string signature)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "cache");
            string version = string.IsNullOrWhiteSpace(config.Profile?.NXVersion) ? "unknown" : Sanitize(config.Profile.NXVersion);
            return Path.Combine(root, "catalog-" + version + "-" + signature.Substring(0, 16) + ".json");
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }

        private static void AddRange(List<string> target, IEnumerable<string> values)
        {
            if (values == null) return;
            foreach (string value in values) AddPath(target, value);
        }

        private static void AddPath(List<string> target, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) target.Add(value);
        }

        private static void AddUnique(List<string> target, string path)
        {
            if (!target.Contains(path, StringComparer.OrdinalIgnoreCase)) target.Add(path);
        }

        private sealed class CatalogCachePayload
        {
            public string NXVersion { get; set; } = string.Empty;
            public string Signature { get; set; } = string.Empty;
            public string CreatedUtc { get; set; } = string.Empty;
            public List<CommandItem> Commands { get; set; } = new List<CommandItem>();
            public List<ApiCrosswalkItem> CrosswalkEntries { get; set; } = new List<ApiCrosswalkItem>();
        }
    }
}
