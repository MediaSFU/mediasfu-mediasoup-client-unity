using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSFU.MediasoupClientUnity.Contracts;

namespace MediaSFU.Unity
{
    public enum MediaSfuLocalMediaTrackState
    {
        Inactive,
        Active,
        Paused
    }

    public enum MediaSfuTransportDirection
    {
        Send,
        Receive
    }

    public sealed class MediaSfuWebRtcTransportRequest
    {
        public MediaSfuTransportDirection Direction { get; set; } = MediaSfuTransportDirection.Send;

        public string IsLevel { get; set; } = string.Empty;
    }

    public sealed class MediaSfuWebRtcTransport
    {
        public string Id { get; set; } = string.Empty;

        public string IceParametersJson { get; set; } = string.Empty;

        public string IceCandidatesJson { get; set; } = string.Empty;

        public string DtlsParametersJson { get; set; } = string.Empty;

        public string SctpParametersJson { get; set; } = string.Empty;

        public string RawPayload { get; set; } = string.Empty;

        /// <summary>
        /// TURN relay offer the server sent beside the transport params (turn-relay-v1). Kept out of the transport
        /// payload on purpose: engines add the relay at creation only when the server's mode is <c>always</c>.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public MediaSfuIceRelayOffer IceRelayOffer { get; set; }

        /// <summary>The media socket this transport was created on, used by the relay fallback.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public MediaSfuIceRelaySignalling IceRelaySignalling { get; set; }
    }

    public sealed class MediaSfuTransportConnectRequest
    {
        public MediaSfuTransportDirection Direction { get; set; } = MediaSfuTransportDirection.Send;

        public string TransportId { get; set; } = string.Empty;

        public string DtlsParametersJson { get; set; } = string.Empty;
    }

    public sealed class MediaSfuProduceRequest
    {
        public string Kind { get; set; } = string.Empty;

        public string RtpParametersJson { get; set; } = string.Empty;

        public string AppDataJson { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string IsLevel { get; set; } = string.Empty;
    }

    public sealed class MediaSfuProduceResponse
    {
        public string ProducerId { get; set; } = string.Empty;

        public string RawPayload { get; set; } = string.Empty;
    }

    public sealed class MediaSfuConsumeRequest
    {
        public string RemoteProducerId { get; set; } = string.Empty;

        public string ServerConsumerTransportId { get; set; } = string.Empty;

        public string RtpCapabilitiesJson { get; set; } = string.Empty;
    }

    public sealed class MediaSfuConsumeResponse
    {
        public string ConsumerId { get; set; } = string.Empty;

        public string ProducerId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string RtpParametersJson { get; set; } = string.Empty;

        public string ServerConsumerId { get; set; } = string.Empty;

        public string RawPayload { get; set; } = string.Empty;
    }

    public sealed class MediaSfuLocalMediaRuntimeState
    {
        public MediaSfuLocalMediaTrackState TrackState { get; set; } = MediaSfuLocalMediaTrackState.Inactive;

        public string ProducerId { get; set; } = string.Empty;
    }

    public sealed class MediaSfuLocalMediaRequest
    {
        public MediaSfuTrackKind Kind { get; set; }

        public bool Enabled { get; set; }

        public MediaSfuRoom Room { get; set; }

        public MediaSfuRoomValidation RoomValidation { get; set; }

        public MediaSfuParticipant LocalParticipant { get; set; }

        public string LocalUserName { get; set; } = string.Empty;

        public string LocalIsLevel { get; set; } = string.Empty;

        public Func<MediaSfuWebRtcTransportRequest, CancellationToken, Task<MediaSfuOperationResult<MediaSfuWebRtcTransport>>> CreateWebRtcTransportAsync { get; set; }

        public Func<MediaSfuTransportConnectRequest, CancellationToken, Task<MediaSfuOperationResult<bool>>> ConnectWebRtcTransportAsync { get; set; }

        public Func<MediaSfuProduceRequest, CancellationToken, Task<MediaSfuOperationResult<MediaSfuProduceResponse>>> ProduceTrackAsync { get; set; }

        public Func<string, Dictionary<string, object>, CancellationToken, Task> EmitSocketEventAsync { get; set; }

        public Func<string, Dictionary<string, object>, CancellationToken, Task<string>> EmitSocketEventWithAckAsync { get; set; }
    }

    public interface IMediaSfuLocalMediaBackend
    {
        Task<MediaSfuOperationResult<bool>> SetMicrophoneEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default);

        Task<MediaSfuOperationResult<bool>> SetCameraEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default);

        Task<MediaSfuOperationResult<bool>> SetScreenShareEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface IMediaSfuTransportBackedLocalMediaAdapter : IDisposable
    {
        Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default);

        Task InitializeSendTransportAsync(
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default);

        Task<string> CreateSendTransportConnectParametersJsonAsync(
            CancellationToken cancellationToken = default);

        Task CompleteSendTransportConnectAsync(
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default);

        Task<MediaSfuProduceRequest> CreateProduceRequestAsync(
            MediaSfuTrackKind kind,
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default);

