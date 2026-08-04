using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NXKeys.Protocol;

namespace NXKeys.BridgeCore
{
    public sealed class BridgeRequestClaim
    {
        public string ProcessingPath { get; }
        public string RequestId { get; }
        public NxCommandRequest Request { get; }

        public BridgeRequestClaim(string processingPath, string requestId, NxCommandRequest request)
        {
            ProcessingPath = processingPath ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }
    }

    public sealed class BridgeRequestRejection
    {
        public string ProcessingPath { get; }
        public string RequestId { get; }
        public string Message { get; }

        public BridgeRequestRejection(string processingPath, string requestId, string message)
        {
            ProcessingPath = processingPath ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class BridgeRequestInbox : IDisposable
    {
        private readonly string pendingDirectory;
        private readonly string processingDirectory;
        private readonly Func<string, bool> resultExists;
        private readonly Action<string, string> duplicateHandler;
        private readonly Action<NxCommandRequest> admission;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<BridgeRequestClaim> ready = new ConcurrentQueue<BridgeRequestClaim>();
        private readonly ConcurrentQueue<BridgeRequestRejection> rejected = new ConcurrentQueue<BridgeRequestRejection>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Thread? worker;
        private int started;

        public int ReadyCount => ready.Count;
        public int RejectedCount => rejected.Count;
        public int BufferedCount => ready.Count + rejected.Count;
        public bool IsRunning => worker != null && worker.IsAlive && !cancellation.IsCancellationRequested;

        public BridgeRequestInbox(
            string pendingPath,
            string processingPath,
            Func<string, bool> resultExistsCallback,
            Action<string, string> duplicateCallback,
            Action<NxCommandRequest> admissionCallback,
            Action<string>? logger = null)
        {
            pendingDirectory = Path.GetFullPath(pendingPath ?? throw new ArgumentNullException(nameof(pendingPath)));
            processingDirectory = Path.GetFullPath(processingPath ?? throw new ArgumentNullException(nameof(processingPath)));
            resultExists = resultExistsCallback ?? (_ => false);
            duplicateHandler = duplicateCallback ?? ((_, _) => { });
            admission = admissionCallback ?? throw new ArgumentNullException(nameof(admissionCallback));
            log = logger ?? (_ => { });
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref started, 1) == 1) return;
            Directory.CreateDirectory(pendingDirectory);
            Directory.CreateDirectory(processingDirectory);
            worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "NXKeys Bridge Inbox"
            };
            worker.Start();
            Signal();
        }

        public void Signal()
        {
            if (!cancellation.IsCancellationRequested) wake.Set();
        }

        public bool TryDequeue(out BridgeRequestClaim? claim) => ready.TryDequeue(out claim);
        public bool TryDequeueRejected(out BridgeRequestRejection? rejection) => rejected.TryDequeue(out rejection);

        private void WorkerLoop()
        {
            WaitHandle[] handles = { wake, cancellation.Token.WaitHandle };
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    int signaled = WaitHandle.WaitAny(handles, TimeSpan.FromSeconds(1));
                    if (signaled == 1 || cancellation.IsCancellationRequested) break;
                    ScanOnce();
                }
            }
            catch (Exception exception)
            {
                log("Bridge inbox worker stopped after an unexpected error: " + exception);
            }
        }

        private void ScanOnce()
        {
            Directory.CreateDirectory(pendingDirectory);
            Directory.CreateDirectory(processingDirectory);
            string[] files = Directory.GetFiles(pendingDirectory, "*.request.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length > NxProtocolConstants.MaxPendingRequestCount)
                log("Pending queue exceeds limit: " + files.Length + ". Background admission remains bounded.");

            int capacity = Math.Max(0, NxProtocolConstants.MaxPendingRequestCount - BufferedCount);
            foreach (string pendingPath in files.Take(Math.Min(NxProtocolConstants.MaxRequestsPerPoll, capacity)))
                ClaimAndValidate(pendingPath);
        }

        private void ClaimAndValidate(string pendingPath)
        {
            string fileName = Path.GetFileName(pendingPath);
            string requestId = fileName.EndsWith(".request.json", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - ".request.json".Length)
                : Path.GetFileNameWithoutExtension(fileName);

            if (resultExists(requestId))
            {
                duplicateHandler(pendingPath, requestId);
                return;
            }

            string processingPath = Path.Combine(processingDirectory, fileName);
            try
            {
                File.Move(pendingPath, processingPath);
            }
            catch (IOException)
            {
                return;
            }

            try
            {
                long payloadLength = new FileInfo(processingPath).Length;
                if (payloadLength <= 0 || payloadLength > NxProtocolConstants.MaxRequestPayloadBytes)
                    throw new InvalidOperationException(
                        "Request payload size is outside the allowed range: " + payloadLength + " bytes.");
                NxCommandRequest? request;
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    request = JsonSerializer.Deserialize<NxCommandRequest>(stream, NxProtocolJson.ReadOptions);
                if (request == null) throw new InvalidOperationException("Request JSON is empty.");
                request.Validate();
                if (!string.Equals(request.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("request_id does not match the file name.");
                admission(request);
                ready.Enqueue(new BridgeRequestClaim(processingPath, requestId, request));
            }
            catch (Exception exception)
            {
                rejected.Enqueue(new BridgeRequestRejection(processingPath, requestId, exception.Message));
            }
        }

        public void Dispose()
        {
            if (cancellation.IsCancellationRequested) return;
            cancellation.Cancel();
            wake.Set();
            try { worker?.Join(2000); } catch { }
            wake.Dispose();
            cancellation.Dispose();
        }
    }
}
