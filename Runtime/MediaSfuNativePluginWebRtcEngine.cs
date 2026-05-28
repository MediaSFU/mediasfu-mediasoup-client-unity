using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaSFU.MediasoupClientUnity.Contracts;
using MediaSFU.Unity.Json;

namespace MediaSFU.Unity
{
    public sealed class MediaSfuNativePluginWebRtcEngineOptions
    {
        public string ConfigurationJson { get; set; } = string.Empty;

        public bool EnableVerboseLogging { get; set; }

        public string IntegrationMode { get; set; } = string.Empty;

        public bool AllowMockBackend { get; set; }
    }

    public sealed class MediaSfuNativePluginBackendInfo
    {
        public string Description { get; set; } = string.Empty;

        public string BackendKind { get; set; } = string.Empty;

        public bool IsPlaceholder { get; set; }

        public string Platform { get; set; } = string.Empty;

        public bool? SupportsAudio { get; set; }

        public bool? SupportsVideo { get; set; }

        public bool? SupportsScreenShare { get; set; }

        public bool? SupportsWhiteboard { get; set; }

        public bool? SupportsReceive { get; set; }
    }

    public sealed class MediaSfuNativePluginRemoteVideoFrameInfo
    {
        public bool Available { get; set; }

        public long Sequence { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Rotation { get; set; }

        public long TimestampNs { get; set; }

        public int ByteCount { get; set; }

        public string ResolvedBy { get; set; } = string.Empty;

        public string RemoteProducerId { get; set; } = string.Empty;

        public string ConsumerId { get; set; } = string.Empty;

        public string TrackId { get; set; } = string.Empty;

        public string ConsumerKind { get; set; } = string.Empty;

        public bool RendererAttached { get; set; }

        public bool ConsumerPaused { get; set; }

        public string ConnectionState { get; set; } = string.Empty;

        public string ConsumerStatsJson { get; set; } = string.Empty;
    }

    public sealed class MediaSfuNativePluginWebRtcEngine : IMediaSfuWebRtcEngine
    {
        private readonly IntPtr _engineHandle;
        private readonly JsonSerializerOptions _jsonOptions;
        private MediaSfuNativePluginBackendInfo _cachedBackendInfo;
        private bool _disposed;

        public static MediaSfuOperationResult<IMediaSfuWebRtcDevice> TryCreateWebRtcDevice(
            MediaSfuNativePluginWebRtcEngineOptions options = null)
        {
            MediaSfuNativePluginWebRtcEngine engine = null;

            try
            {
                engine = new MediaSfuNativePluginWebRtcEngine(options);
                var backendInfo = engine.GetBackendInfo();
                if (backendInfo.IsPlaceholder)
                {
                    return MediaSfuOperationResult<IMediaSfuWebRtcDevice>.FromFailure(
                        "Native plugin WebRTC engine is using the placeholder backend.",
                        BuildPlaceholderBackendDetail(backendInfo));
                }

                if (!ShouldAllowMockBackend(options) && IsMockBackend(backendInfo))
                {
                    return MediaSfuOperationResult<IMediaSfuWebRtcDevice>.FromFailure(
                        "Native plugin WebRTC engine is using a mock backend.",
                        BuildMockBackendDetail(backendInfo));
                }

                var device = new MediaSfuWebRtcEngineDevice(engine);
                engine = null;
                return MediaSfuOperationResult<IMediaSfuWebRtcDevice>.FromSuccess(
                    device,
                    BuildAttachDetail(backendInfo));
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<IMediaSfuWebRtcDevice>.FromFailure(
                    "Native plugin WebRTC engine is unavailable.",
                    BuildTryCreateFailureDetail(error));
            }
            finally
            {
                engine?.Dispose();
            }
        }

        public static MediaSfuOperationResult<MediaSfuNativePluginWebRtcEngine> TryCreateEngine(
            MediaSfuNativePluginWebRtcEngineOptions options = null)
        {
            MediaSfuNativePluginWebRtcEngine engine = null;

            try
            {
                engine = new MediaSfuNativePluginWebRtcEngine(options);
                var backendInfo = engine.GetBackendInfo();
                if (backendInfo.IsPlaceholder)
                {
                    return MediaSfuOperationResult<MediaSfuNativePluginWebRtcEngine>.FromFailure(
                        "Native plugin WebRTC engine is using the placeholder backend.",
                        BuildPlaceholderBackendDetail(backendInfo));
                }

                if (!ShouldAllowMockBackend(options) && IsMockBackend(backendInfo))
                {
                    return MediaSfuOperationResult<MediaSfuNativePluginWebRtcEngine>.FromFailure(
                        "Native plugin WebRTC engine is using a mock backend.",
                        BuildMockBackendDetail(backendInfo));
                }

                var resultEngine = engine;
                engine = null;
                return MediaSfuOperationResult<MediaSfuNativePluginWebRtcEngine>.FromSuccess(
                    resultEngine,
                    BuildAttachDetail(backendInfo));
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<MediaSfuNativePluginWebRtcEngine>.FromFailure(
                    "Native plugin WebRTC engine is unavailable.",
                    BuildTryCreateFailureDetail(error));
            }
            finally
            {
                engine?.Dispose();
            }
        }

