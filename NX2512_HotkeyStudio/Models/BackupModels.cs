using System;
using System.Collections.Generic;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class BackupManifest
    {
        public string Timestamp { get; set; } = string.Empty;
        public string BackupDirectory { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public List<BackupEntry> Entries { get; set; } = new List<BackupEntry>();
    }

    public sealed class BackupEntry
    {
        public string OriginalPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public bool IsNewFile { get; set; }
        public string Sha256Before { get; set; } = string.Empty;
        public string Sha256After { get; set; } = string.Empty;
    }

    public sealed class RestoreResult
    {
        public bool Success { get; set; }
        public bool ForceUsed { get; set; }
        public string ManifestPath { get; set; } = string.Empty;
        public List<string> RestoredFiles { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
