using System;
using System.Collections.Generic;
using System.Linq;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.UI
{
    public static class CommandMenuPolicy
    {
        public static string ResolveMenuLabel(string key, IEnumerable<LeaderSequenceItem> values, int prefixDepth)
        {
            List<LeaderSequenceItem> group = (values ?? Enumerable.Empty<LeaderSequenceItem>()).Where(item => item != null).ToList();
            int pathIndex = Math.Max(0, prefixDepth - 1); // Sequence includes the module prefix; PathLabels does not.
            string label = group.Select(item => item.PathLabels != null && item.PathLabels.Count > pathIndex
                    ? item.PathLabels[pathIndex]
                    : string.Empty)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(label) && pathIndex == 0)
                label = group.Select(item => item.SubmenuLabel).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return string.IsNullOrWhiteSpace(label) ? "Подменю " + key : label;
        }
    }
}
