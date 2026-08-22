using NX2512_HotkeyStudio.Models;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Транспорт файловой IPC-очереди (модуль D): пути, атомарная запись запроса,
    /// чтение контекста и результата. Не знает про подпись/allowlist — принимает
    /// уже аутентифицированный запрос.
    /// </summary>
    public interface INxQueueTransport
    {
        string BridgeRoot { get; }
        string PendingDirectory { get; }
        string ProcessingDirectory { get; }
        string CompletedDirectory { get; }
        string FailedDirectory { get; }
        string ContextPath { get; }

        NxTransportReadResult<NxBridgeContext> ReadContextDetailed();
        NxBridgeContext ReadContext();
        NxTransportReadResult<NxBridgeResult> ReadResultDetailed(string requestId);
        bool TryReadResult(string requestId, out NxBridgeResult result);
        string FindRequestFile(string requestId);

        /// <summary>Атомарная запись запроса в очередь (лимиты + tmp→final move).</summary>
        void WriteRequest(NxCommandRequest request);
    }
}
