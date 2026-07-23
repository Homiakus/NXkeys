using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class NxCommandRequest
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 2;

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = "execute_command";

        [JsonPropertyName("command_id")]
        public string CommandId { get; set; } = string.Empty;

        [JsonPropertyName("command_name")]
        public string CommandName { get; set; } = string.Empty;

        [JsonPropertyName("sequence")]
        public string Sequence { get; set; } = string.Empty;

        [JsonPropertyName("module_id")]
        public string ModuleId { get; set; } = string.Empty;

        [JsonPropertyName("target_application_id")]
        public string TargetApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("created_utc")]
        public string CreatedUtc { get; set; } = string.Empty;

        [JsonPropertyName("expires_utc")]
        public string ExpiresUtc { get; set; } = string.Empty;

        [JsonPropertyName("source_process_id")]
        public int SourceProcessId { get; set; }

        [JsonPropertyName("expected_context_revision")]
        public long ExpectedContextRevision { get; set; }

        [JsonPropertyName("expected_selection_count")]
        public int ExpectedSelectionCount { get; set; } = -1;
    }

    public sealed class NxBridgeContext
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("revision")]
        public long Revision { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("module_id")]
        public string ModuleId { get; set; } = string.Empty;

        [JsonPropertyName("module_label")]
        public string ModuleLabel { get; set; } = string.Empty;

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("updated_utc")]
        public string UpdatedUtc { get; set; } = string.Empty;

        [JsonPropertyName("last_request_id")]
        public string LastRequestId { get; set; } = string.Empty;

        [JsonPropertyName("last_result")]
        public string LastResult { get; set; } = string.Empty;

        [JsonPropertyName("last_message")]
        public string LastMessage { get; set; } = string.Empty;

        [JsonPropertyName("selection_count")]
        public int SelectionCount { get; set; } = -1;

        [JsonPropertyName("work_part_available")]
        public bool WorkPartAvailable { get; set; } = true;

        [JsonPropertyName("display_part_available")]
        public bool DisplayPartAvailable { get; set; } = true;

        [JsonPropertyName("modal_dialog_active")]
        public bool ModalDialogActive { get; set; }

        [JsonIgnore]
        public bool IsFresh
        {
            get
            {
                if (!DateTime.TryParse(UpdatedUtc, out DateTime value)) return false;
                return DateTime.UtcNow - value.ToUniversalTime() < TimeSpan.FromSeconds(10);
            }
        }
    }

    public static class NxCommandBridgeClient
    {
        public static string BridgeRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NXKeys",
            "bridge");

        public static string PendingDirectory => Path.Combine(BridgeRoot, "pending");
        public static string ContextPath => Path.Combine(BridgeRoot, "context.json");

        public static NxCommandRequest Enqueue(LeaderSequenceItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Command == null || string.IsNullOrWhiteSpace(item.Command.ID))
                throw new InvalidOperationException("Leader sequence has no NX command id.");

            NxBridgeContext context = ReadContext();
            NxCommandRequest request = CreateRequest(
                "execute_command",
                item.Command.ID.Trim(),
                item.Command.Name,
                item.Sequence,
                item.ModuleID,
                string.Empty,
                context);
            WriteRequest(request);
            return request;
        }

        public static NxCommandRequest EnqueueModuleSwitch(ModuleConfig module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            string appId = module.NXApplicationIDs != null && module.NXApplicationIDs.Count > 0
                ? module.NXApplicationIDs[0]
                : module.SwitchCommand?.ID;
            if (string.IsNullOrWhiteSpace(appId))
                throw new InvalidOperationException("Module has no NX application id.");

            NxCommandRequest request = CreateRequest(
                "switch_module",
                module.SwitchCommand?.ID,
                module.SwitchCommand?.Name ?? module.Label,
                string.Empty,
                module.ID,
                appId.Trim(),
                ReadContext());
            WriteRequest(request);
            return request;
        }

        public static NxBridgeContext ReadContext()
        {
            try
            {
                if (!File.Exists(ContextPath)) return null;
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return JsonSerializer.Deserialize<NxBridgeContext>(stream, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
            catch
            {
                return null;
            }
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
            DateTime now = DateTime.UtcNow;
            return new NxCommandRequest
            {
                RequestId = $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
                Action = action,
                CommandId = commandId ?? string.Empty,
                CommandName = commandName ?? string.Empty,
                Sequence = sequence ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                TargetApplicationId = targetApplicationId ?? string.Empty,
                CreatedUtc = now.ToString("O"),
                ExpiresUtc = now.AddSeconds(15).ToString("O"),
                SourceProcessId = Process.GetCurrentProcess().Id,
                ExpectedContextRevision = context?.Revision ?? 0,
                ExpectedSelectionCount = context?.SelectionCount ?? -1
            };
        }

        private static void WriteRequest(NxCommandRequest request)
        {
            Directory.CreateDirectory(PendingDirectory);
            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".json");
            string tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, finalPath);
        }
    }
}
