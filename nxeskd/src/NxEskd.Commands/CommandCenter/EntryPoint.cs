using NXOpen;
using NxEskd.NxRuntime;

namespace NxEskd.Commands.CommandCenter;

public static class EntryPoint
{
    public static int Main(string[] args) => CommandHost.OpenCommandCenter();

    /// <summary>
    /// AtTermination: сборка остаётся загруженной до завершения NX.
    /// Это устраняет предупреждения ".NET Core Unload Warning" от статических
    /// конструкторов (NXColor+Factory, Encryptor и др.), удерживающих контекст.
    /// После обновления плагина требуется полный перезапуск NX.
    /// </summary>
    public static int GetUnloadOption(string dummy) => (int)Session.LibraryUnloadOption.AtTermination;
}
