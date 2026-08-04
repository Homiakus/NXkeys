using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NX2512_ControlCenter
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            string baseDirectory = AppContext.BaseDirectory;
            string executable = Path.Combine(baseDirectory, "NX2512_HotkeyStudio.exe");
            if (!File.Exists(executable))
            {
                string parent = Directory.GetParent(baseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? baseDirectory;
                executable = Path.Combine(parent, "NX2512_HotkeyStudio.exe");
            }
            if (!File.Exists(executable))
            {
                MessageBox.Show(
                    "Единый NXKeys Control Center не найден. Переустановите managed package NXKeys.",
                    "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
                return;
            }

            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? baseDirectory
            };
            start.ArgumentList.Add("--gui");
            foreach (string argument in args ?? Array.Empty<string>()) start.ArgumentList.Add(argument);
            Process.Start(start);
        }
    }
}
