using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public static class BackupEngine
    {
        public static BackupManifest CreateBackup(string backupRoot, string profileName, IEnumerable<string> targetFiles)
        {
            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "backups");
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss.fff");
            string backupDir = Path.Combine(backupRoot, stamp);
            string filesDir = Path.Combine(backupDir, "files");
            Directory.CreateDirectory(filesDir);

            BackupManifest manifest = new BackupManifest
            {
                Timestamp = stamp,
                BackupDirectory = backupDir,
                ProfileName = profileName
            };

            int index = 0;
            foreach (string file in targetFiles)
            {
                if (string.IsNullOrWhiteSpace(file)) continue;

                string fullPath = Path.GetFullPath(file);
                BackupEntry entry = new BackupEntry
                {
                    OriginalPath = fullPath
                };

                if (File.Exists(fullPath))
                {
                    entry.IsNewFile = false;
                    entry.Sha256Before = ComputeSha256(fullPath);

                    string backupFileName = $"{index}_{Path.GetFileName(fullPath)}";
                    string backupFilePath = Path.Combine(filesDir, backupFileName);

                    File.Copy(fullPath, backupFilePath, true);
                    entry.BackupPath = backupFilePath;
                }
                else
                {
                    entry.IsNewFile = true;
                    entry.Sha256Before = string.Empty;
                    entry.BackupPath = string.Empty;
                }

                manifest.Entries.Add(entry);
                index++;
            }

            string manifestPath = Path.Combine(backupDir, "manifest.json");
            SaveManifest(manifestPath, manifest);

            return manifest;
        }

        public static void FinalizeBackupManifest(BackupManifest manifest)
        {
            if (manifest == null) return;

            foreach (BackupEntry entry in manifest.Entries)
            {
                if (File.Exists(entry.OriginalPath))
                {
                    entry.Sha256After = ComputeSha256(entry.OriginalPath);
                }
            }

            string manifestPath = Path.Combine(manifest.BackupDirectory, "manifest.json");
            SaveManifest(manifestPath, manifest);
        }

        public static RestoreResult RestoreLatest(string backupRoot, bool force)
        {
            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "backups");
            }

            if (!Directory.Exists(backupRoot))
            {
                return new RestoreResult { Success = false, ErrorMessage = "No backup directory found." };
            }

            var subDirs = Directory.GetDirectories(backupRoot);
            if (subDirs.Length == 0)
            {
                return new RestoreResult { Success = false, ErrorMessage = "No backups found in backup root." };
            }

            Array.Sort(subDirs, (a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase)); // Latest first
            string latestManifest = Path.Combine(subDirs[0], "manifest.json");

            return RestoreFromManifest(latestManifest, force);
        }

        public static RestoreResult RestoreFromManifest(string manifestPath, bool force)
        {
            RestoreResult result = new RestoreResult
            {
                ManifestPath = manifestPath,
                ForceUsed = force
            };

            if (!File.Exists(manifestPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Manifest file not found: {manifestPath}";
                return result;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                BackupManifest manifest = JsonSerializer.Deserialize<BackupManifest>(json);

                if (manifest == null || manifest.Entries == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid manifest file format.";
                    return result;
                }

                if (!force)
                {
                    foreach (BackupEntry entry in manifest.Entries)
                    {
                        if (!File.Exists(entry.OriginalPath)) continue;

                        if (string.IsNullOrEmpty(entry.Sha256After))
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Backup manifest has no post-apply hash for {entry.OriginalPath}. Use --force only if you have reviewed the file.";
                            result.Warnings.Add(result.ErrorMessage);
                            return result;
                        }

                        if (!string.IsNullOrEmpty(entry.Sha256After))
                        {
                            string currentHash = ComputeSha256(entry.OriginalPath);
                            if (currentHash != entry.Sha256After)
                            {
                                result.Success = false;
                                result.ErrorMessage = $"File {entry.OriginalPath} was modified after apply (hash mismatch). Use --force to overwrite.";
                                result.Warnings.Add(result.ErrorMessage);
                                return result;
                            }
                        }
                    }
                }

                for (int i = manifest.Entries.Count - 1; i >= 0; i--)
                {
                    BackupEntry entry = manifest.Entries[i];
                    if (entry.IsNewFile)
                    {
                        if (File.Exists(entry.OriginalPath))
                        {
                            File.Delete(entry.OriginalPath);
                            result.RestoredFiles.Add($"Deleted newly created file: {entry.OriginalPath}");
                        }
                    }
                    else
                    {
                        if (File.Exists(entry.BackupPath))
                        {
                            string parentDir = Path.GetDirectoryName(entry.OriginalPath);
                            if (!string.IsNullOrWhiteSpace(parentDir))
                            {
                                Directory.CreateDirectory(parentDir);
                            }
                            File.Copy(entry.BackupPath, entry.OriginalPath, true);
                            result.RestoredFiles.Add($"Restored: {entry.OriginalPath}");
                        }
                        else
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Backup file is missing: {entry.BackupPath}";
                            return result;
                        }
                    }
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.ToString();
                return result;
            }
        }

        public static List<BackupManifest> ListBackups(string backupRoot)
        {
            var manifests = new List<BackupManifest>();
            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "backups");
            }

            if (!Directory.Exists(backupRoot)) return manifests;

            foreach (string dir in Directory.GetDirectories(backupRoot))
            {
                string manifestPath = Path.Combine(dir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        string json = File.ReadAllText(manifestPath);
                        var m = JsonSerializer.Deserialize<BackupManifest>(json);
                        if (m != null) manifests.Add(m);
                    }
                    catch
                    {
                        // Skip corrupted manifest
                    }
                }
            }

            manifests.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.OrdinalIgnoreCase));
            return manifests;
        }

        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static void SaveManifest(string path, BackupManifest manifest)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(manifest, options);
            File.WriteAllText(path, json);
        }
    }
}
