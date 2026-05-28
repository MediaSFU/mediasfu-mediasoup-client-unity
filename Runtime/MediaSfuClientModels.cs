using System;

namespace MediaSFU.MediasoupClientUnity.Contracts
{
    public enum MediaSfuTrackKind
    {
        Audio,
        Video,
        Screen,
        Whiteboard
    }

    public enum MediaSfuOperationStatus
    {
        Success,
        Failure,
        Deferred
    }

    [Serializable]
    public sealed class MediaSfuRoom
    {
        public string RoomName { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class MediaSfuRoomValidation
    {
        public bool HasRtpCapabilities { get; set; }

        public string RtpCapabilitiesJson { get; set; } = string.Empty;

        public string SecureCode { get; set; } = string.Empty;

        public string RawPayload { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class MediaSfuParticipant
    {
        public string ParticipantId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class MediaSfuRemoteProducerEvent
    {
        public string ProducerId { get; set; } = string.Empty;

        public string IsLevel { get; set; } = string.Empty;

        public bool IsPipeProducer { get; set; }

        public string TranslationMetaJson { get; set; } = string.Empty;

        public string RawPayload { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class MediaSfuRemoteProducerClosedEvent
    {
        public string RemoteProducerId { get; set; } = string.Empty;

        public string RawPayload { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class MediaSfuOperationResult<T>
    {
        private MediaSfuOperationResult(MediaSfuOperationStatus status, T value, string error, string detail)
        {
            Status = status;
            Value = value;
            Error = error;
            Detail = detail;
        }

        public MediaSfuOperationStatus Status { get; }

        public T Value { get; }

        public string Error { get; }

        public string Detail { get; }

        public bool Success => Status == MediaSfuOperationStatus.Success;

        public static MediaSfuOperationResult<T> FromSuccess(T value)
        {
            return new MediaSfuOperationResult<T>(MediaSfuOperationStatus.Success, value, string.Empty, string.Empty);
        }

        public static MediaSfuOperationResult<T> FromSuccess(T value, string detail)
        {
            return new MediaSfuOperationResult<T>(MediaSfuOperationStatus.Success, value, string.Empty, detail ?? string.Empty);
        }

        public static MediaSfuOperationResult<T> FromFailure(string error, string detail = "")
        {
            return new MediaSfuOperationResult<T>(MediaSfuOperationStatus.Failure, default(T), error ?? string.Empty, detail ?? string.Empty);
        }

        public static MediaSfuOperationResult<T> FromDeferredContract(string detail)
        {
            return new MediaSfuOperationResult<T>(MediaSfuOperationStatus.Deferred, default(T), string.Empty, detail ?? string.Empty);
        }
    }
}
