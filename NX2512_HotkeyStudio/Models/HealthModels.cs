using System.Collections.Generic;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class NxKeysHealthReport
    {
        public string ManagedRoot { get; set; } = string.Empty;
        public bool MenuScriptVersionOk { get; set; }
        public List<NxKeysMenuFileStatus> MenuFiles { get; set; } = new List<NxKeysMenuFileStatus>();
        public List<NxKeysMenuFileStatus> StaleFiles { get; set; } = new List<NxKeysMenuFileStatus>();
        public bool BridgeLoaded { get; set; }
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
