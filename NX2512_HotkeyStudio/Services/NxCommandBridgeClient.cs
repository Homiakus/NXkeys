using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NX2512_HotkeyStudio.Models;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class NxCommandRequest : NXKeys.Protocol.NxCommandRequest { }
    public sealed class NxBridgeContext : NxContextSnapshot { }
    public sealed class NxBridgeResult : NxCommandResult { }

    /// <summary>
    /// Фасад к файловой IPC-очереди. Тонкая оркестровка: свежий контекст → построение запроса →
    /// подпись (IRequestPolicy) → транспортировка (INxQueueTransport). Внутреннее состояние подписи
    /// изолировано в <see cref="NxRequestSigningPolicy"/>, файловый IO — в <see cref="NxFileQueueTransport"/>.
    /// </summary>
    public static class NxCommandBridgeClient
    {
        private static readonly IRequestPolicy policy = new NxRequestSigningPolicy();
        private static readonly INxQueueTransport transport = new NxFileQueueTransport();

        public static void ConfigureSecurity(string configPath)
        {
            policy.ConfigureSecurity(configPath);
        }

        public static string BridgeRoot => transport.BridgeRoot;
        public static string PendingDirectory => transport.PendingDirectory;
        public static string ProcessingDirectory => transport.ProcessingDirectory;
        public static string CompletedDirectory => transport.CompletedDirectory;
        public static string FailedDirectory => transport.FailedDirectory;
        public static string ContextPath => transport.ContextPath;

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
                psi.EnvironmentVariables[NxBridgeSecurityEnvironment.ConfigPathVariable] = policy.ActiveProfilePath;
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

        public static NxTransportReadResult<NxBridgeContext> ReadContextDetailed() => transport.ReadContextDetailed();
        public static NxBridgeContext ReadContext() => transport.ReadContext();
        public static NxTransportReadResult<NxBridgeResult> ReadResultDetailed(string requestId) => transport.ReadResultDetailed(requestId);
        public static bool TryReadResult(string requestId, out NxBridgeResult result) => transport.TryReadResult(requestId, out result);
        public static string FindRequestFile(string requestId) => transport.FindRequestFile(requestId);

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

        private static void WriteRequest(NxCommandRequest request)
        {
            policy.PrepareAuthenticated(request);
            transport.WriteRequest(request);
        }
    }
}
