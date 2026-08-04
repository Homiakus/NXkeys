using System;

namespace NX2512_HotkeyStudio.Services
{
    public enum NxTransportReadStatus
    {
        Success,
        NotFound,
        InvalidRequest,
        Corrupt,
        SchemaMismatch,
        AccessDenied,
        IoError
    }

    public sealed class NxTransportReadResult<T> where T : class
    {
        public NxTransportReadStatus Status { get; }
        public T Value { get; }
        public string Message { get; }
        public bool IsSuccess => Status == NxTransportReadStatus.Success && Value != null;

        private NxTransportReadResult(NxTransportReadStatus status, T value, string message)
        {
            Status = status;
            Value = value;
            Message = message ?? string.Empty;
        }

        public static NxTransportReadResult<T> Success(T value) =>
            new NxTransportReadResult<T>(NxTransportReadStatus.Success, value, string.Empty);

        public static NxTransportReadResult<T> Failure(NxTransportReadStatus status, string message) =>
            new NxTransportReadResult<T>(status, null, message);
    }
}
