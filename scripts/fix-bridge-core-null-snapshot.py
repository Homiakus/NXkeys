from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / "NXKeys.BridgeCore" / "BridgeSecurityGate.cs"
text = path.read_text(encoding="utf-8")

replacements = [
    (
        "            RefreshProfileIfNeeded(request?.ProfileDigest);\n",
        "            RefreshProfileIfNeeded(request.ProfileDigest);\n"
        "            NxBridgePermissionSet activePermissions = permissions ??\n"
        "                throw new InvalidOperationException(\"NXKeys Bridge permission set is unavailable.\");\n"
    ),
    (
        "                    permissions.ProfileDigest,\n",
        "                    activePermissions.ProfileDigest,\n"
    ),
    (
        "            if (!permissions.TryGetPermission(request, out NxCommandPermission? permission) || permission == null)\n",
        "            if (!activePermissions.TryGetPermission(request, out NxCommandPermission? permission) || permission == null)\n"
    ),
]
for old, new in replacements:
    if text.count(old) != 1:
        raise RuntimeError(f"BridgeSecurityGate structural fragment was not found exactly once: {old!r}")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8", newline="\n")
print("BridgeCore permission snapshot nullability fixed.")
