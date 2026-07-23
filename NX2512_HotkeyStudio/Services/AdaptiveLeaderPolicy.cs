using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;
using NXKeys.StateMachines;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class LeaderCommandAvailability
    {
        public bool IsVisible { get; set; } = true;
        public bool CanExecute { get; set; } = true;
        public bool RequiresModuleSwitch { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    public static class AdaptiveLeaderPolicy
    {
        private static readonly ContextGuardEvaluator GuardEvaluator = new ContextGuardEvaluator();

        public static LeaderCommandAvailability Evaluate(
            LeaderSequenceItem item,
            NxBridgeContext context,
            LeaderUsageSnapshot usage,
            string activeModuleId)
        {
            var result = new LeaderCommandAvailability();
            if (item == null || !item.Enabled)
            {
                result.IsVisible = false;
                result.CanExecute = false;
                result.Reason = "Команда отключена";
                return result;
            }

            SequenceDefinition definition = ToDefinition(item);
            if (!string.IsNullOrWhiteSpace(activeModuleId) && context != null)
                context.ModuleId = activeModuleId;
            GuardResult guard = GuardEvaluator.Evaluate(definition, context, true);

            result.Score = 100;
            string normalizedItemModule = ContextGuardEvaluator.NormalizeModule(item.ModuleID);
            string normalizedActiveModule = ContextGuardEvaluator.NormalizeModule(activeModuleId ?? context?.ModuleId);
            if (string.Equals(normalizedItemModule, normalizedActiveModule, StringComparison.OrdinalIgnoreCase)) result.Score += 60;
            if (guard.RequiresModuleSwitch)
            {
                result.RequiresModuleSwitch = true;
                result.CanExecute = true;
                result.Reason = "Будет выполнено после переключения модуля";
                result.Score -= 12;
            }
            else if (!guard.Allowed)
            {
                result.CanExecute = false;
                result.Reason = guard.Reason;
                result.Score -= 100;
            }

            if (usage != null)
            {
                result.Score += Math.Min(35, Math.Log10(usage.ExecutionCount + 1) * 18.0);
                if (usage.LastExecutedUtc > DateTime.MinValue)
                {
                    double ageHours = Math.Max(0, (DateTime.UtcNow - usage.LastExecutedUtc).TotalHours);
                    result.Score += Math.Max(0, 18 - Math.Log10(ageHours + 1) * 8.0);
                }
            }

            if (item.Destructive || item.ConfirmBeforeExecute)
            {
                result.Score -= 12;
                if (string.IsNullOrWhiteSpace(result.Reason)) result.Reason = "Требуется подтверждение";
            }
            return result;
        }

        public static List<LeaderSequenceItem> Rank(
            IEnumerable<LeaderSequenceItem> items,
            NxBridgeContext context,
            LeaderUsageStore usageStore,
            string activeModuleId,
            bool includeUnavailable)
        {
            return (items ?? Enumerable.Empty<LeaderSequenceItem>())
                .Select(item => new
                {
                    Item = item,
                    Availability = Evaluate(item, context, usageStore?.Get(item), activeModuleId)
                })
                .Where(value => value.Availability.IsVisible && (includeUnavailable || value.Availability.CanExecute))
                .OrderByDescending(value => value.Availability.Score)
                .ThenBy(value => value.Item.Sequence, StringComparer.OrdinalIgnoreCase)
                .Select(value => value.Item)
                .ToList();
        }

        public static SequenceDefinition ToDefinition(LeaderSequenceItem item)
        {
            if (item == null) return null;
            return new SequenceDefinition
            {
                Id = NormalizeSequence(item.Sequence),
                Sequence = item.Sequence ?? string.Empty,
                ModuleId = NormalizeModule(item.ModuleID, item.Category),
                CommandId = item.Command?.ID ?? string.Empty,
                CommandName = item.Command?.Name ?? string.Empty,
                SearchText = string.Join(" ", new[] { item.Category, item.Notes, item.Fallback }),
                RequiresSelection = item.RequiresSelection,
                MinimumSelectionCount = 1,
                NeedsWorkPart = ContextGuardEvaluator.CommandNeedsWorkPart(item.Command?.ID),
                Destructive = item.Destructive,
                ConfirmBeforeExecute = item.ConfirmBeforeExecute || item.Destructive,
                Enabled = item.Enabled
            };
        }

        public static string NormalizeModule(string moduleId, string category)
        {
            string value = !string.IsNullOrWhiteSpace(moduleId) ? moduleId : category;
            return ContextGuardEvaluator.NormalizeModule(value);
        }

        public static string NormalizeSequence(string sequence)
        {
            return new string((sequence ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }
    }

    public sealed class LeaderUsageSnapshot
    {
        public int ExecutionCount { get; set; }
        public DateTime LastExecutedUtc { get; set; }
    }

    public sealed class LeaderUsageStore
    {
        private readonly string path;
        private readonly Dictionary<string, LeaderUsageSnapshot> entries =
            new Dictionary<string, LeaderUsageSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly object sync = new object();

        public LeaderUsageStore()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NXKeys");
            Directory.CreateDirectory(root);
            path = Path.Combine(root, "leader-usage.json");
            Load();
        }

        public LeaderUsageSnapshot Get(LeaderSequenceItem item)
        {
            if (item == null) return new LeaderUsageSnapshot();
            string key = Key(item);
            lock (sync)
            {
                if (entries.TryGetValue(key, out LeaderUsageSnapshot value))
                {
                    return new LeaderUsageSnapshot
                    {
                        ExecutionCount = value.ExecutionCount,
                        LastExecutedUtc = value.LastExecutedUtc
                    };
                }
                return new LeaderUsageSnapshot();
            }
        }

        public void Record(LeaderSequenceItem item)
        {
            if (item == null) return;
            string key = Key(item);
            lock (sync)
            {
                if (!entries.TryGetValue(key, out LeaderUsageSnapshot value))
                {
                    value = new LeaderUsageSnapshot();
                    entries[key] = value;
                }
                value.ExecutionCount++;
                value.LastExecutedUtc = DateTime.UtcNow;
                Save();
            }
        }

        private static string Key(LeaderSequenceItem item)
        {
            string id = item.Command?.ID ?? item.Command?.Name ?? string.Empty;
            return AdaptiveLeaderPolicy.NormalizeSequence(item.Sequence) + "|" + id.ToUpperInvariant();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(path)) return;
                Dictionary<string, LeaderUsageSnapshot> data =
                    JsonSerializer.Deserialize<Dictionary<string, LeaderUsageSnapshot>>(File.ReadAllText(path));
                if (data == null) return;
                foreach (var pair in data) entries[pair.Key] = pair.Value;
            }
            catch
            {
                string corrupt = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try { if (File.Exists(path)) File.Move(path, corrupt); } catch { }
            }
        }

        private void Save()
        {
            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            AtomicFileWriter.WriteAllText(path, json + Environment.NewLine, true);
        }
    }
}
