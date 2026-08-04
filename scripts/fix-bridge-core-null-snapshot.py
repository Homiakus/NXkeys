from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / "NXKeys.BridgeCore" / "BridgeSecurityGate.cs"
text = path.read_text(encoding="utf-8")
old = """            RefreshProfileIfNeeded(request.ProfileDigest);
            if (!NxRequestAuthenticator.Verify(
                    request,
                    SessionId,
                    secret,
                    permissions.ProfileDigest,
                    out string authenticationError))
                throw new InvalidOperationException(authenticationError);

            ValidateSourceProcess(request.SourceProcessId);
            if (!permissions.TryGetPermission(request, out NxCommandPermission? permission) || permission == null)
"""
new = """            RefreshProfileIfNeeded(request.ProfileDigest);
            NxBridgePermissionSet activePermissions = permissions ??
                throw new InvalidOperationException("NXKeys Bridge permission set is unavailable.");
            if (!NxRequestAuthenticator.Verify(
                    request,
                    SessionId,
                    secret,
                    activePermissions.ProfileDigest,
                    out string authenticationError))
                throw new InvalidOperationException(authenticationError);

            ValidateSourceProcess(request.SourceProcessId);
            if (!activePermissions.TryGetPermission(request, out NxCommandPermission? permission) || permission == null)
"""
if text.count(old) != 1:
    raise RuntimeError("BridgeSecurityGate permission snapshot block was not found exactly once.")
path.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")
print("BridgeCore permission snapshot nullability fixed.")
