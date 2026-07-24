using System.Collections.Generic;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class NxKeysHealthReport
    {
        public string ManagedRoot { get; set; } = string.Empty;
        public string ExpectedCustomDirsFile { get; set; } = string.Empty;
        public Dictionary<string, string> EnvironmentCustomDirsFiles { get; set; } = new Dictionary<string, string>();
        public List<string> EnvironmentWarnings { get; set; } = new List<string>();
        public bool MenuScriptVersionOk { get; set; }
        public List<NxKeysMenuFileStatus> MenuFiles { get; set; } = new List<NxKeysMenuFileStatus>();
        public List<NxKeysMenuFileStatus> StaleFiles { get; set; } = new List<NxKeysMenuFileStatus>();
        public bool BridgeLoaded { get; set; }
        public string BridgeStatusPath { get; set; } = string.Empty;
        public string BridgeContextPath { get; set; } = string.Empty;
        public double BridgeContextAgeSeconds { get; set; } = -1;
        public string BridgeLogPath { get; set; } = string.Empty;
        public List<string> LastBridgeLogLines { get; set; } = new List<string>();
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> LastFailures { get; set; } = new List<string>();
        public bool NxRunning { get; set; }
        public List<string> NxProcesses { get; set; } = new List<string>();
        public bool ManagedPackageOk { get; set; }
        public bool BridgeDllLocked { get; set; }
        public List<string> MissingManagedFiles { get; set; } = new List<string>();
        public List<string> HashMismatches { get; set; } = new List<string>();
    }

    public sealed class NxKeysMenuFileStatus
    {
        public string Path { get; set; } = string.Empty;
        public int Version { get; set; }
        public bool IsExpectedVersion { get; set; }
        public bool IsStale { get; set; }
    }
}
