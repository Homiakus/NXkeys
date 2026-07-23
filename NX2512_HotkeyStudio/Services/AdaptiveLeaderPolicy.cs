using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class LeaderCommandAvailability
    {
        public bool IsVisible { get; set; } = true;
        public bool CanExecute { get; set; } = true;
        public string Reason { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    public static class AdaptiveLeaderPolicy
    {
        private static readonly HashSet<string> SharedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "selection_object", "inspect_view", "reuse"
        };

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

            string itemModule = NormalizeModule(item.ModuleID, item.Category);
            string activeModule = string.IsNullOrWhiteSpace(activeModuleId)
                ? NormalizeModule(context?.ModuleId, string.Empty)
                : NormalizeModule(activeModuleId, string.Empty);

            bool moduleMatches = string.IsNullOrWhiteSpace(itemModule) ||
                                 string.IsNullOrWhiteSpace(activeModule) ||
                                 string.Equals(itemModule, activeModule, StringComparison.OrdinalIgnoreCase) ||
                                 SharedModules.Contains(itemModule);

            if (!moduleMatches)
            {
                result.IsVisible = false;
                result.CanExecute = false;
                result.Reason = "Недоступно в текущем модуле";
                return result;
            }

            result.Score = 100;
            if (string.Equals(itemModule, activeModule, StringComparison.OrdinalIgnoreCase)) result.Score += 60;
            if (SharedModules.Contains(itemModule)) result.Score += 20;
            if (usage != null)
            {
                result.Score += Math.Min(35, Math.Log10(usage.ExecutionCount + 1) * 18.0);
                if (usage.LastExecutedUtc > DateTime.MinValue)
                {
                    double ageHours = Math.Max(0, (DateTime.UtcNow - usage.LastExecutedUtc).TotalHours);
                    result.Score += Math.Max(0, 18 - Math.Log10(ageHours + 1) * 8.0);
                }
            }

            if (item.RequiresSelection && context != null && context.SelectionCount == 0)
            {
                result.CanExecute = false;
                result.Reason = "Сначала выберите объект";
                result.Score -= 80;
            }
            else if (item.RequiresSelection && context != null && context.SelectionCount < 0)
            {
                result.Reason = "Требуется выбор объекта";
                result.Score -= 8;
            }

            if (context != null && context.ModalDialogActive)
            {
                result.CanExecute = false;
                result.Reason = "Закройте активный диалог NX";
                result.Score -= 100;
            }

            if (item.Destructive || item.ConfirmBeforeExecute)
            {
                result.Score -= 12;
                if (string.IsNullOrWhiteSpace(result.Reason)) result.Reason = "Требуется подтверждение";
            }

            if (context != null && !context.WorkPartAvailable && NeedsWorkPart(item))
            {
                result.CanExecute = false;
                result.Reason = "Нет активной рабочей детали";
                result.Score -= 100;
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
                .Where(x => x.Availability.IsVisible && (includeUnavailable || x.Availability.CanExecute))
                .OrderByDescending(x => x.Availability.Score)
                .ThenBy(x => x.Item.Sequence, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Item)
                .ToList();
        }

        public static string NormalizeModule(string moduleId, string category)
        {
            string value = !string.IsNullOrWhiteSpace(moduleId) ? moduleId : category;
            value = (value ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
            switch (value)
            {
                case "view":
                case "inspect":
                case "inspect_/_view": return "inspect_view";
                case "selection_filters":
                case "selection": return "selection_object";
                case "cam_/_manufacturing":
                case "cam": return "manufacturing";
                case "cae_/_simulation":
                case "cae": return "simulation";
                case "mold_/_tooling": return "mold";
                case "reuse_/_templates": return "reuse";
                default: return value;
            }
        }

        private static bool NeedsWorkPart(LeaderSequenceItem item)
        {
            string id = item?.Command?.ID ?? string.Empty;
            if (id.StartsWith("UG_FILE_", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("UG_APP_", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("UG_VIEW_", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("UG_HELP_", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
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
                if (entries.TryGetValue(key, out LeaderUsageSnapshot value)) return value;
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
            return (item.Sequence ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant() + "|" + id.ToUpperInvariant();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(path)) return;
                Dictionary<string, LeaderUsageSnapshot> data = JsonSerializer.Deserialize<Dictionary<string, LeaderUsageSnapshot>>(File.ReadAllText(path));
                if (data == null) return;
                foreach (var pair in data) entries[pair.Key] = pair.Value;
            }
            catch
            {
            }
        }

        private void Save()
        {
            try
            {
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch
            {
            }
        }
    }
}