        public static MediaSfuOperationResult<MediaSfuNativePluginBackendInfo> TryGetBackendInfo(
            MediaSfuNativePluginWebRtcEngineOptions options = null)
        {
            MediaSfuNativePluginWebRtcEngine engine = null;

            try
            {
                engine = new MediaSfuNativePluginWebRtcEngine(options);
                return MediaSfuOperationResult<MediaSfuNativePluginBackendInfo>.FromSuccess(
                    engine.GetBackendInfo(),
                    "Native plugin backend information resolved.");
            }
            catch (Exception error)
            {
                return MediaSfuOperationResult<MediaSfuNativePluginBackendInfo>.FromFailure(
                    "Native plugin backend information is unavailable.",
                    BuildTryCreateFailureDetail(error));
            }
            finally
            {
                engine?.Dispose();
            }
        }

        public MediaSfuNativePluginWebRtcEngine(
            MediaSfuNativePluginWebRtcEngineOptions options = null)
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var payload = JsonSerializer.Serialize(
                new NativePluginCreateEnginePayload
                {
                    ConfigurationJson = options?.ConfigurationJson ?? string.Empty,
                    EnableVerboseLogging = options?.EnableVerboseLogging ?? false,
                    IntegrationMode = options?.IntegrationMode ?? string.Empty,
                    AllowMockBackend = options?.AllowMockBackend ?? false
                },
                _jsonOptions);

            try
            {
                _engineHandle = NativePluginInterop.CreateEngine(payload);
            }
            catch (Exception error) when (IsNativeInteropException(error))
            {
                throw BuildInteropUnavailableException("create the native WebRTC engine", error);
            }

            if (_engineHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "MediaSFU native plugin returned a null engine handle. Ensure the native bridge is installed and exported correctly.");
            }
        }

        public Task InitializeSendTransportAsync(
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.InitializeSendTransport,
                new NativePluginSendTransportPayload
                {
                    Transport = transport ?? throw new ArgumentNullException(nameof(transport))
                });

