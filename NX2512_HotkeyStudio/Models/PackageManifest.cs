using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class PackageManifest
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("package_version")]
        public string PackageVersion { get; set; } = "0.2.0";

        [JsonPropertyName("target_nx")]
        public string TargetNX { get; set; } = string.Empty;

        [JsonPropertyName("created_utc")]
        public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("O");

        [JsonPropertyName("managed_root")]
        public string ManagedRoot { get; set; } = string.Empty;

        [JsonPropertyName("files")]
        public List<PackageFileEntry> Files { get; set; } = new List<PackageFileEntry>();
    }

    public sealed class PackageFileEntry
    {
        [JsonPropertyName("relative_path")]
        public string RelativePath { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; } = true;
    }
}
