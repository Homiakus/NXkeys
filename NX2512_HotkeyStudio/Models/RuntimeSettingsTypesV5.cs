using System;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class PerformanceConfig
    {
        [JsonPropertyName("catalog_cache_enabled")] public bool CatalogCacheEnabled { get; set; } = true;
        [JsonPropertyName("lazy_studio_scan")] public bool LazyStudioScan { get; set; } = true;
        [JsonPropertyName("bridge_watcher")] public bool BridgeWatcher { get; set; } = true;
        public void ApplyDefaults() { CatalogCacheEnabled = true; LazyStudioScan = true; BridgeWatcher = true; }
    }

    public sealed class RoleDeployment
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; }
        [JsonPropertyName("source_mtx")] public string SourceMTX { get; set; } = string.Empty;
        [JsonPropertyName("target_directory")] public string TargetDirectory { get; set; } = string.Empty;
        [JsonPropertyName("target_name")] public string TargetName { get; set; } = string.Empty;
        [JsonPropertyName("set_as_default")] public bool SetAsDefault { get; set; }
        [JsonPropertyName("default_role_env")] public string DefaultRoleEnv { get; set; } = "UGII_DEFAULT_ROLE";
        public void ApplyDefaults(string nxVersion)
        {
            if (string.IsNullOrWhiteSpace(TargetName)) TargetName = "NX_Adaptive_Modules_" + nxVersion + ".mtx";
            if (string.IsNullOrWhiteSpace(DefaultRoleEnv)) DefaultRoleEnv = "UGII_DEFAULT_ROLE";
        }
    }
}
