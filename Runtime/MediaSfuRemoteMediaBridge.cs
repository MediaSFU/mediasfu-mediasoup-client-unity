using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSFU.MediasoupClientUnity.Contracts;

namespace MediaSFU.Unity
{
    public sealed class MediaSfuRemoteMediaRequest
    {
        public MediaSfuRemoteProducerEvent RemoteProducer { get; set; }

        public MediaSfuRoom Room { get; set; }

        public MediaSfuRoomValidation RoomValidation { get; set; }

        public Func<MediaSfuWebRtcTransportRequest, CancellationToken, Task<MediaSfuOperationResult<MediaSfuWebRtcTransport>>> CreateWebRtcTransportAsync { get; set; }

        public Func<MediaSfuTransportConnectRequest, CancellationToken, Task<MediaSfuOperationResult<bool>>> ConnectWebRtcTransportAsync { get; set; }

        public Func<MediaSfuConsumeRequest, CancellationToken, Task<MediaSfuOperationResult<MediaSfuConsumeResponse>>> ConsumeTrackAsync { get; set; }

        public Func<string, CancellationToken, Task<MediaSfuOperationResult<bool>>> ResumeConsumerAsync { get; set; }

        public Func<string, CancellationToken, Task<MediaSfuOperationResult<bool>>> PauseConsumerAsync { get; set; }
    }

    public interface IMediaSfuRemoteMediaBridge : IDisposable
    {
        Task<MediaSfuOperationResult<bool>> OnRemoteProducerAvailableAsync(
            MediaSfuRemoteMediaRequest request,
            CancellationToken cancellationToken = default);

        Task<MediaSfuOperationResult<bool>> OnRemoteProducerClosedAsync(
            MediaSfuRemoteProducerClosedEvent remoteProducerClosed,
            CancellationToken cancellationToken = default);
    }

    public interface IMediaSfuTransportBackedRemoteMediaAdapter : IDisposable
    {
        Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default);

