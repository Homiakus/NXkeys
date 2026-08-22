using System;
using System.IO;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Транспорт файловой IPC-очереди (модуль D). Владеет путями и файловым IO
    /// (атомарная запись, чтение контекста/результата). Не выполняет подпись/allowlist —
    /// запрос поступает уже аутентифицированным.
    /// </summary>
    public sealed class NxFileQueueTransport : INxQueueTransport
    {
        public string BridgeRoot
        {
            get
            {
                string overrideRoot = Environment.GetEnvironmentVariable("NXKEYS_BRIDGE_ROOT");
                return string.IsNullOrWhiteSpace(overrideRoot)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "bridge")
                    : Path.GetFullPath(overrideRoot);
            }
        }

        public string PendingDirectory => Path.Combine(BridgeRoot, "pending");
        public string ProcessingDirectory => Path.Combine(BridgeRoot, "processing");
        public string CompletedDirectory => Path.Combine(BridgeRoot, "completed");
        public string FailedDirectory => Path.Combine(BridgeRoot, "failed");
        public string ContextPath => Path.Combine(BridgeRoot, "context.json");

        public NxTransportReadResult<NxBridgeContext> ReadContextDetailed()
        {
            if (!File.Exists(ContextPath))
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.NotFound, "Bridge context file was not found.");
            try
            {
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    NxBridgeContext context = JsonSerializer.Deserialize<NxBridgeContext>(stream, NxProtocolJson.ReadOptions);
                    if (context == null)
                        return NxTransportReadResult<NxBridgeContext>.Failure(
                            NxTransportReadStatus.Corrupt, "Bridge context JSON is empty.");
                    if (context.SchemaVersion != NxProtocolConstants.SchemaVersion)
                        return NxTransportReadResult<NxBridgeContext>.Failure(
                            NxTransportReadStatus.SchemaMismatch,
                            "Unsupported Bridge context schema: " + context.SchemaVersion + ".");
                    return NxTransportReadResult<NxBridgeContext>.Success(context);
                }
            }
            catch (JsonException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.Corrupt, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.AccessDenied, exception.Message);
            }
            catch (IOException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.IoError, exception.Message);
            }
        }

        public NxBridgeContext ReadContext()
        {
            NxTransportReadResult<NxBridgeContext> read = ReadContextDetailed();
            return read.IsSuccess ? read.Value : null;
        }

        public NxTransportReadResult<NxBridgeResult> ReadResultDetailed(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return NxTransportReadResult<NxBridgeResult>.Failure(
                    NxTransportReadStatus.InvalidRequest, "requestId is required.");
            foreach (string directory in new[] { CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".result.json");
                if (!File.Exists(path)) continue;
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        NxBridgeResult result = JsonSerializer.Deserialize<NxBridgeResult>(stream, NxProtocolJson.ReadOptions);
                        if (result == null)
                            return NxTransportReadResult<NxBridgeResult>.Failure(
                                NxTransportReadStatus.Corrupt, "Bridge result JSON is empty.");
                        if (result.SchemaVersion != NxProtocolConstants.SchemaVersion)
                            return NxTransportReadResult<NxBridgeResult>.Failure(
                                NxTransportReadStatus.SchemaMismatch,
                                "Unsupported Bridge result schema: " + result.SchemaVersion + ".");
                        return NxTransportReadResult<NxBridgeResult>.Success(result);
                    }
                }
                catch (JsonException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.Corrupt, exception.Message);
                }
                catch (UnauthorizedAccessException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.AccessDenied, exception.Message);
                }
                catch (IOException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.IoError, exception.Message);
                }
            }
            return NxTransportReadResult<NxBridgeResult>.Failure(
                NxTransportReadStatus.NotFound, "Bridge result file was not found.");
        }

        public bool TryReadResult(string requestId, out NxBridgeResult result)
        {
            NxTransportReadResult<NxBridgeResult> read = ReadResultDetailed(requestId);
            result = read.IsSuccess ? read.Value : null;
            return read.IsSuccess;
        }

        public string FindRequestFile(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return string.Empty;
            foreach (string directory in new[] { PendingDirectory, ProcessingDirectory, CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".request.json");
                if (File.Exists(path)) return path;
            }
            return string.Empty;
        }

        public void WriteRequest(NxCommandRequest request)
        {
            Directory.CreateDirectory(PendingDirectory);
            Directory.CreateDirectory(ProcessingDirectory);
            Directory.CreateDirectory(CompletedDirectory);
            Directory.CreateDirectory(FailedDirectory);

            int pendingCount = Directory.GetFiles(PendingDirectory, "*.request.json").Length;
            if (pendingCount >= NxProtocolConstants.MaxPendingRequestCount)
                throw new InvalidOperationException(
                    "NXKeys Bridge queue limit reached: " + pendingCount + ".");

            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".request.json");
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, NxProtocolJson.WriteOptions);
            if (payload.Length > NxProtocolConstants.MaxRequestPayloadBytes)
                throw new InvalidOperationException(
                    "NXKeys request payload exceeds " + NxProtocolConstants.MaxRequestPayloadBytes + " bytes.");
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, finalPath);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            }
        }
    }
}
