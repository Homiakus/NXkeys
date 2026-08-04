from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(path: str, old: str, new: str) -> None:
    target = ROOT / path
    text = target.read_text(encoding="utf-8")
    if text.count(old) != 1:
        raise RuntimeError(f"{path}: expected one match for {old!r}")
    target.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")

replace_once(
    "NX2512_CommandBridge/Program.cs",
    "requestInbox.TryDequeueRejected(out BridgeRequestRejection? rejection) && rejection != null",
    "requestInbox.TryDequeueRejected(out BridgeRequestRejection rejection) && rejection != null")
replace_once(
    "NX2512_CommandBridge/Program.cs",
    "requestInbox.TryDequeue(out BridgeRequestClaim? claim) && claim != null",
    "requestInbox.TryDequeue(out BridgeRequestClaim claim) && claim != null")
replace_once(
    "NX2512_HotkeyStudio.Tests/Program.cs",
    "NXKeys.BridgeCore.BridgeRequestClaim? claim = null;",
    "NXKeys.BridgeCore.BridgeRequestClaim claim = null;")
replace_once(
    "NX2512_HotkeyStudio.Tests/Program.cs",
    "NXKeys.BridgeCore.BridgeRequestRejection? rejection = null;",
    "NXKeys.BridgeCore.BridgeRequestRejection rejection = null;")

print("Legacy nullable boundary annotations cleaned.")
