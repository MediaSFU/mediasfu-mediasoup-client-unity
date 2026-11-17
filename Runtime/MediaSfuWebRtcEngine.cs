using System;
using System.Threading;
using System.Threading.Tasks;
using MediaSFU.MediasoupClientUnity.Contracts;

namespace MediaSFU.Unity
{
    public interface IMediaSfuWebRtcEngine : IDisposable
    {
        Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default);

        Task<string> GetLoadedDeviceRtpCapabilitiesJsonAsync(
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

        Task PauseProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default);

        Task ResumeProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default);

        Task CloseProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default);

        Task InitializeReceiveTransportAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default);

        Task<string> CreateReceiveTransportConnectParametersJsonAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            CancellationToken cancellationToken = default);

        Task CompleteReceiveTransportConnectAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default);

        Task BindConsumerAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuConsumeResponse response,
            CancellationToken cancellationToken = default);

        Task CloseConsumerAsync(
            string remoteProducerId,
            CancellationToken cancellationToken = default);
    }

    public sealed class MediaSfuDelegateWebRtcEngine : IMediaSfuWebRtcEngine
    {
        private readonly Func<string, CancellationToken, Task> _ensureCapabilitiesLoadedAsync;
        private readonly Func<CancellationToken, Task<string>> _getLoadedDeviceRtpCapabilitiesJsonAsync;
        private readonly Func<MediaSfuWebRtcTransport, CancellationToken, Task> _initializeSendTransportAsync;
        private readonly Func<CancellationToken, Task<string>> _createSendTransportConnectParametersJsonAsync;
        private readonly Func<bool, string, CancellationToken, Task> _completeSendTransportConnectAsync;
        private readonly Func<MediaSfuTrackKind, MediaSfuLocalMediaRequest, CancellationToken, Task<MediaSfuProduceRequest>> _createProduceRequestAsync;
        private readonly Func<MediaSfuTrackKind, string, CancellationToken, Task> _bindProducerAsync;
        private readonly Func<MediaSfuTrackKind, CancellationToken, Task> _pauseProducerAsync;
        private readonly Func<MediaSfuTrackKind, CancellationToken, Task> _resumeProducerAsync;
        private readonly Func<MediaSfuTrackKind, CancellationToken, Task> _closeProducerAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, MediaSfuWebRtcTransport, CancellationToken, Task> _initializeReceiveTransportAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, CancellationToken, Task<string>> _createReceiveTransportConnectParametersJsonAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, bool, string, CancellationToken, Task> _completeReceiveTransportConnectAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, MediaSfuConsumeResponse, CancellationToken, Task> _bindConsumerAsync;
        private readonly Func<string, CancellationToken, Task> _closeConsumerAsync;
        private readonly Action _disposeAction;

        public MediaSfuDelegateWebRtcEngine(
            Func<string, CancellationToken, Task> ensureCapabilitiesLoadedAsync,
            Func<MediaSfuWebRtcTransport, CancellationToken, Task> initializeSendTransportAsync,
            Func<CancellationToken, Task<string>> createSendTransportConnectParametersJsonAsync,
            Func<bool, string, CancellationToken, Task> completeSendTransportConnectAsync,
            Func<MediaSfuTrackKind, MediaSfuLocalMediaRequest, CancellationToken, Task<MediaSfuProduceRequest>> createProduceRequestAsync,
            Func<MediaSfuTrackKind, string, CancellationToken, Task> bindProducerAsync,
            Func<MediaSfuTrackKind, CancellationToken, Task> pauseProducerAsync,
            Func<MediaSfuTrackKind, CancellationToken, Task> resumeProducerAsync,
            Func<MediaSfuTrackKind, CancellationToken, Task> closeProducerAsync,
            Func<MediaSfuRemoteProducerEvent, MediaSfuWebRtcTransport, CancellationToken, Task> initializeReceiveTransportAsync,
            Func<MediaSfuRemoteProducerEvent, CancellationToken, Task<string>> createReceiveTransportConnectParametersJsonAsync,
            Func<MediaSfuRemoteProducerEvent, bool, string, CancellationToken, Task> completeReceiveTransportConnectAsync,
            Func<MediaSfuRemoteProducerEvent, MediaSfuConsumeResponse, CancellationToken, Task> bindConsumerAsync,
            Func<string, CancellationToken, Task> closeConsumerAsync,
            Action disposeAction = null,
            Func<CancellationToken, Task<string>> getLoadedDeviceRtpCapabilitiesJsonAsync = null)
        {
            _ensureCapabilitiesLoadedAsync = ensureCapabilitiesLoadedAsync ?? throw new ArgumentNullException(nameof(ensureCapabilitiesLoadedAsync));
            _getLoadedDeviceRtpCapabilitiesJsonAsync = getLoadedDeviceRtpCapabilitiesJsonAsync;
            _initializeSendTransportAsync = initializeSendTransportAsync ?? throw new ArgumentNullException(nameof(initializeSendTransportAsync));
            _createSendTransportConnectParametersJsonAsync = createSendTransportConnectParametersJsonAsync ?? throw new ArgumentNullException(nameof(createSendTransportConnectParametersJsonAsync));
            _completeSendTransportConnectAsync = completeSendTransportConnectAsync ?? throw new ArgumentNullException(nameof(completeSendTransportConnectAsync));
            _createProduceRequestAsync = createProduceRequestAsync ?? throw new ArgumentNullException(nameof(createProduceRequestAsync));
            _bindProducerAsync = bindProducerAsync ?? throw new ArgumentNullException(nameof(bindProducerAsync));
            _pauseProducerAsync = pauseProducerAsync ?? throw new ArgumentNullException(nameof(pauseProducerAsync));
            _resumeProducerAsync = resumeProducerAsync ?? throw new ArgumentNullException(nameof(resumeProducerAsync));
            _closeProducerAsync = closeProducerAsync ?? throw new ArgumentNullException(nameof(closeProducerAsync));
            _initializeReceiveTransportAsync = initializeReceiveTransportAsync ?? throw new ArgumentNullException(nameof(initializeReceiveTransportAsync));
            _createReceiveTransportConnectParametersJsonAsync = createReceiveTransportConnectParametersJsonAsync ?? throw new ArgumentNullException(nameof(createReceiveTransportConnectParametersJsonAsync));
            _completeReceiveTransportConnectAsync = completeReceiveTransportConnectAsync ?? throw new ArgumentNullException(nameof(completeReceiveTransportConnectAsync));
            _bindConsumerAsync = bindConsumerAsync ?? throw new ArgumentNullException(nameof(bindConsumerAsync));
            _closeConsumerAsync = closeConsumerAsync ?? throw new ArgumentNullException(nameof(closeConsumerAsync));
            _disposeAction = disposeAction;
        }

        public Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default)
        {
            return _ensureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson, cancellationToken);
        }

        public Task<string> GetLoadedDeviceRtpCapabilitiesJsonAsync(
            CancellationToken cancellationToken = default)
        {
            if (_getLoadedDeviceRtpCapabilitiesJsonAsync == null)
            {
                throw new NotSupportedException(
                    "This MediaSFU WebRTC engine does not expose loaded device RTP capabilities.");
            }

            return _getLoadedDeviceRtpCapabilitiesJsonAsync(cancellationToken);
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

        public Task PauseProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            return _pauseProducerAsync(kind, cancellationToken);
        }

        public Task ResumeProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            return _resumeProducerAsync(kind, cancellationToken);
        }

        public Task CloseProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            return _closeProducerAsync(kind, cancellationToken);
        }

        public Task InitializeReceiveTransportAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default)
        {
            return _initializeReceiveTransportAsync(remoteProducer, transport, cancellationToken);
        }

        public Task<string> CreateReceiveTransportConnectParametersJsonAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            CancellationToken cancellationToken = default)
        {
            return _createReceiveTransportConnectParametersJsonAsync(remoteProducer, cancellationToken);
        }

        public Task CompleteReceiveTransportConnectAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default)
        {
            return _completeReceiveTransportConnectAsync(
                remoteProducer,
                success,
                errorDetail ?? string.Empty,
                cancellationToken);
        }

        public Task BindConsumerAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuConsumeResponse response,
            CancellationToken cancellationToken = default)
        {
            return _bindConsumerAsync(remoteProducer, response, cancellationToken);
        }

        public Task CloseConsumerAsync(
            string remoteProducerId,
            CancellationToken cancellationToken = default)
        {
            return _closeConsumerAsync(remoteProducerId, cancellationToken);
        }

        public void Dispose()
        {
            _disposeAction?.Invoke();
        }
    }

    public sealed class MediaSfuWebRtcEngineLocalMediaAdapter : IMediaSfuTransportBackedLocalMediaAdapter, IMediaSfuIceRelayTransportControl
    {
        private readonly IMediaSfuWebRtcEngine _engine;
        private readonly bool _ownsEngine;
        private bool _disposed;

        public MediaSfuWebRtcEngineLocalMediaAdapter(
            IMediaSfuWebRtcEngine engine,
            bool ownsEngine = true)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _ownsEngine = ownsEngine;
        }

        public Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.EnsureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson, cancellationToken);
        }

        public Task<string> GetLoadedDeviceRtpCapabilitiesJsonAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.GetLoadedDeviceRtpCapabilitiesJsonAsync(cancellationToken);
        }

        public Task InitializeSendTransportAsync(
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.InitializeSendTransportAsync(transport, cancellationToken);
        }

        public Task<string> CreateSendTransportConnectParametersJsonAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CreateSendTransportConnectParametersJsonAsync(cancellationToken);
        }

        public Task CompleteSendTransportConnectAsync(
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CompleteSendTransportConnectAsync(success, errorDetail, cancellationToken);
        }

        public Task<MediaSfuProduceRequest> CreateProduceRequestAsync(
            MediaSfuTrackKind kind,
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CreateProduceRequestAsync(kind, request, cancellationToken);
        }

        public Task BindProducerAsync(
            MediaSfuTrackKind kind,
            string producerId,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.BindProducerAsync(kind, producerId, cancellationToken);
        }

        public Task PauseAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.PauseProducerAsync(kind, cancellationToken);
        }

        public Task ResumeAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.ResumeProducerAsync(kind, cancellationToken);
        }

        public Task CloseAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CloseProducerAsync(kind, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsEngine)
            {
                _engine.Dispose();
            }
        }

        public Task<MediaSfuTransportIceState> GetTransportIceStateAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            CancellationToken cancellationToken = default)
        {
            return _disposed || !(_engine is IMediaSfuIceRelayTransportControl control)
                ? Task.FromResult<MediaSfuTransportIceState>(null)
                : control.GetTransportIceStateAsync(direction, transportId, cancellationToken);
        }

        public Task UpdateIceServersAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            string iceServersJson,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return MediaSfuIceRelayEngineSupport.RequireControl(_engine)
                .UpdateIceServersAsync(direction, transportId, iceServersJson, cancellationToken);
        }

        public Task RestartIceAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            string iceParametersJson,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return MediaSfuIceRelayEngineSupport.RequireControl(_engine)
                .RestartIceAsync(direction, transportId, iceParametersJson, cancellationToken);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuWebRtcEngineLocalMediaAdapter));
            }
        }
    }

    public sealed class MediaSfuWebRtcEngineRemoteMediaAdapter : IMediaSfuTransportBackedRemoteMediaAdapter, IMediaSfuIceRelayTransportControl
    {
        private readonly IMediaSfuWebRtcEngine _engine;
        private readonly bool _ownsEngine;
        private bool _disposed;

        public MediaSfuWebRtcEngineRemoteMediaAdapter(
            IMediaSfuWebRtcEngine engine,
            bool ownsEngine = true)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _ownsEngine = ownsEngine;
        }

        public Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.EnsureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson, cancellationToken);
        }

        public Task<string> GetLoadedDeviceRtpCapabilitiesJsonAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.GetLoadedDeviceRtpCapabilitiesJsonAsync(cancellationToken);
        }

        public Task InitializeReceiveTransportAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.InitializeReceiveTransportAsync(remoteProducer, transport, cancellationToken);
        }

        public Task<string> CreateReceiveTransportConnectParametersJsonAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CreateReceiveTransportConnectParametersJsonAsync(remoteProducer, cancellationToken);
        }

        public Task CompleteReceiveTransportConnectAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CompleteReceiveTransportConnectAsync(remoteProducer, success, errorDetail, cancellationToken);
        }

        public Task BindConsumerAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuConsumeResponse response,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.BindConsumerAsync(remoteProducer, response, cancellationToken);
        }

        public Task CloseConsumerAsync(
            string remoteProducerId,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _engine.CloseConsumerAsync(remoteProducerId, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsEngine)
            {
                _engine.Dispose();
            }
        }

        public Task<MediaSfuTransportIceState> GetTransportIceStateAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            CancellationToken cancellationToken = default)
        {
            return _disposed || !(_engine is IMediaSfuIceRelayTransportControl control)
                ? Task.FromResult<MediaSfuTransportIceState>(null)
                : control.GetTransportIceStateAsync(direction, transportId, cancellationToken);
        }

        public Task UpdateIceServersAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            string iceServersJson,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return MediaSfuIceRelayEngineSupport.RequireControl(_engine)
                .UpdateIceServersAsync(direction, transportId, iceServersJson, cancellationToken);
        }

        public Task RestartIceAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            string iceParametersJson,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return MediaSfuIceRelayEngineSupport.RequireControl(_engine)
                .RestartIceAsync(direction, transportId, iceParametersJson, cancellationToken);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuWebRtcEngineRemoteMediaAdapter));
            }
        }
    }

    internal static class MediaSfuIceRelayEngineSupport
    {
        public static IMediaSfuIceRelayTransportControl RequireControl(IMediaSfuWebRtcEngine engine)
        {
            return engine as IMediaSfuIceRelayTransportControl ??
                   throw new NotSupportedException("This MediaSFU WebRTC engine cannot restart ICE with a relay.");
        }
    }

    public sealed class MediaSfuWebRtcEngineDevice : IMediaSfuWebRtcDevice
    {
        private readonly IMediaSfuWebRtcEngine _engine;
        private readonly MediaSfuTransportBackedWebRtcDevice _innerDevice;
        private bool _disposed;

        public MediaSfuWebRtcEngineDevice(IMediaSfuWebRtcEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _innerDevice = new MediaSfuTransportBackedWebRtcDevice(
                new MediaSfuWebRtcEngineLocalMediaAdapter(engine, ownsEngine: false),
                new MediaSfuWebRtcEngineRemoteMediaAdapter(engine, ownsEngine: false));
        }

        public Task<MediaSfuOperationResult<bool>> SetMicrophoneEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerDevice.SetMicrophoneEnabledAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> SetCameraEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerDevice.SetCameraEnabledAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> SetScreenShareEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerDevice.SetScreenShareEnabledAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> OnRemoteProducerAvailableAsync(
            MediaSfuRemoteMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerDevice.OnRemoteProducerAvailableAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> OnRemoteProducerClosedAsync(
            MediaSfuRemoteProducerClosedEvent remoteProducerClosed,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerDevice.OnRemoteProducerClosedAsync(remoteProducerClosed, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _innerDevice.Dispose();
            }
            finally
            {
                _engine.Dispose();
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuWebRtcEngineDevice));
            }
        }
    }
}
