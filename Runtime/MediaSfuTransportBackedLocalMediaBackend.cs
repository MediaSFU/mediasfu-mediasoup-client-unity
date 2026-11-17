using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSFU.MediasoupClientUnity.Contracts;

namespace MediaSFU.Unity
{
    public sealed class MediaSfuTransportBackedLocalMediaBackend : IMediaSfuLocalMediaBackend, IDisposable
    {
        private readonly IMediaSfuTransportBackedLocalMediaAdapter _adapter;
        private readonly Dictionary<MediaSfuTrackKind, MediaSfuLocalMediaRuntimeState> _trackRuntime =
            new Dictionary<MediaSfuTrackKind, MediaSfuLocalMediaRuntimeState>();
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);

        private string _sendTransportId = string.Empty;
        private bool _sendTransportConnected;
        private bool _disposed;
        private MediaSfuIceRelayFallback _sendRelayFallback;

        public MediaSfuTransportBackedLocalMediaBackend(IMediaSfuTransportBackedLocalMediaAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public Task<MediaSfuOperationResult<bool>> SetMicrophoneEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            return SetTrackEnabledAsync(
                request,
                mediaTag: "audio",
                pauseOnDisable: true,
                emitResumeSignalOnResume: true,
                emitCloseScreenSignalOnDisable: false,
                cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> SetCameraEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            return SetTrackEnabledAsync(
                request,
                mediaTag: "video",
                pauseOnDisable: false,
                emitResumeSignalOnResume: false,
                emitCloseScreenSignalOnDisable: false,
                cancellationToken);
        }

        public Task<MediaSfuOperationResult<bool>> SetScreenShareEnabledAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            return SetTrackEnabledAsync(
                request,
                mediaTag: "screen",
                pauseOnDisable: false,
                emitResumeSignalOnResume: false,
                emitCloseScreenSignalOnDisable: true,
                cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sendRelayFallback?.Dispose();
            _sendRelayFallback = null;
            _adapter.Dispose();
            _operationLock.Dispose();
        }

        private async Task<MediaSfuOperationResult<bool>> SetTrackEnabledAsync(
            MediaSfuLocalMediaRequest request,
            string mediaTag,
            bool pauseOnDisable,
            bool emitResumeSignalOnResume,
            bool emitCloseScreenSignalOnDisable,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            var validationResult = ValidateRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return request.Enabled
                    ? await EnableTrackAsync(request, mediaTag, emitResumeSignalOnResume, cancellationToken).ConfigureAwait(false)
                    : await DisableTrackAsync(request, mediaTag, pauseOnDisable, emitCloseScreenSignalOnDisable, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"{GetOperationLabel(request.Kind, request.Enabled)} failed: {error.Message}",
                    error.ToString());
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task<MediaSfuOperationResult<bool>> EnableTrackAsync(
            MediaSfuLocalMediaRequest request,
            string mediaTag,
            bool emitResumeSignalOnResume,
            CancellationToken cancellationToken)
        {
            var runtimeState = GetOrCreateRuntimeState(request.Kind);
            if (runtimeState.TrackState == MediaSfuLocalMediaTrackState.Active)
            {
                Console.WriteLine(
                    $"MediaSfu local media enable skipped because {mediaTag} is already active. producerId={runtimeState.ProducerId}");
                return MediaSfuOperationResult<bool>.FromSuccess(true);
            }

            if (runtimeState.TrackState == MediaSfuLocalMediaTrackState.Paused)
            {
                Console.WriteLine($"MediaSfu local media resuming paused {mediaTag} producer.");
                await _adapter.ResumeAsync(request.Kind, cancellationToken).ConfigureAwait(false);
                if (emitResumeSignalOnResume)
                {
                    await EmitResumeProducerSignalAsync(request, cancellationToken).ConfigureAwait(false);
                }

                runtimeState.TrackState = MediaSfuLocalMediaTrackState.Active;
                return MediaSfuOperationResult<bool>.FromSuccess(true);
            }

            Console.WriteLine($"MediaSfu local media enabling {mediaTag}: initializing send transport.");
            var sendTransportResult = await EnsureSendTransportInitializedAsync(request, cancellationToken).ConfigureAwait(false);
            if (!sendTransportResult.Success)
            {
                Console.WriteLine($"MediaSfu local media {mediaTag} send transport init failed: {sendTransportResult.Error} {sendTransportResult.Detail}");
                return sendTransportResult;
            }

            Task<MediaSfuProduceRequest> pendingProduceRequestTask = null;
            if (!_sendTransportConnected)
            {
                // mediasoup send transports emit onConnect from produce(), not from transport creation.
                Console.WriteLine($"MediaSfu local media {mediaTag}: creating pending produce request before send transport connect.");
                pendingProduceRequestTask = _adapter.CreateProduceRequestAsync(request.Kind, request, cancellationToken);
                sendTransportResult = await EnsureSendTransportConnectedAsync(request, cancellationToken).ConfigureAwait(false);
                if (!sendTransportResult.Success)
                {
                    ObservePendingTaskFailure(pendingProduceRequestTask);
                    Console.WriteLine($"MediaSfu local media {mediaTag} send transport connect failed: {sendTransportResult.Error} {sendTransportResult.Detail}");
                    return sendTransportResult;
                }
            }

            var produceRequest = pendingProduceRequestTask != null
                ? await pendingProduceRequestTask.ConfigureAwait(false)
                : await _adapter.CreateProduceRequestAsync(request.Kind, request, cancellationToken)
                    .ConfigureAwait(false);
            if (produceRequest == null)
            {
                Console.WriteLine($"MediaSfu local media {mediaTag}: adapter returned no produce request.");
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"{GetOperationLabel(request.Kind, true)} failed: adapter returned no produce request.");
            }

            if (string.IsNullOrWhiteSpace(produceRequest.Name))
            {
                produceRequest.Name = request.LocalUserName;
            }

            if (string.IsNullOrWhiteSpace(produceRequest.IsLevel))
            {
                produceRequest.IsLevel = request.LocalIsLevel;
            }

            var produceResult = await request.ProduceTrackAsync(produceRequest, cancellationToken).ConfigureAwait(false);
            if (!produceResult.Success || produceResult.Value == null)
            {
                Console.WriteLine($"MediaSfu local media {mediaTag} produce failed: {produceResult.Error} {produceResult.Detail}");
                return ConvertOperationResult(produceResult, GetOperationLabel(request.Kind, true));
            }

            Console.WriteLine($"MediaSfu local media {mediaTag} produced server producerId={produceResult.Value.ProducerId}");
            await _adapter.BindProducerAsync(request.Kind, produceResult.Value.ProducerId, cancellationToken)
                .ConfigureAwait(false);
            runtimeState.TrackState = MediaSfuLocalMediaTrackState.Active;
            runtimeState.ProducerId = produceResult.Value.ProducerId;
            Console.WriteLine($"MediaSfu local media {mediaTag} bound producerId={runtimeState.ProducerId}");
            return MediaSfuOperationResult<bool>.FromSuccess(true);
        }

        private async Task<MediaSfuOperationResult<bool>> DisableTrackAsync(
            MediaSfuLocalMediaRequest request,
            string mediaTag,
            bool pauseOnDisable,
            bool emitCloseScreenSignalOnDisable,
            CancellationToken cancellationToken)
        {
            var runtimeState = GetOrCreateRuntimeState(request.Kind);
            if (runtimeState.TrackState == MediaSfuLocalMediaTrackState.Inactive)
            {
                return MediaSfuOperationResult<bool>.FromSuccess(true);
            }

            if (pauseOnDisable)
            {
                await _adapter.PauseAsync(request.Kind, cancellationToken).ConfigureAwait(false);
                runtimeState.TrackState = MediaSfuLocalMediaTrackState.Paused;
            }
            else
            {
                await _adapter.CloseAsync(request.Kind, cancellationToken).ConfigureAwait(false);
                runtimeState.TrackState = MediaSfuLocalMediaTrackState.Inactive;
                runtimeState.ProducerId = string.Empty;
            }

            if (emitCloseScreenSignalOnDisable)
            {
                await request.EmitSocketEventAsync(
                        "closeScreenProducer",
                        new Dictionary<string, object>(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await request.EmitSocketEventAsync(
                    "pauseProducerMedia",
                    BuildMediaControlPayload(request, mediaTag),
                    cancellationToken)
                .ConfigureAwait(false);

            return MediaSfuOperationResult<bool>.FromSuccess(true);
        }

        private async Task<MediaSfuOperationResult<bool>> EnsureSendTransportInitializedAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken)
        {
            var roomRtpCapabilitiesJson = request.RoomValidation?.RtpCapabilitiesJson;
            if (string.IsNullOrWhiteSpace(roomRtpCapabilitiesJson))
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "CreateSendTransport failed: room validation does not include RTP capabilities JSON.");
            }

            await _adapter.EnsureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(_sendTransportId))
            {
                var createResult = await request.CreateWebRtcTransportAsync(
                        new MediaSfuWebRtcTransportRequest
                        {
                            Direction = MediaSfuTransportDirection.Send,
                            IsLevel = request.LocalIsLevel
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!createResult.Success || createResult.Value == null)
                {
                    return ConvertOperationResult(createResult, "CreateSendTransport");
                }

                await _adapter.InitializeSendTransportAsync(createResult.Value, cancellationToken).ConfigureAwait(false);
                _sendTransportId = createResult.Value.Id;
                _sendTransportConnected = false;

                // turn-relay-v1: relay restart if ICE stays stuck. This SDK has no other ICE recovery to run beside it.
                var sendTransportId = _sendTransportId;
                _sendRelayFallback?.Dispose();
                _sendRelayFallback = MediaSfuIceRelayFallback.Attach(
                    _adapter,
                    createResult.Value,
                    MediaSfuTransportDirection.Send,
                    () => !_disposed && string.Equals(_sendTransportId, sendTransportId, StringComparison.Ordinal));
                Console.WriteLine($"MediaSfu local media send transport initialized id={_sendTransportId}");
            }

            return MediaSfuOperationResult<bool>.FromSuccess(true);
        }

        private async Task<MediaSfuOperationResult<bool>> EnsureSendTransportConnectedAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_sendTransportId))
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "ConnectSendTransport failed: send transport was not initialized before connect was requested.");
            }

            if (_sendTransportConnected)
            {
                return MediaSfuOperationResult<bool>.FromSuccess(true);
            }

            var dtlsParametersJson = await _adapter.CreateSendTransportConnectParametersJsonAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(dtlsParametersJson))
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "ConnectSendTransport failed: adapter returned no DTLS parameters JSON.");
            }

            // The transport's connect step: ICE starts now even if no connecting state shows yet.
            _sendRelayFallback?.NotifyTransportConnect();

            MediaSfuOperationResult<bool> connectResult;
            try
            {
                connectResult = await request.ConnectWebRtcTransportAsync(
                        new MediaSfuTransportConnectRequest
                        {
                            Direction = MediaSfuTransportDirection.Send,
                            TransportId = _sendTransportId,
                            DtlsParametersJson = dtlsParametersJson
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception error)
            {
                var completionFailure = await TryCompleteSendTransportConnectAsync(
                        success: false,
                        errorDetail: error.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (completionFailure != null)
                {
                    return completionFailure;
                }

                return MediaSfuOperationResult<bool>.FromFailure(
                    $"ConnectSendTransport failed: {error.Message}",
                    error.ToString());
            }

            if (!connectResult.Success)
            {
                var completionFailure = await TryCompleteSendTransportConnectAsync(
                        success: false,
                        errorDetail: BuildOperationFailureDetail(connectResult, "ConnectSendTransport"),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (completionFailure != null)
                {
                    return completionFailure;
                }

                return ConvertOperationResult(connectResult, "ConnectSendTransport");
            }

            var connectCompletionFailure = await TryCompleteSendTransportConnectAsync(
                    success: true,
                    errorDetail: string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            if (connectCompletionFailure != null)
            {
                return connectCompletionFailure;
            }

            _sendTransportConnected = true;
            Console.WriteLine($"MediaSfu local media send transport connected id={_sendTransportId}");
            return MediaSfuOperationResult<bool>.FromSuccess(true);
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

        private async Task<MediaSfuOperationResult<bool>> TryCompleteSendTransportConnectAsync(
            bool success,
            string errorDetail,
            CancellationToken cancellationToken)
        {
            try
            {
                await _adapter.CompleteSendTransportConnectAsync(
                        success,
                        errorDetail ?? string.Empty,
                        cancellationToken)
                    .ConfigureAwait(false);
                return null;
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    $"ConnectSendTransport finalize failed: {error.Message}",
                    error.ToString());
            }
        }

        private static MediaSfuOperationResult<bool> ValidateRequest(MediaSfuLocalMediaRequest request)
        {
            if (request == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Local media backend requires a non-null request.");
            }

            if (request.CreateWebRtcTransportAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Local media backend requires CreateWebRtcTransportAsync support.");
            }

            if (request.ConnectWebRtcTransportAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Local media backend requires ConnectWebRtcTransportAsync support.");
            }

            if (request.ProduceTrackAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Local media backend requires ProduceTrackAsync support.");
            }

            if (request.EmitSocketEventAsync == null)
            {
                return MediaSfuOperationResult<bool>.FromFailure(
                    "Local media backend requires EmitSocketEventAsync support.");
            }

            return null;
        }

        private async Task EmitResumeProducerSignalAsync(
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken)
        {
            await request.EmitSocketEventAsync(
                    "resumeProducerAudio",
                    BuildMediaControlPayload(request, "audio"),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static Dictionary<string, object> BuildMediaControlPayload(
            MediaSfuLocalMediaRequest request,
            string mediaTag)
        {
            return new Dictionary<string, object>
            {
                ["mediaTag"] = mediaTag,
                ["roomName"] = request.Room?.RoomName ?? string.Empty
            };
        }

        private MediaSfuLocalMediaRuntimeState GetOrCreateRuntimeState(MediaSfuTrackKind kind)
        {
            if (!_trackRuntime.TryGetValue(kind, out var runtimeState))
            {
                runtimeState = new MediaSfuLocalMediaRuntimeState();
                _trackRuntime[kind] = runtimeState;
            }

            return runtimeState;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuTransportBackedLocalMediaBackend));
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

        private static string GetOperationLabel(MediaSfuTrackKind kind, bool enabled)
        {
            return kind switch
            {
                MediaSfuTrackKind.Audio => enabled ? "EnableMicrophone" : "DisableMicrophone",
                MediaSfuTrackKind.Video => enabled ? "EnableCamera" : "DisableCamera",
                MediaSfuTrackKind.Screen => enabled ? "EnableScreenShare" : "DisableScreenShare",
                _ => enabled ? "EnableTrack" : "DisableTrack"
            };
        }
    }
}
