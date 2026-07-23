using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public static class MenuScriptWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string Normalize(string content, int version)
        {
            if (content == null) content = string.Empty;

            string normalized = Regex.Replace(content, @"(?m)^VERSION\s+\d+\s*$", "VERSION " + version);
            normalized = normalized.Replace("\r\n", "\n").Replace("\n\r", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
            if (!normalized.EndsWith("\r\n", StringComparison.Ordinal)) normalized += "\r\n";
            return normalized;
        }

        public static void WriteAllText(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, Normalize(content, MenuScriptDefaults.ExpectedVersionForPath(path)), Utf8NoBom);
        }
    }
}