        Task<string> GetLoadedDeviceRtpCapabilitiesJsonAsync(
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

    public sealed class MediaSfuDelegateTransportBackedRemoteMediaAdapter : IMediaSfuTransportBackedRemoteMediaAdapter
    {
        private readonly Func<string, CancellationToken, Task> _ensureCapabilitiesLoadedAsync;
        private readonly Func<CancellationToken, Task<string>> _getLoadedDeviceRtpCapabilitiesJsonAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, MediaSfuWebRtcTransport, CancellationToken, Task> _initializeReceiveTransportAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, CancellationToken, Task<string>> _createReceiveTransportConnectParametersJsonAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, bool, string, CancellationToken, Task> _completeReceiveTransportConnectAsync;
        private readonly Func<MediaSfuRemoteProducerEvent, MediaSfuConsumeResponse, CancellationToken, Task> _bindConsumerAsync;
        private readonly Func<string, CancellationToken, Task> _closeConsumerAsync;
        private readonly Action _disposeAction;

        public MediaSfuDelegateTransportBackedRemoteMediaAdapter(
            Func<string, CancellationToken, Task> ensureCapabilitiesLoadedAsync,
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
                    "This MediaSFU remote media adapter does not expose loaded device RTP capabilities.");
            }

            return _getLoadedDeviceRtpCapabilitiesJsonAsync(cancellationToken);
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

    public sealed class MediaSfuTransportBackedRemoteMediaBridge : IMediaSfuRemoteMediaBridge
    {
        private readonly IMediaSfuTransportBackedRemoteMediaAdapter _adapter;
        private readonly Dictionary<string, RemoteMediaRuntimeState> _runtimeByProducerId =
            new Dictionary<string, RemoteMediaRuntimeState>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);

        private bool _disposed;

        public MediaSfuTransportBackedRemoteMediaBridge(IMediaSfuTransportBackedRemoteMediaAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public async Task<MediaSfuOperationResult<bool>> OnRemoteProducerAvailableAsync(
            MediaSfuRemoteMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            var validationResult = ValidateRemoteProducerRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            MediaSfuIceRelayFallback receiveRelayFallback = null;
            try
            {
                var roomRtpCapabilitiesJson = request.RoomValidation?.RtpCapabilitiesJson;
                if (string.IsNullOrWhiteSpace(roomRtpCapabilitiesJson))
                {
                    return MediaSfuOperationResult<bool>.FromFailure(
                        "CreateReceiveTransport failed: room validation does not include RTP capabilities JSON.");
                }

                await _adapter.EnsureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson, cancellationToken)
                    .ConfigureAwait(false);

                var deviceRtpCapabilitiesJson = await _adapter.GetLoadedDeviceRtpCapabilitiesJsonAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(deviceRtpCapabilitiesJson))
                {
                    return MediaSfuOperationResult<bool>.FromFailure(
                        "ConsumeTrack failed: device RTP capabilities JSON is unavailable after loading router capabilities.");
                }

                var producerId = request.RemoteProducer.ProducerId;
                if (_runtimeByProducerId.ContainsKey(producerId))
                {
                    return MediaSfuOperationResult<bool>.FromSuccess(true);
                }

                var createTransportResult = await request.CreateWebRtcTransportAsync(
                        new MediaSfuWebRtcTransportRequest
                        {
                            Direction = MediaSfuTransportDirection.Receive,
                            IsLevel = request.RemoteProducer.IsLevel
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!createTransportResult.Success || createTransportResult.Value == null)
                {
                    return ConvertOperationResult(createTransportResult, "CreateReceiveTransport");
                }

                await _adapter.InitializeReceiveTransportAsync(
                        request.RemoteProducer,
                        createTransportResult.Value,
                        cancellationToken)
                    .ConfigureAwait(false);

                // turn-relay-v1: relay restart if ICE stays stuck. This SDK has no other ICE recovery to run beside it.
                receiveRelayFallback = MediaSfuIceRelayFallback.Attach(
                    _adapter,
                    createTransportResult.Value,
                    MediaSfuTransportDirection.Receive,
                    () => !_disposed);

                var consumeResult = await request.ConsumeTrackAsync(
                        new MediaSfuConsumeRequest
                        {
                            RemoteProducerId = producerId,
                            ServerConsumerTransportId = createTransportResult.Value.Id,
                            RtpCapabilitiesJson = deviceRtpCapabilitiesJson
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!consumeResult.Success || consumeResult.Value == null)
                {
                    return ConvertOperationResult(consumeResult, "ConsumeTrack");
                }

                // mediasoup receive transports emit onConnect from consume(), not from transport creation.
                var pendingBindConsumerTask = _adapter.BindConsumerAsync(
                    request.RemoteProducer,
                    consumeResult.Value,
                    cancellationToken);

                var dtlsParametersJson = await _adapter.CreateReceiveTransportConnectParametersJsonAsync(
                        request.RemoteProducer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(dtlsParametersJson))
                {
                    ObservePendingTaskFailure(pendingBindConsumerTask);
                    return MediaSfuOperationResult<bool>.FromFailure(
                        "ConnectReceiveTransport failed: adapter returned no DTLS parameters JSON.");
                }

                // The transport's connect step: ICE starts now even if no connecting state shows yet.
                receiveRelayFallback?.NotifyTransportConnect();

                MediaSfuOperationResult<bool> connectResult;
                try
                {
                    connectResult = await request.ConnectWebRtcTransportAsync(
                            new MediaSfuTransportConnectRequest
                            {
                                Direction = MediaSfuTransportDirection.Receive,
                                TransportId = createTransportResult.Value.Id,
                                DtlsParametersJson = dtlsParametersJson
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    ObservePendingTaskFailure(pendingBindConsumerTask);
                    var completionFailure = await TryCompleteReceiveTransportConnectAsync(
                            request.RemoteProducer,
                            success: false,
                            errorDetail: error.Message,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (completionFailure != null)
                    {
                        return completionFailure;
                    }

                    return MediaSfuOperationResult<bool>.FromFailure(
                        $"ConnectReceiveTransport failed: {error.Message}",
                        error.ToString());
                }

                if (!connectResult.Success)
                {
                    ObservePendingTaskFailure(pendingBindConsumerTask);
                    var completionFailure = await TryCompleteReceiveTransportConnectAsync(
                            request.RemoteProducer,
                            success: false,
                            errorDetail: BuildOperationFailureDetail(connectResult, "ConnectReceiveTransport"),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (completionFailure != null)
                    {
                        return completionFailure;
                    }

                    return ConvertOperationResult(connectResult, "ConnectReceiveTransport");
                }

                var connectCompletionFailure = await TryCompleteReceiveTransportConnectAsync(
                        request.RemoteProducer,
                        success: true,
                        errorDetail: string.Empty,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (connectCompletionFailure != null)
                {
                    ObservePendingTaskFailure(pendingBindConsumerTask);
                    return connectCompletionFailure;
                }

                await pendingBindConsumerTask.ConfigureAwait(false);

                var serverConsumerId = string.IsNullOrWhiteSpace(consumeResult.Value.ServerConsumerId)
                    ? consumeResult.Value.ConsumerId
                    : consumeResult.Value.ServerConsumerId;
                if (string.IsNullOrWhiteSpace(serverConsumerId))
                {
                    return MediaSfuOperationResult<bool>.FromFailure(
                        "ResumeConsumer failed: consume acknowledgment returned no server consumer id.");
                }

                var resumeResult = await request.ResumeConsumerAsync(serverConsumerId, cancellationToken)
                    .ConfigureAwait(false);
                if (!resumeResult.Success)
                {
                    return ConvertOperationResult(resumeResult, "ResumeConsumer");
                }

                _runtimeByProducerId[producerId] = new RemoteMediaRuntimeState
                {
                    TransportId = createTransportResult.Value.Id,
                    ConsumerId = consumeResult.Value.ConsumerId,
                    ServerConsumerId = serverConsumerId,
                    RelayFallback = receiveRelayFallback
                };
                receiveRelayFallback = null;

                return MediaSfuOperationResult<bool>.FromSuccess(true);
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"OnRemoteProducerAvailable failed: {error.Message}",
                    error.ToString());
            }
            finally
            {
                // A transport that never became a consumer is not watched any longer.
                receiveRelayFallback?.Dispose();
                _operationLock.Release();
            }
        }

        public async Task<MediaSfuOperationResult<bool>> OnRemoteProducerClosedAsync(
            MediaSfuRemoteProducerClosedEvent remoteProducerClosed,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            if (remoteProducerClosed == null || string.IsNullOrWhiteSpace(remoteProducerClosed.RemoteProducerId))
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "OnRemoteProducerClosed requires a non-null close event with a remote producer id.");
            }

            await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_runtimeByProducerId.TryGetValue(remoteProducerClosed.RemoteProducerId, out var closedRuntime))
                {
                    return MediaSfuOperationResult<bool>.FromSuccess(true);
                }

                _runtimeByProducerId.Remove(remoteProducerClosed.RemoteProducerId);
                closedRuntime.RelayFallback?.Dispose();

                try
                {
                    await _adapter.CloseConsumerAsync(remoteProducerClosed.RemoteProducerId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    Console.WriteLine(
                        "MediaSfu remote consumer close skipped producerId=" +
                        remoteProducerClosed.RemoteProducerId +
                        " reason=" + error.Message);
                }

                return MediaSfuOperationResult<bool>.FromSuccess(true);
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"OnRemoteProducerClosed failed: {error.Message}",
                    error.ToString());
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var runtime in _runtimeByProducerId.Values)
            {
                runtime.RelayFallback?.Dispose();
            }

            _adapter.Dispose();
            _operationLock.Dispose();
            _runtimeByProducerId.Clear();
        }

        private static MediaSfuOperationResult<bool> ValidateRemoteProducerRequest(MediaSfuRemoteMediaRequest request)
        {
            if (request == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires a non-null request.");
            }

            if (request.RemoteProducer == null || string.IsNullOrWhiteSpace(request.RemoteProducer.ProducerId))
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires a remote producer event with a producer id.");
            }

            if (request.CreateWebRtcTransportAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires CreateWebRtcTransportAsync support.");
            }

            if (request.ConnectWebRtcTransportAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires ConnectWebRtcTransportAsync support.");
            }

            if (request.ConsumeTrackAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires ConsumeTrackAsync support.");
            }

            if (request.ResumeConsumerAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires ResumeConsumerAsync support.");
            }

            if (request.RoomValidation == null || string.IsNullOrWhiteSpace(request.RoomValidation.RtpCapabilitiesJson))
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Remote media bridge requires room RTP capabilities from ConnectMediaAsync validation.");
            }

            return null;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuTransportBackedRemoteMediaBridge));
            }
        }

        private async Task<MediaSfuOperationResult<bool>> TryCompleteReceiveTransportConnectAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            bool success,
            string errorDetail,
            CancellationToken cancellationToken)
        {
            try
            {
                await _adapter.CompleteReceiveTransportConnectAsync(
                        remoteProducer,
                        success,
                        errorDetail ?? string.Empty,
                        cancellationToken)
                    .ConfigureAwait(false);
                return null;
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"ConnectReceiveTransport finalize failed: {error.Message}",
                    error.ToString());
            }
        }

        private static MediaSfuOperationResult<bool> ConvertOperationResult<T>(
            MediaSfuOperationResult<T> result,
            string fallbackOperationName)
        {
            if (result == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"{fallbackOperationName} failed: operation returned no result.");
            }

            return result.Status switch
            {
                MediaSfuOperationStatus.Deferred => MediaSfuOperationResult<bool>.FromDeferredContract(result.Detail),
                MediaSfuOperationStatus.Failure => MediaSfuOperationResult<bool>.FromFailure(result.Error, result.Detail),
                _ => MediaSfuOperationResult<bool>.FromSuccess(true)
            };
        }

        private static string BuildOperationFailureDetail<T>(
            MediaSfuOperationResult<T> result,
            string fallbackOperationName)
        {
            if (result == null)
            {
                return $"{fallbackOperationName} failed: operation returned no result.";
            }

            if (!string.IsNullOrWhiteSpace(result.Detail))
            {
                return result.Detail;
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                return result.Error;
            }

            return $"{fallbackOperationName} failed.";
        }

        private static void ObservePendingTaskFailure(Task pendingTask)
        {
            if (pendingTask == null)
            {
                return;
            }

            _ = ObservePendingTaskFailureCoreAsync(pendingTask);
        }

        private static async Task ObservePendingTaskFailureCoreAsync(Task pendingTask)
        {
            try
            {
                await pendingTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private sealed class RemoteMediaRuntimeState
        {
            public string TransportId { get; set; } = string.Empty;

            public string ConsumerId { get; set; } = string.Empty;

            public string ServerConsumerId { get; set; } = string.Empty;

            public MediaSfuIceRelayFallback RelayFallback { get; set; }
        }
    }
}
