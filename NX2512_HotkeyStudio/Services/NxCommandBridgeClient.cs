using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class NxCommandRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string Action { get; set; } = "execute_command";
        public string CommandId { get; set; } = string.Empty;
        public string CommandName { get; set; } = string.Empty;
        public string Sequence { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string TargetApplicationId { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
        public int SourceProcessId { get; set; }
    }

    public sealed class NxBridgeContext
    {
        public string Status { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string ModuleLabel { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;
        public string UpdatedUtc { get; set; } = string.Empty;
        public string LastRequestId { get; set; } = string.Empty;
        public string LastResult { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
    }

    public static class NxCommandBridgeClient
    {
        public static string BridgeRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys",
                    "bridge");
            }
        }

        public static string PendingDirectory => Path.Combine(BridgeRoot, "pending");

        public static string ContextPath => Path.Combine(BridgeRoot, "context.json");

        public static NxCommandRequest Enqueue(LeaderSequenceItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Command == null || string.IsNullOrWhiteSpace(item.Command.ID))
            {
                throw new InvalidOperationException("Leader sequence has no NX command id.");
            }

            Directory.CreateDirectory(PendingDirectory);

            NxCommandRequest request = new NxCommandRequest
            {
                RequestId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
                Action = "execute_command",
                CommandId = item.Command.ID.Trim(),
                CommandName = item.Command.Name ?? string.Empty,
                Sequence = item.Sequence ?? string.Empty,
                ModuleId = item.ModuleID ?? string.Empty,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                SourceProcessId = Process.GetCurrentProcess().Id
            };

            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".json");
            string tempPath = finalPath + ".tmp";
            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(request, options));
            File.Move(tempPath, finalPath);

            return request;
        }

        public static NxCommandRequest EnqueueModuleSwitch(ModuleConfig module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            string appId = module.NXApplicationIDs != null && module.NXApplicationIDs.Count > 0
                ? module.NXApplicationIDs[0]
                : module.SwitchCommand?.ID;
            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new InvalidOperationException("Module has no NX application id.");
            }

            Directory.CreateDirectory(PendingDirectory);
            NxCommandRequest request = new NxCommandRequest
            {
                RequestId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
                Action = "switch_module",
                CommandId = module.SwitchCommand?.ID ?? string.Empty,
                CommandName = module.SwitchCommand?.Name ?? module.Label,
                ModuleId = module.ID ?? string.Empty,
                TargetApplicationId = appId.Trim(),
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                SourceProcessId = Process.GetCurrentProcess().Id
            };

            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".json");
            string tempPath = finalPath + ".tmp";
            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(request, options));
            File.Move(tempPath, finalPath);
            return request;
        }

        public static NxBridgeContext ReadContext()
        {
            try
            {
                if (!File.Exists(ContextPath)) return null;
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return JsonSerializer.Deserialize<NxBridgeContext>(stream);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
