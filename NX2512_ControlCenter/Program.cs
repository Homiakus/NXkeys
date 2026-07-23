using System;
using System.IO;
using System.Windows.Forms;

namespace NX2512_ControlCenter
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            string config = ResolveArgument(args, "--config") ?? Path.Combine(AppContext.BaseDirectory, "config", "nx2512-pro-hybrid.json");
            string catalog = ResolveArgument(args, "--catalog") ?? string.Empty;
            Application.Run(new ControlCenterForm(config, catalog));
        }

        private static string ResolveArgument(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            }
            return null;
        }
    }
}
