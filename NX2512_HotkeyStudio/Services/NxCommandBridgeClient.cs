using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class NxCommandRequest : NXKeys.Protocol.NxCommandRequest { }
    public sealed class NxBridgeContext : NxContextSnapshot { }
    public sealed class NxBridgeResult : NxCommandResult { }

    public static class NxCommandBridgeClient
    {
        public static string BridgeRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NXKeys",
            "bridge");

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
            NxCommandRequest request = CreateRequest(
                "execute_command",
                item.Command.ID.Trim(),
                item.Command.Name,
                item.Sequence,
                item.ModuleID,
                string.Empty,
                context);
            request.Destructive = item.Destructive;
            request.ConfirmationAccepted = confirmationAccepted || (!item.Destructive && !item.ConfirmBeforeExecute);
            request.Validate();
            WriteRequest(request);
            return request;
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

        public static NxBridgeContext ReadContext()
        {
            try
            {
                if (!File.Exists(ContextPath)) return null;
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    return JsonSerializer.Deserialize<NxBridgeContext>(stream, NxProtocolJson.ReadOptions);
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool TryReadResult(string requestId, out NxBridgeResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(requestId)) return false;
            foreach (string directory in new[] { CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".result.json");
                if (!File.Exists(path)) continue;
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        result = JsonSerializer.Deserialize<NxBridgeResult>(stream, NxProtocolJson.ReadOptions);
                    }
                    return result != null;
                }
                catch
                {
                    return false;
                }
            }
            return false;
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
            NxBridgeContext context = ReadContext();
            if (context == null)
                throw new InvalidOperationException("NXKeys Bridge не загружен: в NX нажмите Start NXKeys Bridge, затем повторите команду.");
            if (!context.IsFresh)
                throw new InvalidOperationException("NXKeys Bridge context устарел: в NX нажмите Start NXKeys Bridge. Возраст context: " + ContextAgeText(context) + ".");
            if (!string.Equals(context.Status, "running", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NXKeys Bridge не готов: " + context.Status + ". В NX нажмите Start NXKeys Bridge.");
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
                ExpectedApplicationId = context?.ApplicationId ?? string.Empty
            };
        }

        private static void WriteRequest(NxCommandRequest request)
        {
            Directory.CreateDirectory(PendingDirectory);
            Directory.CreateDirectory(ProcessingDirectory);
            Directory.CreateDirectory(CompletedDirectory);
            Directory.CreateDirectory(FailedDirectory);

            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".request.json");
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, NxProtocolJson.WriteOptions);
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
