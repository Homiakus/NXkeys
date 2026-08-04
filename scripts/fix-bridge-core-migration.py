from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    (ROOT / path).write_text(content, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one match, found {count}: {old[:160]!r}")
    write(path, text.replace(old, new, 1))


# Do not mutate CapsLock from the background context poll.
replace_once(
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    """            if (!running) return;
            if (triggerVk == VK_CAPITAL && GetActiveNxWindow() != IntPtr.Zero) EnsureCapsLockOff();
            ModuleConfig previous = activeModule;
""",
    """            if (!running) return;
            // Background context polling never changes the user's CapsLock state.
            ModuleConfig previous = activeModule;
""",
)

# Keep the validator compatible with the typed protocol constant.
replace_once(
    "scripts/validate-command-tree.mjs",
    """  if (!bridgeSource.includes("set_selection_filter") || !bridgeSource.includes("SetEnabledGlobalFilterMembers"))
    fail("CommandBridge must implement direct NXOpen selection filter actions.");
""",
    """  const hasSelectionDispatch = bridgeSource.includes("set_selection_filter") ||
    bridgeSource.includes("NxProtocolActions.SetSelectionFilter");
  if (!hasSelectionDispatch || !bridgeSource.includes("SetEnabledGlobalFilterMembers"))
    fail("CommandBridge must implement direct NXOpen selection filter actions.");
""",
)

# Nullable-clean Protocol boundary.
security = "NXKeys.Protocol/NxBridgeSecurity.cs"
replace_once(
    security,
    """                                applications.TryGetValue(targetModule, out targetApplication);
""",
    """                                if (applications.TryGetValue(targetModule, out string? resolvedApplication))
                                    targetApplication = resolvedApplication ?? string.Empty;
""",
)
replace_once(
    security,
    """        public bool TryGetPermission(NxCommandRequest request, out NxCommandPermission permission)
        {
            permission = null;
""",
    """        public bool TryGetPermission(NxCommandRequest request, out NxCommandPermission? permission)
        {
            permission = null;
""",
)
replace_once(
    security,
    """            foreach (JsonElement item in array.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    return item.GetString().Trim();
""",
    """            foreach (JsonElement item in array.EnumerateArray())
            {
                string? value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
""",
)
replace_once(
    security,
    """        public static bool Verify(
            NxCommandRequest request,
""",
    """        public static bool Verify(
            NxCommandRequest? request,
""",
)
replace_once(
    security,
    """            error = string.Empty;
            try { ValidateEnvelope(request); }
""",
    """            error = string.Empty;
            if (request == null) { error = "Request is null."; return false; }
            try { ValidateEnvelope(request); }
""",
)

# Nullable-clean BridgeCore security gate.
gate = "NXKeys.BridgeCore/BridgeSecurityGate.cs"
replace_once(gate, "private NxBridgePermissionSet permissions;", "private NxBridgePermissionSet? permissions;")
replace_once(gate, "NxBridgePermissionSet permissionSet,", "NxBridgePermissionSet? permissionSet,")
replace_once(gate, "Action<string> logger)", "Action<string>? logger)")
replace_once(gate, "public static BridgeSecurityGate CreateFromEnvironment(Action<string> log = null)", "public static BridgeSecurityGate CreateFromEnvironment(Action<string>? log = null)")
replace_once(gate, "public void Validate(NxCommandRequest request)\n        {\n            if (!IsAuthenticated", "public void Validate(NxCommandRequest? request)\n        {\n            if (request == null) throw new ArgumentNullException(nameof(request));\n            if (!IsAuthenticated")
replace_once(gate, "if (!permissions.TryGetPermission(request, out NxCommandPermission permission))", "if (!permissions.TryGetPermission(request, out NxCommandPermission? permission) || permission == null)")
replace_once(gate, "private void RefreshProfileIfNeeded(string requestedDigest)", "private void RefreshProfileIfNeeded(string? requestedDigest)")

# Nullable-clean background inbox.
inbox = "NXKeys.BridgeCore/BridgeRequestInbox.cs"
replace_once(inbox, "private Thread worker;", "private Thread? worker;")
replace_once(inbox, "Action<string> logger = null)", "Action<string>? logger = null)")
replace_once(inbox, "public bool TryDequeue(out BridgeRequestClaim claim) => ready.TryDequeue(out claim);", "public bool TryDequeue(out BridgeRequestClaim? claim) => ready.TryDequeue(out claim);")
replace_once(inbox, "public bool TryDequeueRejected(out BridgeRequestRejection rejection) => rejected.TryDequeue(out rejection);", "public bool TryDequeueRejected(out BridgeRequestRejection? rejection) => rejected.TryDequeue(out rejection);")
replace_once(
    inbox,
    """                NxCommandRequest request;
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    request = JsonSerializer.Deserialize<NxCommandRequest>(stream, NxProtocolJson.ReadOptions);
""",
    """                NxCommandRequest? request;
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    request = JsonSerializer.Deserialize<NxCommandRequest>(stream, NxProtocolJson.ReadOptions);
""",
)

# Adapt nullable dequeue results at NX and test boundaries.
bridge = "NX2512_CommandBridge/Program.cs"
replace_once(bridge, "requestInbox.TryDequeueRejected(out BridgeRequestRejection rejection)", "requestInbox.TryDequeueRejected(out BridgeRequestRejection? rejection) && rejection != null")
replace_once(bridge, "requestInbox.TryDequeue(out BridgeRequestClaim claim)", "requestInbox.TryDequeue(out BridgeRequestClaim? claim) && claim != null")

hotkey_tests = "NX2512_HotkeyStudio.Tests/Program.cs"
replace_once(hotkey_tests, "NXKeys.BridgeCore.BridgeRequestClaim claim = null;", "NXKeys.BridgeCore.BridgeRequestClaim? claim = null;")
replace_once(hotkey_tests, "NXKeys.BridgeCore.BridgeRequestRejection rejection = null;", "NXKeys.BridgeCore.BridgeRequestRejection? rejection = null;")

print("BridgeCore migration fixes applied successfully.")