            return Task.CompletedTask;
        }

        public Task EnsureCapabilitiesLoadedAsync(
            string roomRtpCapabilitiesJson,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(roomRtpCapabilitiesJson))
            {
                throw new InvalidOperationException(
                    "MediaSFU native plugin requires a non-empty router RTP capabilities JSON payload before transport creation.");
            }

            InvokeVoidOperation(
                NativePluginOperationNames.LoadDeviceRtpCapabilities,
                new NativePluginLoadDeviceRtpCapabilitiesPayload
                {
                    RoomRtpCapabilitiesJson = roomRtpCapabilitiesJson
                });

            return Task.CompletedTask;
        }

        public Task<string> GetLoadedDeviceRtpCapabilitiesJsonAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            var response = InvokeOperation<NativePluginRtpCapabilitiesResponse>(
                NativePluginOperationNames.GetLoadedDeviceRtpCapabilities,
                new NativePluginEmptyPayload());

            return Task.FromResult(response?.RtpCapabilitiesJson ?? string.Empty);
        }

        public Task<string> CreateSendTransportConnectParametersJsonAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            var response = InvokeOperation<NativePluginDtlsParametersResponse>(
                NativePluginOperationNames.CreateSendTransportConnectParameters,
                new NativePluginEmptyPayload());

            return Task.FromResult(response?.DtlsParametersJson ?? string.Empty);
        }

        public MediaSfuNativePluginBackendInfo GetBackendInfo(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            if (_cachedBackendInfo != null)
            {
                return _cachedBackendInfo;
            }

            var response = InvokeOperation<NativePluginBackendInfoResponse>(
                NativePluginOperationNames.DescribeBackend,
                new NativePluginEmptyPayload());

            _cachedBackendInfo = new MediaSfuNativePluginBackendInfo
            {
                Description = response?.Description ?? string.Empty,
                BackendKind = response?.BackendKind ?? string.Empty,
                IsPlaceholder = response?.IsPlaceholder ?? false,
                Platform = response?.Platform ?? string.Empty,
                SupportsAudio = response?.SupportsAudio,
                SupportsVideo = response?.SupportsVideo,
                SupportsScreenShare = response?.SupportsScreenShare,
                SupportsWhiteboard = response?.SupportsWhiteboard,
                SupportsReceive = response?.SupportsReceive,
            };

            return _cachedBackendInfo;
        }

        public Task CompleteSendTransportConnectAsync(
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.CompleteSendTransportConnect,
                new NativePluginCompleteTransportConnectPayload
                {
                    Success = success,
                    ErrorDetail = errorDetail ?? string.Empty
                });

            return Task.CompletedTask;
        }

        public Task<MediaSfuProduceRequest> CreateProduceRequestAsync(
            MediaSfuTrackKind kind,
            MediaSfuLocalMediaRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureNotDisposed();

                if (request == null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                EnsureBackendSupportsTrackKind(kind, cancellationToken);

                var response = InvokeOperation<NativePluginProduceRequestResponse>(
                    NativePluginOperationNames.CreateProduceRequest,
                    new NativePluginCreateProduceRequestPayload
                    {
                        TrackKind = kind,
                        Enabled = request.Enabled,
                        LocalUserName = request.LocalUserName,
                        LocalIsLevel = request.LocalIsLevel,
                        MeetingId = request.Room?.RoomName ?? string.Empty,
                        RoomRtpCapabilitiesJson = request.RoomValidation?.RtpCapabilitiesJson ?? string.Empty,
                        SecureCode = request.RoomValidation?.SecureCode ?? string.Empty,
                        ParticipantId = request.LocalParticipant?.ParticipantId ?? string.Empty,
                        ParticipantName = request.LocalParticipant?.DisplayName ?? string.Empty,
                        ParticipantIslevel = request.LocalIsLevel ?? string.Empty
                    });

                return response?.ProduceRequest;
            }, cancellationToken);
        }

        public Task BindProducerAsync(
            MediaSfuTrackKind kind,
            string producerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.BindProducer,
                new NativePluginBindProducerPayload
                {
                    TrackKind = kind,
                    ProducerId = producerId ?? string.Empty
                });

            return Task.CompletedTask;
        }

        public Task PauseProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.PauseProducer,
                new NativePluginTrackOperationPayload
                {
                    TrackKind = kind
                });

            return Task.CompletedTask;
        }

        public Task ResumeProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.ResumeProducer,
                new NativePluginTrackOperationPayload
                {
                    TrackKind = kind
                });

            return Task.CompletedTask;
        }

        public Task CloseProducerAsync(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.CloseProducer,
                new NativePluginTrackOperationPayload
                {
                    TrackKind = kind
                });

            return Task.CompletedTask;
        }

        public Task InitializeReceiveTransportAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuWebRtcTransport transport,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            EnsureBackendSupportsReceive(cancellationToken);

            if (remoteProducer == null)
            {
                throw new ArgumentNullException(nameof(remoteProducer));
            }

            InvokeVoidOperation(
                NativePluginOperationNames.InitializeReceiveTransport,
                new NativePluginReceiveTransportPayload
                {
                    RemoteProducer = remoteProducer,
                    Transport = transport ?? throw new ArgumentNullException(nameof(transport))
                });

            return Task.CompletedTask;
        }

        public Task<string> CreateReceiveTransportConnectParametersJsonAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            if (remoteProducer == null)
            {
                throw new ArgumentNullException(nameof(remoteProducer));
            }

            var response = InvokeOperation<NativePluginDtlsParametersResponse>(
                NativePluginOperationNames.CreateReceiveTransportConnectParameters,
                new NativePluginRemoteProducerPayload
                {
                    RemoteProducer = remoteProducer
                });

            return Task.FromResult(response?.DtlsParametersJson ?? string.Empty);
        }

        public Task CompleteReceiveTransportConnectAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            bool success,
            string errorDetail = "",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            if (remoteProducer == null)
            {
                throw new ArgumentNullException(nameof(remoteProducer));
            }

            InvokeVoidOperation(
                NativePluginOperationNames.CompleteReceiveTransportConnect,
                new NativePluginCompleteReceiveTransportConnectPayload
                {
                    RemoteProducer = remoteProducer,
                    Success = success,
                    ErrorDetail = errorDetail ?? string.Empty
                });

            return Task.CompletedTask;
        }

        public Task BindConsumerAsync(
            MediaSfuRemoteProducerEvent remoteProducer,
            MediaSfuConsumeResponse response,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureNotDisposed();
                EnsureBackendSupportsReceive(cancellationToken);

                if (remoteProducer == null)
                {
                    throw new ArgumentNullException(nameof(remoteProducer));
                }

                InvokeVoidOperation(
                    NativePluginOperationNames.BindConsumer,
                    new NativePluginBindConsumerPayload
                    {
                        RemoteProducer = remoteProducer,
                        ConsumeResponse = response ?? throw new ArgumentNullException(nameof(response))
                    });
            }, cancellationToken);
        }

        public Task CloseConsumerAsync(
            string remoteProducerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();

            InvokeVoidOperation(
                NativePluginOperationNames.CloseConsumer,
                new NativePluginCloseConsumerPayload
                {
                    RemoteProducerId = remoteProducerId ?? string.Empty
                });

            return Task.CompletedTask;
        }

        public MediaSfuNativePluginRemoteVideoFrameInfo GetRemoteVideoFrameInfo(
            string remoteProducerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            EnsureBackendSupportsReceive(cancellationToken);

            if (string.IsNullOrWhiteSpace(remoteProducerId))
            {
                throw new ArgumentException(
                    "MediaSFU native plugin requires a non-empty remote producer id to query remote video frame metadata.",
                    nameof(remoteProducerId));
            }

            var response = InvokeOperation<NativePluginRemoteVideoFrameMetadataResponse>(
                NativePluginOperationNames.GetRemoteVideoFrameMetadata,
                new NativePluginRemoteVideoFramePayload
                {
                    RemoteProducerId = remoteProducerId
                });

            return new MediaSfuNativePluginRemoteVideoFrameInfo
            {
                Available = response?.Available ?? false,
                Sequence = response?.Sequence ?? 0,
                Width = response?.Width ?? 0,
                Height = response?.Height ?? 0,
                Rotation = response?.Rotation ?? 0,
                TimestampNs = response?.TimestampNs ?? 0,
                ByteCount = response?.ByteCount ?? 0,
                ResolvedBy = response?.ResolvedBy ?? string.Empty,
                RemoteProducerId = response?.RemoteProducerId ?? string.Empty,
                ConsumerId = response?.ConsumerId ?? string.Empty,
                TrackId = response?.TrackId ?? string.Empty,
                ConsumerKind = response?.ConsumerKind ?? string.Empty,
                RendererAttached = response?.RendererAttached ?? false,
                ConsumerPaused = response?.ConsumerPaused ?? false,
                ConnectionState = response?.ConnectionState ?? string.Empty,
                ConsumerStatsJson = response?.ConsumerStatsJson ?? string.Empty,
            };
        }

        public int CopyRemoteVideoFrame(
            string remoteProducerId,
            byte[] destination,
            int destinationOffset = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            EnsureBackendSupportsReceive(cancellationToken);

            if (string.IsNullOrWhiteSpace(remoteProducerId))
            {
                throw new ArgumentException(
                    "MediaSFU native plugin requires a non-empty remote producer id to copy a remote video frame.",
                    nameof(remoteProducerId));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destinationOffset < 0 || destinationOffset > destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }

            var capacity = destination.Length - destinationOffset;
            if (capacity <= 0)
            {
                return 0;
            }

            GCHandle pinnedBuffer = default;
            try
            {
                pinnedBuffer = GCHandle.Alloc(destination, GCHandleType.Pinned);
                var destinationPointer = IntPtr.Add(pinnedBuffer.AddrOfPinnedObject(), destinationOffset);
                int copiedByteCount;
                try
                {
                    copiedByteCount = NativePluginInterop.CopyRemoteVideoFrame(
                        _engineHandle,
                        remoteProducerId,
                        destinationPointer,
                        capacity);
                }
                catch (Exception error) when (IsNativeInteropException(error))
                {
                    throw BuildInteropUnavailableException(
                        $"copy the remote video frame for producer '{remoteProducerId}'",
                        error);
                }

                if (copiedByteCount < 0)
                {
                    throw new InvalidOperationException(
                        $"MediaSFU remote video frame copy requires {-copiedByteCount} bytes, but the provided destination only has {capacity} bytes available.");
                }

                return copiedByteCount;
            }
            finally
            {
                if (pinnedBuffer.IsAllocated)
                {
                    pinnedBuffer.Free();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_engineHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                NativePluginInterop.DestroyEngine(_engineHandle);
            }
            catch (Exception error) when (IsNativeInteropException(error))
            {
                throw BuildInteropUnavailableException("destroy the native WebRTC engine", error);
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaSfuNativePluginWebRtcEngine));
            }
        }

        private void InvokeVoidOperation(string operationName, object payload)
        {
            InvokeOperation<NativePluginEmptyPayload>(operationName, payload);
        }

        private TResponse InvokeOperation<TResponse>(string operationName, object payload)
            where TResponse : class
        {
            string payloadJson;
            try
            {
                payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize MediaSFU native plugin payload for operation '{operationName}': {error.Message}",
                    error);
            }

            IntPtr responsePointer;
            try
            {
                responsePointer = NativePluginInterop.Invoke(_engineHandle, operationName, payloadJson);
            }
            catch (Exception error) when (IsNativeInteropException(error))
            {
                throw BuildInteropUnavailableException(
                    $"invoke native WebRTC engine operation '{operationName}'",
                    error);
            }

            if (responsePointer == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"MediaSFU native plugin operation '{operationName}' returned no response payload.");
            }

            string responseJson = null;
            try
            {
                responseJson = ReadUtf8String(responsePointer);
            }
            finally
            {
                try
                {
                    NativePluginInterop.FreeString(responsePointer);
                }
                catch (Exception error) when (IsNativeInteropException(error))
                {
                    throw BuildInteropUnavailableException(
                        $"free the native response for operation '{operationName}'",
                        error);
                }
            }

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new InvalidOperationException(
                    $"MediaSFU native plugin operation '{operationName}' returned an empty response payload.");
            }

            NativePluginResponse<TResponse> parsedResponse;
            try
            {
                parsedResponse = JsonSerializer.Deserialize<NativePluginResponse<TResponse>>(responseJson, _jsonOptions);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    $"Failed to parse MediaSFU native plugin response for operation '{operationName}': {error.Message}",
                    error);
            }

            if (parsedResponse == null)
            {
                throw new InvalidOperationException(
                    $"MediaSFU native plugin operation '{operationName}' returned an unreadable response.");
            }

            if (!parsedResponse.Success)
            {
                var detail = string.IsNullOrWhiteSpace(parsedResponse.Detail)
                    ? parsedResponse.Error
                    : parsedResponse.Detail;
                throw new InvalidOperationException(
                    $"MediaSFU native plugin operation '{operationName}' failed: {detail}");
            }

            return parsedResponse.Result;
        }

        private static string ReadUtf8String(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            var length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            var buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        private static bool IsNativeInteropException(Exception error)
        {
            return error is DllNotFoundException ||
                   error is EntryPointNotFoundException ||
                   error is BadImageFormatException;
        }

        private static InvalidOperationException BuildInteropUnavailableException(
            string attemptedAction,
            Exception innerException)
        {
            return new InvalidOperationException(
                $"MediaSFU native plugin bridge is unavailable while trying to {attemptedAction}. Add the native plugin binary and export the expected MediaSFU bridge symbols before attaching MediaSfuNativePluginWebRtcEngine.",
                innerException);
        }

        private static string BuildAttachDetail(MediaSfuNativePluginBackendInfo backendInfo)
        {
            var description = string.IsNullOrWhiteSpace(backendInfo?.Description)
                ? "Native plugin backend"
                : backendInfo.Description;
            var backendKind = string.IsNullOrWhiteSpace(backendInfo?.BackendKind)
                ? "installed"
                : backendInfo.BackendKind;
            var capabilitySummary = BuildCapabilitySummary(backendInfo);

            return $"Native plugin WebRTC engine attached. Backend: {description} ({backendKind}).{capabilitySummary}";
        }

        private static string BuildPlaceholderBackendDetail(MediaSfuNativePluginBackendInfo backendInfo)
        {
            var description = string.IsNullOrWhiteSpace(backendInfo?.Description)
                ? "MediaSFU Unity native bridge placeholder"
                : backendInfo.Description;

            return description +
                   " is loadable but does not provide a real media backend. Replace the shared bridge stub or install a real backend behind the MediaSfuUnity* ABI before attaching MediaSfuNativePluginWebRtcEngine.";
        }

        private static string BuildMockBackendDetail(MediaSfuNativePluginBackendInfo backendInfo)
        {
            var description = string.IsNullOrWhiteSpace(backendInfo?.Description)
                ? "MediaSFU Unity native bridge mock"
                : backendInfo.Description;
            var platform = string.IsNullOrWhiteSpace(backendInfo?.Platform)
                ? string.Empty
                : $" Platform={backendInfo.Platform}.";

            return description +
                   " only simulates WebRTC operations and cannot publish real Unity audio/video to the SFU." +
                   platform +
                   " Replace the plugin binary with a real native backend, or set AllowMockBackend=true only for explicit smoke tests.";
        }

        private static bool ShouldAllowMockBackend(MediaSfuNativePluginWebRtcEngineOptions options)
        {
            return options?.AllowMockBackend ?? false;
        }

        private static bool IsMockBackend(MediaSfuNativePluginBackendInfo backendInfo)
        {
            if (backendInfo == null)
            {
                return false;
            }

            return string.Equals(backendInfo.BackendKind, "mock", StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(backendInfo.Description) &&
                    backendInfo.Description.IndexOf("mock", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildTryCreateFailureDetail(Exception error)
        {
            if (error == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(error.Message);

            var innerError = error.InnerException;
            while (innerError != null)
            {
                if (!string.IsNullOrWhiteSpace(innerError.Message))
                {
                    builder.Append(" Inner: ");
                    builder.Append(innerError.Message);
                }

                innerError = innerError.InnerException;
            }

            return builder.ToString();
        }

        private void EnsureBackendSupportsTrackKind(
            MediaSfuTrackKind kind,
            CancellationToken cancellationToken)
        {
            var backendInfo = GetBackendInfo(cancellationToken);
            string unsupportedDetail = null;

            switch (kind)
            {
                case MediaSfuTrackKind.Audio:
                    if (backendInfo.SupportsAudio == false)
                    {
                        unsupportedDetail = BuildUnsupportedCapabilityDetail(backendInfo, "local audio capture and producer creation");
                    }

                    break;
                case MediaSfuTrackKind.Video:
                    if (backendInfo.SupportsVideo == false)
                    {
                        unsupportedDetail = BuildUnsupportedCapabilityDetail(backendInfo, "local video capture and producer creation");
                    }

                    break;
                case MediaSfuTrackKind.Screen:
                    if (backendInfo.SupportsScreenShare == false)
                    {
                        unsupportedDetail = BuildUnsupportedCapabilityDetail(backendInfo, "screen-share capture and producer creation");
                    }

                    break;
                case MediaSfuTrackKind.Whiteboard:
                    if (backendInfo.SupportsWhiteboard == false)
                    {
                        unsupportedDetail = BuildUnsupportedCapabilityDetail(backendInfo, "whiteboard track production");
                    }

                    break;
            }

            if (!string.IsNullOrWhiteSpace(unsupportedDetail))
            {
                throw new NotSupportedException(unsupportedDetail);
            }
        }

        private void EnsureBackendSupportsReceive(CancellationToken cancellationToken)
        {
            var backendInfo = GetBackendInfo(cancellationToken);
            if (backendInfo.SupportsReceive == false)
            {
                throw new NotSupportedException(
                    BuildUnsupportedCapabilityDetail(backendInfo, "receive transport initialization and consumer binding"));
            }
        }

        private static string BuildUnsupportedCapabilityDetail(
            MediaSfuNativePluginBackendInfo backendInfo,
            string capabilityLabel)
        {
            var description = string.IsNullOrWhiteSpace(backendInfo?.Description)
                ? "Native plugin backend"
                : backendInfo.Description;
            var platform = string.IsNullOrWhiteSpace(backendInfo?.Platform)
                ? string.Empty
                : $" Platform={backendInfo.Platform}.";

            return description + " does not support " + capabilityLabel + "." + platform;
        }

        private static string BuildCapabilitySummary(MediaSfuNativePluginBackendInfo backendInfo)
        {
            if (backendInfo == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var hasAnyCapabilityData = false;

            AppendCapability(builder, ref hasAnyCapabilityData, "audio", backendInfo.SupportsAudio);
            AppendCapability(builder, ref hasAnyCapabilityData, "video", backendInfo.SupportsVideo);
            AppendCapability(builder, ref hasAnyCapabilityData, "screenShare", backendInfo.SupportsScreenShare);
            AppendCapability(builder, ref hasAnyCapabilityData, "whiteboard", backendInfo.SupportsWhiteboard);
            AppendCapability(builder, ref hasAnyCapabilityData, "receive", backendInfo.SupportsReceive);

            if (!hasAnyCapabilityData)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(backendInfo.Platform))
            {
                builder.Append(" platform=");
                builder.Append(backendInfo.Platform);
                builder.Append(';');
            }

            return " Capabilities: " + builder.ToString().TrimEnd(';');
        }

        private static void AppendCapability(
            StringBuilder builder,
            ref bool hasAnyCapabilityData,
            string name,
            bool? supported)
        {
            if (!supported.HasValue)
            {
                return;
            }

            if (hasAnyCapabilityData)
            {
                builder.Append(' ');
            }

            builder.Append(name);
            builder.Append('=');
            builder.Append(supported.Value ? "true" : "false");
            builder.Append(';');
            hasAnyCapabilityData = true;
        }

        private static class NativePluginOperationNames
        {
            public const string DescribeBackend = "describeBackend";
            public const string LoadDeviceRtpCapabilities = "loadDeviceRtpCapabilities";
            public const string GetLoadedDeviceRtpCapabilities = "getLoadedDeviceRtpCapabilities";
            public const string InitializeSendTransport = "initializeSendTransport";
            public const string CreateSendTransportConnectParameters = "createSendTransportConnectParameters";
            public const string CompleteSendTransportConnect = "completeSendTransportConnect";
            public const string CreateProduceRequest = "createProduceRequest";
            public const string BindProducer = "bindProducer";
            public const string PauseProducer = "pauseProducer";
            public const string ResumeProducer = "resumeProducer";
            public const string CloseProducer = "closeProducer";
            public const string InitializeReceiveTransport = "initializeReceiveTransport";
            public const string CreateReceiveTransportConnectParameters = "createReceiveTransportConnectParameters";
            public const string CompleteReceiveTransportConnect = "completeReceiveTransportConnect";
            public const string BindConsumer = "bindConsumer";
            public const string GetRemoteVideoFrameMetadata = "getRemoteVideoFrameMetadata";
            public const string CloseConsumer = "closeConsumer";
        }

        private static class NativePluginInterop
        {
            public static IntPtr CreateEngine(string payloadJson)
            {
                return CreateEngineInternal(payloadJson ?? string.Empty);
            }

            public static void DestroyEngine(IntPtr engineHandle)
            {
                if (engineHandle == IntPtr.Zero)
                {
                    return;
                }

                DestroyEngineInternal(engineHandle);
            }

            public static IntPtr Invoke(IntPtr engineHandle, string operationName, string payloadJson)
            {
                return InvokeInternal(engineHandle, operationName ?? string.Empty, payloadJson ?? string.Empty);
            }

            public static void FreeString(IntPtr payloadPointer)
            {
                if (payloadPointer == IntPtr.Zero)
                {
                    return;
                }

                FreeStringInternal(payloadPointer);
            }

            public static int CopyRemoteVideoFrame(
                IntPtr engineHandle,
                string remoteProducerId,
                IntPtr destination,
                int capacity)
            {
                return CopyRemoteVideoFrameInternal(
                    engineHandle,
                    remoteProducerId ?? string.Empty,
                    destination,
                    capacity);
            }

#if UNITY_IOS && !UNITY_EDITOR
            private const string PluginName = "__Internal";
#else
            private const string PluginName = "MediaSFUNativeBridge";
#endif

            [DllImport(PluginName, EntryPoint = "MediaSfuUnityCreateWebRtcEngine")]
            private static extern IntPtr CreateEngineInternal(string payloadJson);

            [DllImport(PluginName, EntryPoint = "MediaSfuUnityDestroyWebRtcEngine")]
            private static extern void DestroyEngineInternal(IntPtr engineHandle);

            [DllImport(PluginName, EntryPoint = "MediaSfuUnityInvokeWebRtcEngine")]
            private static extern IntPtr InvokeInternal(IntPtr engineHandle, string operationName, string payloadJson);

            [DllImport(PluginName, EntryPoint = "MediaSfuUnityCopyRemoteVideoFrame")]
            private static extern int CopyRemoteVideoFrameInternal(
                IntPtr engineHandle,
                string remoteProducerId,
                IntPtr destination,
                int capacity);

            [DllImport(PluginName, EntryPoint = "MediaSfuUnityFreeString")]
            private static extern void FreeStringInternal(IntPtr payloadPointer);
        }

        private sealed class NativePluginResponse<T>
            where T : class
        {
            public bool Success { get; set; } = true;

            public string Error { get; set; } = string.Empty;

            public string Detail { get; set; } = string.Empty;

            public T Result { get; set; }
        }

        private sealed class NativePluginBackendInfoResponse
        {
            public string Description { get; set; } = string.Empty;

            public string BackendKind { get; set; } = string.Empty;

            public bool IsPlaceholder { get; set; }

            public string Platform { get; set; } = string.Empty;

            public bool? SupportsAudio { get; set; }

            public bool? SupportsVideo { get; set; }

            public bool? SupportsScreenShare { get; set; }

            public bool? SupportsWhiteboard { get; set; }

            public bool? SupportsReceive { get; set; }
        }

        private sealed class NativePluginCreateEnginePayload
        {
            public string ConfigurationJson { get; set; } = string.Empty;

            public bool EnableVerboseLogging { get; set; }

            public string IntegrationMode { get; set; } = string.Empty;

            public bool AllowMockBackend { get; set; }
        }

        private sealed class NativePluginEmptyPayload
        {
        }

        private sealed class NativePluginSendTransportPayload
        {
            public MediaSfuWebRtcTransport Transport { get; set; }
        }

        private sealed class NativePluginLoadDeviceRtpCapabilitiesPayload
        {
            public string RoomRtpCapabilitiesJson { get; set; } = string.Empty;
        }

        private class NativePluginCompleteTransportConnectPayload
        {
            public bool Success { get; set; }

            public string ErrorDetail { get; set; } = string.Empty;
        }

        private sealed class NativePluginReceiveTransportPayload
        {
            public MediaSfuRemoteProducerEvent RemoteProducer { get; set; }

            public MediaSfuWebRtcTransport Transport { get; set; }
        }

        private sealed class NativePluginCompleteReceiveTransportConnectPayload : NativePluginCompleteTransportConnectPayload
        {
            public MediaSfuRemoteProducerEvent RemoteProducer { get; set; }
        }

        private sealed class NativePluginRemoteProducerPayload
        {
            public MediaSfuRemoteProducerEvent RemoteProducer { get; set; }
        }

        private sealed class NativePluginTrackOperationPayload
        {
            public MediaSfuTrackKind TrackKind { get; set; }
        }

        private sealed class NativePluginBindProducerPayload
        {
            public MediaSfuTrackKind TrackKind { get; set; }

            public string ProducerId { get; set; } = string.Empty;
        }

        private sealed class NativePluginBindConsumerPayload
        {
            public MediaSfuRemoteProducerEvent RemoteProducer { get; set; }

            public MediaSfuConsumeResponse ConsumeResponse { get; set; }
        }

        private sealed class NativePluginCloseConsumerPayload
        {
            public string RemoteProducerId { get; set; } = string.Empty;
        }

        private sealed class NativePluginRemoteVideoFramePayload
        {
            public string RemoteProducerId { get; set; } = string.Empty;
        }

        private sealed class NativePluginCreateProduceRequestPayload
        {
            public MediaSfuTrackKind TrackKind { get; set; }

            public bool Enabled { get; set; }

            public string LocalUserName { get; set; } = string.Empty;

            public string LocalIsLevel { get; set; } = string.Empty;

            public string MeetingId { get; set; } = string.Empty;

            public string RoomRtpCapabilitiesJson { get; set; } = string.Empty;

            public string SecureCode { get; set; } = string.Empty;

            public string ParticipantId { get; set; } = string.Empty;

            public string ParticipantName { get; set; } = string.Empty;

            public string ParticipantIslevel { get; set; } = string.Empty;
        }

        private sealed class NativePluginDtlsParametersResponse
        {
            public string DtlsParametersJson { get; set; } = string.Empty;
        }

        private sealed class NativePluginRtpCapabilitiesResponse
        {
            public string RtpCapabilitiesJson { get; set; } = string.Empty;
        }

        private sealed class NativePluginProduceRequestResponse
        {
            public MediaSfuProduceRequest ProduceRequest { get; set; }
        }

        private sealed class NativePluginRemoteVideoFrameMetadataResponse
        {
            public bool Available { get; set; }

            public long Sequence { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public int Rotation { get; set; }

            public long TimestampNs { get; set; }

            public int ByteCount { get; set; }

            public string ResolvedBy { get; set; } = string.Empty;

            public string RemoteProducerId { get; set; } = string.Empty;

            public string ConsumerId { get; set; } = string.Empty;

            public string TrackId { get; set; } = string.Empty;

            public string ConsumerKind { get; set; } = string.Empty;

            public bool RendererAttached { get; set; }

            public bool ConsumerPaused { get; set; }

            public string ConnectionState { get; set; } = string.Empty;

            public string ConsumerStatsJson { get; set; } = string.Empty;
        }
    }
}
