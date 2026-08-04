using System;
using System.Diagnostics;
using System.IO;
using NXKeys.Protocol;

namespace NXKeys.BridgeCore
{
    public sealed class BridgeSecurityGate
    {
        private readonly Action<string> log;
        private readonly object sync = new object();
        private readonly byte[] secret;
        private readonly string profilePath;
        private readonly string expectedClientExecutable;
        private readonly NxReplayGuard replayGuard = new NxReplayGuard();
        private NxBridgePermissionSet? permissions;

        public string Status { get; private set; }
        public string SessionId { get; }
        public string ProfileDigest => permissions?.ProfileDigest ?? string.Empty;
        public bool IsAuthenticated => string.Equals(Status, "authenticated", StringComparison.OrdinalIgnoreCase);

        private BridgeSecurityGate(
            string status,
            string sessionId,
            byte[] sessionSecret,
            string securityProfilePath,
            string clientExecutable,
            NxBridgePermissionSet? permissionSet,
            Action<string>? logger)
        {
            Status = status ?? "authentication_required";
            SessionId = sessionId ?? string.Empty;
            secret = sessionSecret ?? Array.Empty<byte>();
            profilePath = securityProfilePath ?? string.Empty;
            expectedClientExecutable = clientExecutable ?? string.Empty;
            permissions = permissionSet;
            log = logger ?? (_ => { });
        }

        public static BridgeSecurityGate CreateFromEnvironment(Action<string>? log = null)
        {
            Action<string> logger = log ?? (_ => { });
            if (!NxBridgeSecurityEnvironment.TryRead(
                    out string sessionId,
                    out byte[] secret,
                    out string profilePath,
                    out string clientExecutable,
                    out string error))
            {
                logger("Secure IPC is unavailable: " + error);
                return new BridgeSecurityGate(
                    "authentication_required", sessionId, secret, profilePath, clientExecutable, null, logger);
            }

            try
            {
                NxBridgePermissionSet permissions = NxBridgePermissionSet.FromProfileFile(profilePath);
                logger("Secure IPC initialized. Profile digest=" + permissions.ProfileDigest);
                return new BridgeSecurityGate(
                    "authenticated", sessionId, secret, profilePath, clientExecutable, permissions, logger);
            }
            catch (Exception exception)
            {
                logger("Secure IPC profile load failed: " + exception.Message);
                return new BridgeSecurityGate(
                    "profile_invalid", sessionId, secret, profilePath, clientExecutable, null, logger);
            }
        }

        public void Validate(NxCommandRequest? request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!IsAuthenticated || permissions == null)
                throw new InvalidOperationException(
                    "NXKeys authenticated session is not ready. Start NX through the managed NXKeys launcher.");

            RefreshProfileIfNeeded(request.ProfileDigest);
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
                throw new InvalidOperationException("NX command/action is not present in the active profile allowlist.");
            if (request.Destructive != permission.Destructive)
                throw new InvalidOperationException("Request destructive policy differs from the active profile.");
            if (permission.ConfirmationRequired && !request.ConfirmationAccepted)
                throw new InvalidOperationException("Request requires confirmation according to the active profile.");
            if (!replayGuard.TryAccept(request, out string replayError))
                throw new InvalidOperationException(replayError);
        }

        private void RefreshProfileIfNeeded(string? requestedDigest)
        {
            if (permissions != null &&
                string.Equals(permissions.ProfileDigest, requestedDigest, StringComparison.OrdinalIgnoreCase)) return;
            lock (sync)
            {
                NxBridgePermissionSet refreshed = NxBridgePermissionSet.FromProfileFile(profilePath);
                if (!string.Equals(refreshed.ProfileDigest, requestedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Request profile digest does not match the installed NXKeys profile.");
                permissions = refreshed;
                Status = "authenticated";
                log("Secure IPC permission set reloaded. Digest=" + refreshed.ProfileDigest);
            }
        }

        private void ValidateSourceProcess(int processId)
        {
            if (processId <= 0) throw new InvalidOperationException("Request source_process_id is invalid.");
            try
            {
                using (Process source = Process.GetProcessById(processId))
                {
                    string actual = Path.GetFullPath(source.MainModule?.FileName ?? string.Empty);
                    if (!string.Equals(actual, expectedClientExecutable, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Request source process is not the trusted managed HotkeyStudio executable.");
                }
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("Request source process is no longer running.");
            }
        }
    }
}
