using System.Collections.Generic;
using System.IO;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Defines the single canonical NX ribbon location and the legacy files that must be removed.
    /// Loading the same tab from startup and application/profiles/All creates duplicate NXKeys tabs.
    /// </summary>
    public static class NxRibbonLayout
    {
        public static string CanonicalRelativePath =>
            Path.Combine("custom", "application", "profiles", "All", "rbn_nxkeys.rtb");

        public static IReadOnlyList<string> LegacyRelativePaths { get; } = new[]
        {
            Path.Combine("custom", "startup", "nxkeys_ribbon.rtb")
        };

        public static string BuildTabFile(int toolbarVersion) =>
            "! NXKeys launch ribbon\r\n" +
            "TITLE NXKeys\r\n" +
            "VERSION " + toolbarVersion + "\r\n" +
            "BEGIN_GROUP NXKEYS_RBN_GROUP\r\n" +
            "LABEL NXKeys\r\n" +
            "BITMAP finished_flag\r\n" +
            "    BUTTON UG_NXKEYS_START_BRIDGE\r\n" +
            "    BUTTON UG_NXKEYS_START_DAEMON\r\n" +
            "    BUTTON UG_NXKEYS_OPEN_STUDIO\r\n" +
            "END_GROUP\r\n";
    }
}
