using System;
using System.Threading;
using System.Threading.Tasks;
using MediaSFU.MediasoupClientUnity.Contracts;

namespace MediaSFU.Unity
{
    public interface IMediaSfuWebRtcDevice : IMediaSfuLocalMediaBackend, IMediaSfuRemoteMediaBridge
    {
    }

    public sealed class MediaSfuTransportBackedWebRtcDevice : IMediaSfuWebRtcDevice
    {
        private readonly MediaSfuTransportBackedLocalMediaBackend _localMediaBackend;
        private readonly MediaSfuTransportBackedRemoteMediaBridge _remoteMediaBridge;
        private bool _disposed;

        public MediaSfuTransportBackedWebRtcDevice(
            IMediaSfuTransportBackedLocalMediaAdapter localMediaAdapter,
            IMediaSfuTransportBackedRemoteMediaAdapter remoteMediaAdapter)
        {
            if (localMediaAdapter == null)
            {
                throw new ArgumentNullException(nameof(localMediaAdapter));
            }

            if (remoteMediaAdapter == null)
            {
                throw new ArgumentNullException(nameof(remoteMediaAdapter));
            }

            _localMediaBackend = new MediaSfuTransportBackedLocalMediaBackend(localMediaAdapter);
            _remoteMediaBridge = new MediaSfuTransportBackedRemoteMediaBridge(remoteMediaAdapter);
        }

        public Task<MediaSfuOperationResult<bool>> SetMicrophoneEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _localMediaBackend.SetMicrophoneEnabledAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> SetCameraEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _localMediaBackend.SetCameraEnabledAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> SetScreenShareEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _localMediaBackend.SetScreenShareEnabledAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> OnRemoteProducerAvailableAsync(
            MediaSfuRemoteMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _remoteMediaBridge.OnRemoteProducerAvailableAsync(request, cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> OnRemoteProducerClosedAsync(
            MediaSfuRemoteProducerClosedEvent remoteProducerClosed,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _remoteMediaBridge.OnRemoteProducerClosedAsync(remoteProducerClosed, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _localMediaBackend.Dispose();
            _remoteMediaBridge.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuTransportBackedWebRtcDevice));
            }
        }
    }
}