        Task BindProducerAsync(
            MediaSfuTrackKind kind,
            string producerId,
            CancellationToken cancellationToken = default);

        Task PauseAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default);

        Task ResumeAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default);

        Task CloseAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default);
    }

    public sealed class MediaSfuDelegateTransportBackedLocalMediaAdapter : IMediaSfuTransportBackedLocalMediaAdapter
    {
        private readonly Func<string, CancellationToken, Task> _ensureCapabilitiesLoadedAsync;
        private readonly Func<MediaSfuWebRtcTransport, CancellationToken, Task> _initializeSendTransportAsync;
        private readonly Func<CancellationToken, Task<string>> _createSendTransportConnectParametersJsonAsync;
        private readonly Func<bool, string, CancellationToken, Task> _completeSendTransportConnectAsync;
        private readonly Func<MediaSfuTrackKind, MediaSfuLocalMediaRequest, CancellationToken, Task<MediaSfuProduceRequest>> _createProduceRequestAsync;
        private readonly Func<MediaSfuTrackKind, string, CancellationToken, Task> _bindProducerAsync;
        private readonly Func<MediaSfuTrackKind, CancellationToken, Task> _pauseAsync;
        private readonly Func<MediaSfuTrackKind, CancellationToken, Task> _resumeAsync;
        private readonly Func<MediaSfuTrackKind, CancellationToken, Task> _closeAsync;
        private readonly Action _disposeAction;

        public MediaSfuDelegateTransportBackedLocalMediaAdapter(
            Func<string, CancellationToken, Task> ensureCapabilitiesLoadedAsync,
            Func<MediaSfuWebRtcTransport, CancellationToken, Task> initializeSendTransportAsync,
            Func<CancellationToken, Task<string>> createSendTransportConnectParametersJsonAsync,
            Func<bool, string, CancellationToken, Task> completeSendTransportConnectAsync,
            Func<MediaSfuTrackKind, MediaSfuLocalMediaRequest, CancellationToken, Task<MediaSfuProduceRequest>> createProduceRequestAsync,
            Func<MediaSfuTrackKind, string, CancellationToken, Task> bindProducerAsync,
            Func<MediaSfuTrackKind, CancellationToken, Task> pauseAsync,
            Func<MediaSfuTrackKind, CancellationToken, Task> resumeAsync,
            Func<MediaSfuTrackKind, CancellationToken, Task> closeAsync,
            Action disposeAction = null)
        {
            _ensureCapabilitiesLoadedAsync = ensureCapabilitiesLoadedAsync ?? throw new ArgumentNullException(nameof(ensureCapabilitiesLoadedAsync));
            _initializeSendTransportAsync = initializeSendTransportAsync ?? throw new ArgumentNullException(nameof(initializeSendTransportAsync));
            _createSendTransportConnectParametersJsonAsync = createSendTransportConnectParametersJsonAsync ?? throw new ArgumentNullException(nameof(createSendTransportConnectParametersJsonAsync));
            _completeSendTransportConnectAsync = completeSendTransportConnectAsync ?? throw new ArgumentNullException(nameof(completeSendTransportConnectAsync));
            _createProduceRequestAsync = createProduceRequestAsync ?? throw new ArgumentNullException(nameof(createProduceRequestAsync));
            _bindProducerAsync = bindProducerAsync ?? throw new ArgumentNullException(nameof(bindProducerAsync));
            _pauseAsync = pauseAsync ?? throw new ArgumentNullException(nameof(pauseAsync));
            _resumeAsync = resumeAsync ?? throw new ArgumentNullException(nameof(resumeAsync));
            _closeAsync = closeAsync ?? throw new ArgumentNullException(nameof(closeAsync));
            _disposeAction = disposeAction;
        }

        public Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default)
        {
            return _ensureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson, cancellationToken);
        }

        public Task InitializeSendTransportAsync(
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default)
        {
            return _initializeSendTransportAsync(transport, cancellationToken);
        }

        public Task<string> CreateSendTransportConnectParametersJsonAsync(
            CancellationToken cancellationToken = default)
        {
            return _createSendTransportConnectParametersJsonAsync(cancellationToken);
        }

        public Task CompleteSendTransportConnectAsync(
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default)
        {
            return _completeSendTransportConnectAsync(success, errorDetail ?? string.Empty, cancellationToken);
        }

        public Task<MediaSfuProduceRequest> CreateProduceRequestAsync(
            MediaSfuTrackKind kind,
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            return _createProduceRequestAsync(kind, request, cancellationToken);
        }

        public Task BindProducerAsync(
            MediaSfuTrackKind kind,
            string producerId,
            CancellationToken cancellationToken = default)
        {
            return _bindProducerAsync(kind, producerId, cancellationToken);
        }

        public Task PauseAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            return _pauseAsync(kind, cancellationToken);
        }

        public Task ResumeAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            return _resumeAsync(kind, cancellationToken);
        }

        public Task CloseAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            return _closeAsync(kind, cancellationToken);
        }

        public void Dispose()
        {
            _disposeAction?.Invoke();
        }
    }
}