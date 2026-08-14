using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NX2512_HotkeyStudio.Models;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class NxCommandRequest : NXKeys.Protocol.NxCommandRequest { }
    public sealed class NxBridgeContext : NxContextSnapshot { }
    public sealed class NxBridgeResult : NxCommandResult { }

    public static class NxCommandBridgeClient
    {
        private static readonly string ClientInstanceId = Guid.NewGuid().ToString("N");
        private static readonly object SecuritySync = new object();
        private static NxBridgePermissionSet securityPermissions;
        private static string securityProfilePath = string.Empty;
        private static DateTime securityProfileWriteUtc = DateTime.MinValue;
        private static long securitySequence;

        public static void ConfigureSecurity(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("NXKeys security profile path is required.", nameof(configPath));
            string fullPath = Path.GetFullPath(configPath);
            NxBridgePermissionSet permissions = NxBridgePermissionSet.FromProfileFile(fullPath);
            lock (SecuritySync)
            {
                securityPermissions = permissions;
                securityProfilePath = fullPath;
                securityProfileWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            }
        }
        public static string BridgeRoot
        {
            get
            {
                string overrideRoot = Environment.GetEnvironmentVariable("NXKEYS_BRIDGE_ROOT");
                return string.IsNullOrWhiteSpace(overrideRoot)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "bridge")
                    : Path.GetFullPath(overrideRoot);
            }
        }

        public static string PendingDirectory => Path.Combine(BridgeRoot, "pending");
        public static string ProcessingDirectory => Path.Combine(BridgeRoot, "processing");
        public static string CompletedDirectory => Path.Combine(BridgeRoot, "completed");
        public static string FailedDirectory => Path.Combine(BridgeRoot, "failed");
        public static string ContextPath => Path.Combine(BridgeRoot, "context.json");

        public static NxCommandRequest Enqueue(LeaderSequenceItem item, bool confirmationAccepted = false)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Command == null || string.IsNullOrWhiteSpace(item.Command.ID))
                throw new InvalidOperationException("Leader sequence has no exact NX command id.");

            NxBridgeContext context = RequireFreshContext();
            string action = string.IsNullOrWhiteSpace(item.Action)
                ? "execute_command"
                : item.Action.Trim();

            if (string.Equals(action, "run_capability", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Command?.ID, "nxeskd.open_workflow", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(item.Command?.ID, "nxeskd.open_workflow", StringComparison.OrdinalIgnoreCase))
                {
                    return LaunchNxEskdConfigurator(item, context);
                }
            }

            NxCommandRequest request = CreateRequest(
                action,
                item.Command.ID.Trim(),
                item.Command.Name,
                item.Sequence,
                item.ModuleID,
                string.Empty,
                context);
            request.SelectionFilter = item.SelectionType ?? string.Empty;
            request.Destructive = item.Destructive;
            request.ConfirmationAccepted = confirmationAccepted || (!item.Destructive && !item.ConfirmBeforeExecute);
            request.Validate();
            WriteRequest(request);
            return request;
        }

        public static NxCommandRequest LaunchNxEskdConfigurator(LeaderSequenceItem item, NxBridgeContext context)
        {
            string workflowId = Guid.NewGuid().ToString("N");
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profilePath = Path.Combine(root, "NXKeys", "profiles", "nxeskd", "active-profile.json");
            if (!File.Exists(profilePath))
            {
                string example = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "active-profile.example.json");
                if (File.Exists(example))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                    File.Copy(example, profilePath, false);
                }
            }

            string exePath = LocateConfiguratorExecutable();
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)!
                };

                psi.EnvironmentVariables[NxBridgeSecurityEnvironment.SessionIdVariable] = (context?.SecuritySessionId ?? string.Empty);
                psi.EnvironmentVariables[NxBridgeSecurityEnvironment.ConfigPathVariable] = securityProfilePath;
                psi.EnvironmentVariables["NXKEYS_BRIDGE_ROOT"] = BridgeRoot;

                if (File.Exists(profilePath))
                    psi.ArgumentList.Add("--profile=" + profilePath);
                psi.ArgumentList.Add("--workflow=" + workflowId);

                try { Process.Start(psi); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Не удалось запустить ЕСКД Конфигуратор: " + ex.Message, ex);
                }
            }

            var request = CreateRequest(
                "run_capability",
                "nxeskd.open_workflow",
                "ЕСКД — мастер подготовки чертежа",
                item.Sequence,
                item.ModuleID,
                string.Empty,
                context);
            request.CapabilityId = "nxeskd.open_workflow";
            request.WorkflowId = workflowId;
            request.ConfirmationAccepted = true;
            return request;
        }

        private static string LocateConfiguratorExecutable()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "NxEskd.Configurator.exe"),
                Path.Combine(baseDir, "bin", "NxEskd.Configurator.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "components", "nxeskd", "current", "configurator", "NxEskd.Configurator.exe"),
                Path.Combine(baseDir, "..", "nxeskd", "src", "NxEskd.Configurator", "bin", "Release", "net8.0-windows", "NxEskd.Configurator.exe"),
                Path.Combine(baseDir, "..", "..", "..", "nxeskd", "src", "NxEskd.Configurator", "bin", "Release", "net8.0-windows", "NxEskd.Configurator.exe")
            };
            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        public static NxCommandRequest EnqueueModuleSwitch(ModuleConfig module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            string applicationId = module.NXApplicationIDs != null && module.NXApplicationIDs.Count > 0
                ? module.NXApplicationIDs[0]
                : module.SwitchCommand?.ID;
            if (string.IsNullOrWhiteSpace(applicationId))
                throw new InvalidOperationException("Module has no NX application id.");

            NxBridgeContext context = RequireFreshContext();
            NxCommandRequest request = CreateRequest(
                "switch_module",
                module.SwitchCommand?.ID,
                module.SwitchCommand?.Name ?? module.Label,
                string.Empty,
                module.ID,
                applicationId.Trim(),
                context);
            request.ConfirmationAccepted = true;
            request.Validate();
            WriteRequest(request);
            return request;
        }

        public static NxTransportReadResult<NxBridgeContext> ReadContextDetailed()
        {
            if (!File.Exists(ContextPath))
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.NotFound, "Bridge context file was not found.");
            try
            {
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    NxBridgeContext context = JsonSerializer.Deserialize<NxBridgeContext>(stream, NxProtocolJson.ReadOptions);
                    if (context == null)
                        return NxTransportReadResult<NxBridgeContext>.Failure(
                            NxTransportReadStatus.Corrupt, "Bridge context JSON is empty.");
                    if (context.SchemaVersion != NxProtocolConstants.SchemaVersion)
                        return NxTransportReadResult<NxBridgeContext>.Failure(
                            NxTransportReadStatus.SchemaMismatch,
                            "Unsupported Bridge context schema: " + context.SchemaVersion + ".");
                    return NxTransportReadResult<NxBridgeContext>.Success(context);
                }
            }
            catch (JsonException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.Corrupt, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.AccessDenied, exception.Message);
            }
            catch (IOException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.IoError, exception.Message);
            }
        }

        public static NxBridgeContext ReadContext()
        {
            NxTransportReadResult<NxBridgeContext> read = ReadContextDetailed();
            return read.IsSuccess ? read.Value : null;
        }

        public static NxTransportReadResult<NxBridgeResult> ReadResultDetailed(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return NxTransportReadResult<NxBridgeResult>.Failure(
                    NxTransportReadStatus.InvalidRequest, "requestId is required.");
            foreach (string directory in new[] { CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".result.json");
                if (!File.Exists(path)) continue;
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        NxBridgeResult result = JsonSerializer.Deserialize<NxBridgeResult>(stream, NxProtocolJson.ReadOptions);
                        if (result == null)
                            return NxTransportReadResult<NxBridgeResult>.Failure(
                                NxTransportReadStatus.Corrupt, "Bridge result JSON is empty.");
                        if (result.SchemaVersion != NxProtocolConstants.SchemaVersion)
                            return NxTransportReadResult<NxBridgeResult>.Failure(
                                NxTransportReadStatus.SchemaMismatch,
                                "Unsupported Bridge result schema: " + result.SchemaVersion + ".");
                        return NxTransportReadResult<NxBridgeResult>.Success(result);
                    }
                }
                catch (JsonException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.Corrupt, exception.Message);
                }
                catch (UnauthorizedAccessException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.AccessDenied, exception.Message);
                }
                catch (IOException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.IoError, exception.Message);
                }
            }
            return NxTransportReadResult<NxBridgeResult>.Failure(
                NxTransportReadStatus.NotFound, "Bridge result file was not found.");
        }

        public static bool TryReadResult(string requestId, out NxBridgeResult result)
        {
            NxTransportReadResult<NxBridgeResult> read = ReadResultDetailed(requestId);
            result = read.IsSuccess ? read.Value : null;
            return read.IsSuccess;
        }

        public static string FindRequestFile(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return string.Empty;
            foreach (string directory in new[] { PendingDirectory, ProcessingDirectory, CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".request.json");
                if (File.Exists(path)) return path;
            }
            return string.Empty;
        }

        private static NxBridgeContext RequireFreshContext()
        {
            NxTransportReadResult<NxBridgeContext> read = ReadContextDetailed();
            if (!read.IsSuccess)
                throw new InvalidOperationException(
                    "NXKeys Bridge context недоступен [" + read.Status + "]: " + read.Message);
            NxBridgeContext context = read.Value;
            if (!context.IsFresh)
                throw new InvalidOperationException("NXKeys Bridge context устарел: в NX нажмите Start NXKeys Bridge. Возраст context: " + ContextAgeText(context) + ".");
            if (!string.Equals(context.Status, "running", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NXKeys Bridge не готов: " + context.Status + ". В NX нажмите Start NXKeys Bridge.");
            if (!string.Equals(context.SecurityStatus, "authenticated", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "NXKeys Bridge не имеет authenticated session. Запустите NX через managed launcher NXKeys.");
            return context;
        }

        private static string ContextAgeText(NxBridgeContext context)
        {
            if (context == null || !DateTimeOffset.TryParse(context.UpdatedUtc, out DateTimeOffset updated)) return "неизвестен";
            return Math.Max(0, (DateTimeOffset.UtcNow - updated.ToUniversalTime()).TotalSeconds).ToString("0.0") + "s";
        }

        private static NxCommandRequest CreateRequest(
            string action,
            string commandId,
            string commandName,
            string sequence,
            string moduleId,
            string targetApplicationId,
            NxBridgeContext context)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new NxCommandRequest
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                RequestId = $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
                Action = action ?? string.Empty,
                CommandId = commandId ?? string.Empty,
                CommandName = commandName ?? string.Empty,
                Sequence = sequence ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                TargetApplicationId = targetApplicationId ?? string.Empty,
                CreatedUtc = now.ToString("O"),
                ExpiresUtc = now.Add(NxProtocolConstants.DefaultRequestLifetime).ToString("O"),
                SourceProcessId = Process.GetCurrentProcess().Id,
                ExpectedContextRevision = context?.Revision ?? 0,
                ExpectedSelectionCount = context?.SelectionCount ?? -1,
                ExpectedSelectionFingerprint = context?.SelectionFingerprint ?? string.Empty,
                ExpectedApplicationId = context?.ApplicationId ?? string.Empty
            };
        }

        private static void PrepareAuthenticatedRequest(NxCommandRequest request)
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
            lock (SecuritySync)
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
                ClientInstanceId,
                secret,
                permissions.ProfileDigest,
                Interlocked.Increment(ref securitySequence));
            request.Validate();
        }

        private static void WriteRequest(NxCommandRequest request)
        {
            PrepareAuthenticatedRequest(request);
            Directory.CreateDirectory(PendingDirectory);
            Directory.CreateDirectory(ProcessingDirectory);
            Directory.CreateDirectory(CompletedDirectory);
            Directory.CreateDirectory(FailedDirectory);

            int pendingCount = Directory.GetFiles(PendingDirectory, "*.request.json").Length;
            if (pendingCount >= NxProtocolConstants.MaxPendingRequestCount)
                throw new InvalidOperationException(
                    "NXKeys Bridge queue limit reached: " + pendingCount + ".");

            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".request.json");
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, NxProtocolJson.WriteOptions);
            if (payload.Length > NxProtocolConstants.MaxRequestPayloadBytes)
                throw new InvalidOperationException(
                    "NXKeys request payload exceeds " + NxProtocolConstants.MaxRequestPayloadBytes + " bytes.");
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, finalPath);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            }
        }
    }
}
