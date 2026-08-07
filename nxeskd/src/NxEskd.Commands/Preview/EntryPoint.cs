using NXOpen;
using NxEskd.Core.Runtime;
using NxEskd.NxRuntime;

namespace NxEskd.Commands.Preview;

public static class EntryPoint
{
    public static int Main(string[] args) => CommandHost.Run(DrawingCommand.Preview);

    /// <summary>
    /// AtTermination: сборка остаётся загруженной до завершения NX.
    /// Устраняет ".NET Core Unload Warning: Could not successfully unload DLL".
    /// После обновления плагина требуется полный перезапуск NX.
    /// </summary>
    public static int GetUnloadOption(string dummy) => (int)Session.LibraryUnloadOption.AtTermination;
}
