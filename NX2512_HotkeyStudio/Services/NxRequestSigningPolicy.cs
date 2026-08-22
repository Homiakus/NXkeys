using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NX2512_HotkeyStudio.Models;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Политика подписи и допуска запроса (модуль D). Владеет секретом/сессией, профилем,
    /// allowlist, client-проверкой и HMAC-подписью. Не выполняет файловый IO очереди.
    /// </summary>
    public sealed class NxRequestSigningPolicy : IRequestPolicy
    {
        private readonly object securitySync = new object();
        private NxBridgePermissionSet securityPermissions;
        private string securityProfilePath = string.Empty;
        private DateTime securityProfileWriteUtc = DateTime.MinValue;
        private long securitySequence;
        private readonly string clientInstanceId = Guid.NewGuid().ToString("N");

        public string ActiveProfilePath => securityProfilePath;

        public void ConfigureSecurity(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("NXKeys security profile path is required.", nameof(configPath));
            string fullPath = Path.GetFullPath(configPath);
            NxBridgePermissionSet permissions = NxBridgePermissionSet.FromProfileFile(fullPath);
            lock (securitySync)
            {
                securityPermissions = permissions;
                securityProfilePath = fullPath;
                securityProfileWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            }
        }

        public void PrepareAuthenticated(NxCommandRequest request)
        {
            if (!NxBridgeSecurityEnvironment.TryRead(
                    out string sessionId,
                    out byte[] secret,
                    out string environmentProfilePath,
                    out string clientExecutable,
                    out string error))
                throw new InvalidOperationException(error + " Запустите NX через launch-nx2512-with-nxkeys.cmd.");

            string actualClient = Path.GetFullPath(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            if (!string.Equals(actualClient, clientExecutable, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NXKeys request source is not the trusted managed HotkeyStudio executable.");

            NxBridgePermissionSet permissions;
            lock (securitySync)
            {
                string fullEnvironmentProfile = Path.GetFullPath(environmentProfilePath);
                if (securityPermissions == null ||
                    !string.Equals(securityProfilePath, fullEnvironmentProfile, StringComparison.OrdinalIgnoreCase) ||
                    File.GetLastWriteTimeUtc(fullEnvironmentProfile) != securityProfileWriteUtc)
                {
                    securityPermissions = NxBridgePermissionSet.FromProfileFile(fullEnvironmentProfile);
                    securityProfilePath = fullEnvironmentProfile;
                    securityProfileWriteUtc = File.GetLastWriteTimeUtc(fullEnvironmentProfile);
                }
                permissions = securityPermissions;
            }

            if (!permissions.TryGetPermission(request, out NxCommandPermission permission))
                throw new InvalidOperationException(
                    "NXKeys request is not present in the active profile allowlist: " +
                    NxBridgePermissionSet.PermissionKey(request.Action, request.CommandId, request.ModuleId,
                        request.TargetApplicationId, request.SelectionFilter));
            if (request.Destructive != permission.Destructive)
                throw new InvalidOperationException("NXKeys request destructive policy differs from the active profile.");
            if (permission.ConfirmationRequired && !request.ConfirmationAccepted)
                throw new InvalidOperationException("NXKeys request has not passed the confirmation policy.");

            NxRequestAuthenticator.Sign(
                request,
                sessionId,
                clientInstanceId,
                secret,
                permissions.ProfileDigest,
                Interlocked.Increment(ref securitySequence));
            request.Validate();
        }
    }
}
