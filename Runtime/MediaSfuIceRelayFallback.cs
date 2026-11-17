using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("MediaSFU.Unity.Editor.Tests")]

namespace MediaSFU.Unity
{
    /// <summary>
    /// TURN relay details a MediaSFU server sends beside the <c>createWebRtcTransport</c> params.
    /// The server decides the mode and the wait; there is nothing to configure on the client.
    /// </summary>
    public sealed class MediaSfuIceRelayOffer
    {
        /// <summary>JSON array of RTCIceServer objects (<c>urls</c>, <c>username</c>, <c>credential</c>).</summary>
        public string IceServersJson { get; set; } = string.Empty;

        /// <summary><c>fallback</c> or <c>always</c>, as sent by the server.</summary>
        public string IceRelayMode { get; set; } = string.Empty;

        /// <summary>How long ICE may stay unconnected before the relay restart, as sent by the server.</summary>
        public int IceRelayAfterMs { get; set; }

        public bool HasIceServers => IsNonEmptyArray(IceServersJson);

        /// <summary>True when the transport must be created with the relay already allowed (<c>always</c>).</summary>
        public bool CreatesTransportWithRelay =>
            HasIceServers && string.Equals(IceRelayMode, "always", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads <c>iceServers</c>, <c>iceRelayMode</c> and <c>iceRelayAfterMs</c> from the top level of a
        /// <c>createWebRtcTransport</c> acknowledgment (beside <c>params</c>, never inside). Returns an empty offer for
        /// older servers that do not send them.
        /// </summary>
        public static MediaSfuIceRelayOffer FromTransportAcknowledgment(string acknowledgmentJson)
        {
            var offer = new MediaSfuIceRelayOffer();
            var root = ParseAcknowledgmentRoot(acknowledgmentJson);
            if (root == null)
            {
                return offer;
            }

            if (root["iceServers"] is JArray servers && servers.Count > 0)
            {
                offer.IceServersJson = servers.ToString(Newtonsoft.Json.Formatting.None);
            }

            if (root["iceRelayMode"] is JValue mode && mode.Type == JTokenType.String)
            {
                offer.IceRelayMode = (string)mode ?? string.Empty;
            }

            if (root["iceRelayAfterMs"] is JValue after &&
                (after.Type == JTokenType.Integer || after.Type == JTokenType.Float))
            {
                offer.IceRelayAfterMs = (int)Math.Max(0, Math.Min(int.MaxValue, (double)after));
            }

            return offer;
        }

        internal static JObject ParseAcknowledgmentRoot(string acknowledgmentJson)
        {
            if (string.IsNullOrWhiteSpace(acknowledgmentJson))
            {
                return null;
            }

            try
            {
                var token = JToken.Parse(acknowledgmentJson);
                if (token is JArray array)
                {
                    token = array.Count > 0 ? array[0] : null;
                }

                return token as JObject;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        internal static bool IsNonEmptyArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                return JToken.Parse(json) is JArray array && array.Count > 0;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The media socket a transport was created on. Set by the MediaSFU client next to the relay offer so the relay
    /// restart and its report go to the same socket that owns the transport.
    /// </summary>
    public sealed class MediaSfuIceRelaySignalling
    {
        public Func<string, Dictionary<string, object>, CancellationToken, Task<string>> EmitWithAckAsync { get; set; }

        public Func<string, Dictionary<string, object>, CancellationToken, Task> EmitAsync { get; set; }

        /// <summary>False once that socket is gone or replaced; the watcher then stops without acting.</summary>
        public Func<bool> IsCurrent { get; set; }

        /// <summary>Called when the network blocks the call; the client shows the message once per media socket.</summary>
        public Action OnNetworkBlocked { get; set; }
    }

    /// <summary>ICE state of one transport, as reported by the WebRTC engine.</summary>
    public sealed class MediaSfuTransportIceState
    {
        /// <summary>new, checking, connecting, connected, completed, disconnected, failed or closed.</summary>
        public string ConnectionState { get; set; } = string.Empty;

        /// <summary>Selected local candidate: host, srflx, prflx or relay (empty when unknown).</summary>
        public string CandidateType { get; set; } = string.Empty;

        public string Protocol { get; set; } = string.Empty;

        public string RelayProtocol { get; set; } = string.Empty;
    }

    /// <summary>
    /// Optional engine capability needed by the TURN relay fallback. Engines (or native bridges) without it keep the
    /// previous behaviour: no relay restart and no blocked-network message.
    /// </summary>
    public interface IMediaSfuIceRelayTransportControl
    {
        /// <summary>Returns null when the engine cannot report transport ICE state.</summary>
        Task<MediaSfuTransportIceState> GetTransportIceStateAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            CancellationToken cancellationToken = default);

        Task UpdateIceServersAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            string iceServersJson,
            CancellationToken cancellationToken = default);

        Task RestartIceAsync(
            MediaSfuTransportDirection direction,
            string transportId,
            string iceParametersJson,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// TURN relay fallback (turn-relay-v1). Media servers accept ICE only on a high UDP/TCP port range, so a network
    /// that blocks those ports used to leave a transport connecting forever. When the server offers a relay and the
    /// transport is still not connected <c>iceRelayAfterMs</c> after ICE starts, ICE is restarted once with the relay
    /// allowed. If that does not connect either (or no relay is offered), the user is told the network is blocking the
    /// call instead of a silent call. Every outcome is reported to the server with <c>transport-ice-report</c>.
    /// </summary>
    public sealed class MediaSfuIceRelayFallback : IDisposable
    {
        public const string NetworkBlockedMessage =
            "Your network is blocking the call connection. Try another network (a phone hotspot works) or turn off your VPN, then rejoin.";

        internal const int DefaultRelayAfterMs = 8000;
        internal const int ConnectWaitMs = 12000;
        internal const int ThrottleRetryMs = 2100;
        internal const int AckTimeoutMs = 8000;
        internal const int PollIntervalMs = 250;

        private readonly IMediaSfuIceRelayTransportControl _control;
        private readonly MediaSfuIceRelayOffer _offer;
        private readonly MediaSfuIceRelaySignalling _signalling;
        private readonly MediaSfuTransportDirection _direction;
        private readonly string _transportId;
        private readonly Func<bool> _isTransportCurrent;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Func<DateTime> _utcNow;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly int _relayAfterMs;

        private int _connectSignalled;
        private int _disposed;
        private DateTime _startedAt;

        private MediaSfuIceRelayFallback(
            IMediaSfuIceRelayTransportControl control,
            MediaSfuIceRelayOffer offer,
            MediaSfuIceRelaySignalling signalling,
            MediaSfuTransportDirection direction,
            string transportId,
            Func<bool> isTransportCurrent,
            Func<TimeSpan, CancellationToken, Task> delay,
            Func<DateTime> utcNow)
        {
            _control = control;
            _offer = offer ?? new MediaSfuIceRelayOffer();
            _signalling = signalling;
            _direction = direction;
            _transportId = transportId;
            _isTransportCurrent = isTransportCurrent ?? (() => true);
            _delay = delay ?? Task.Delay;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _relayAfterMs = Math.Max(
                3000,
                Math.Min(20000, _offer.IceRelayAfterMs > 0 ? _offer.IceRelayAfterMs : DefaultRelayAfterMs));
        }

        /// <summary>Completes when the watcher has finished (connected, blocked, closed or disposed).</summary>
        public Task Completion { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Starts watching one transport. Returns null (nothing watched, previous behaviour) when the engine has no
        /// ICE control or the transport has no media socket to signal on.
        /// </summary>
        public static MediaSfuIceRelayFallback Attach(
            object engineOrAdapter,
            MediaSfuWebRtcTransport transport,
            MediaSfuTransportDirection direction,
            Func<bool> isTransportCurrent)
        {
            return Attach(engineOrAdapter, transport, direction, isTransportCurrent, null, null);
        }

        internal static MediaSfuIceRelayFallback Attach(
            object engineOrAdapter,
            MediaSfuWebRtcTransport transport,
            MediaSfuTransportDirection direction,
            Func<bool> isTransportCurrent,
            Func<TimeSpan, CancellationToken, Task> delay,
            Func<DateTime> utcNow)
        {
            if (!(engineOrAdapter is IMediaSfuIceRelayTransportControl control) ||
                transport == null ||
                string.IsNullOrWhiteSpace(transport.Id) ||
                transport.IceRelaySignalling?.EmitWithAckAsync == null)
            {
                return null;
            }

            var watcher = new MediaSfuIceRelayFallback(
                control,
                transport.IceRelayOffer,
                transport.IceRelaySignalling,
                direction,
                transport.Id,
                isTransportCurrent,
                delay,
                utcNow);
            // Off the caller's stack and off Unity's main-thread context: transport setup continues while this waits.
            watcher.Completion = Task.Run(watcher.RunGuardedAsync);
            return watcher;
        }

        /// <summary>
        /// The transport's connect step ran (DTLS parameters were requested). ICE starts then even when the engine
        /// does not show a connecting state yet.
        /// </summary>
        public void NotifyTransportConnect()
        {
            Interlocked.Exchange(ref _connectSignalled, 1);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private bool Current =>
            Volatile.Read(ref _disposed) == 0 &&
            SafeInvoke(_isTransportCurrent) &&
            (_signalling?.IsCurrent == null || SafeInvoke(_signalling.IsCurrent));

        private async Task RunGuardedAsync()
        {
            try
            {
                await RunAsync(_cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                Console.WriteLine("MediaSfu relay fallback stopped transportId=" + _transportId + " reason=" + error.Message);
            }
            finally
            {
                Dispose();
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // Wait for ICE to start: the first connecting/checking state or the transport's connect step.
            while (true)
            {
                if (!Current)
                {
                    return;
                }

                var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
                if (state == null)
                {
                    // The engine cannot report ICE state: keep the previous behaviour.
                    return;
                }

                if (IsConnected(state) || IsClosed(state))
                {
                    return;
                }

                if (IsChecking(state) || Volatile.Read(ref _connectSignalled) == 1)
                {
                    break;
                }

                await _delay(TimeSpan.FromMilliseconds(PollIntervalMs), cancellationToken).ConfigureAwait(false);
            }

            _startedAt = _utcNow();

            // Give direct ICE its time; connected before the mark means nothing to do.
            while (ElapsedMs() < _relayAfterMs)
            {
                await _delay(TimeSpan.FromMilliseconds(Math.Min(PollIntervalMs, Math.Max(1, _relayAfterMs - ElapsedMs()))), cancellationToken)
                    .ConfigureAwait(false);
                if (!Current)
                {
                    return;
                }

                var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
                if (state == null || IsConnected(state) || IsClosed(state))
                {
                    return;
                }
            }

            if (!Current)
            {
                return;
            }

            // Allow the relay on the live transport before the server is asked to restart: an engine that refuses
            // the servers (for example one that cannot pass TURN credentials) must not leave the server with new ICE
            // parameters the client never follows.
            if (!_offer.HasIceServers || !await TryUpdateIceServersAsync(_offer.IceServersJson, cancellationToken).ConfigureAwait(false))
            {
                // No usable relay: give ICE a little longer, then say so instead of staying silent.
                if (!await WaitConnectedAsync(ConnectWaitMs, cancellationToken).ConfigureAwait(false) && Current)
                {
                    await ReportAsync("unavailable", null, cancellationToken).ConfigureAwait(false);
                    await BlockedAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            JObject answer = null;
            for (var attempt = 0; attempt < 2 && Current && answer?["iceParameters"] == null; attempt++)
            {
                answer = await RequestRelayRestartAsync(cancellationToken).ConfigureAwait(false);
                // The server throttles restarts to one per 2 s; another recovery may have just used it.
                if (answer?["iceParameters"] == null && attempt == 0)
                {
                    await _delay(TimeSpan.FromMilliseconds(ThrottleRetryMs), cancellationToken).ConfigureAwait(false);
                }
            }

            if (!Current)
            {
                return;
            }

            var beforeRestart = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
            if (beforeRestart != null && IsConnected(beforeRestart))
            {
                return;
            }

            var iceParameters = answer?["iceParameters"];
            if (iceParameters == null || iceParameters.Type != JTokenType.Object)
            {
                await BlockedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            // Fresh credentials come with the restart answer; without them the offered servers already apply.
            var answerServers = answer["iceServers"] as JArray;
            if (answerServers != null &&
                answerServers.Count > 0 &&
                !await TryUpdateIceServersAsync(answerServers.ToString(Newtonsoft.Json.Formatting.None), cancellationToken).ConfigureAwait(false))
            {
                await BlockedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                await _control.RestartIceAsync(
                        _direction,
                        _transportId,
                        iceParameters.ToString(Newtonsoft.Json.Formatting.None),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await BlockedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (await WaitConnectedAsync(ConnectWaitMs, cancellationToken).ConfigureAwait(false))
            {
                if (Current)
                {
                    var connected = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
                    await ReportAsync("connected", connected, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            await BlockedAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> TryUpdateIceServersAsync(string iceServersJson, CancellationToken cancellationToken)
        {
            try
            {
                await _control.UpdateIceServersAsync(_direction, _transportId, iceServersJson, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<JObject> RequestRelayRestartAsync(CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(AckTimeoutMs);
                try
                {
                    var acknowledgment = await _signalling.EmitWithAckAsync(
                            "transport-restart-ice",
                            new Dictionary<string, object>
                            {
                                ["transportId"] = _transportId,
                                ["relay"] = true
                            },
                            timeout.Token)
                        .ConfigureAwait(false);
                    return MediaSfuIceRelayOffer.ParseAcknowledgmentRoot(acknowledgment);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private async Task<bool> WaitConnectedAsync(int waitMs, CancellationToken cancellationToken)
        {
            var deadline = _utcNow().AddMilliseconds(waitMs);
            while (true)
            {
                var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
                if (state != null && IsConnected(state))
                {
                    return true;
                }

                if (state != null && IsClosed(state))
                {
                    return false;
                }

                var remaining = (deadline - _utcNow()).TotalMilliseconds;
                if (remaining <= 0)
                {
                    return false;
                }

                await _delay(TimeSpan.FromMilliseconds(Math.Min(PollIntervalMs, Math.Max(1, remaining))), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task<MediaSfuTransportIceState> ReadStateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _control.GetTransportIceStateAsync(_direction, _transportId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task BlockedAsync(CancellationToken cancellationToken)
        {
            if (!Current)
            {
                return;
            }

            await ReportAsync("blocked", null, cancellationToken).ConfigureAwait(false);
            try
            {
                _signalling.OnNetworkBlocked?.Invoke();
            }
            catch (Exception)
            {
            }
        }

        private async Task ReportAsync(string phase, MediaSfuTransportIceState state, CancellationToken cancellationToken)
        {
            var emit = _signalling?.EmitAsync;
            if (emit == null)
            {
                return;
            }

            try
            {
                await emit(
                        "transport-ice-report",
                        new Dictionary<string, object>
                        {
                            ["transportId"] = _transportId,
                            ["direction"] = _direction == MediaSfuTransportDirection.Receive ? "recv" : "send",
                            ["phase"] = phase,
                            ["candidateType"] = OrUnknown(state?.CandidateType),
                            ["protocol"] = OrUnknown(state?.Protocol),
                            ["relayProtocol"] = OrUnknown(state?.RelayProtocol),
                            ["waitedMs"] = (long)ElapsedMs()
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Fire and forget: the report never changes the call.
            }
        }

        private double ElapsedMs()
        {
            return _startedAt == default ? 0 : Math.Max(0, (_utcNow() - _startedAt).TotalMilliseconds);
        }

        private static string OrUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static bool IsConnected(MediaSfuTransportIceState state)
        {
            return Is(state, "connected") || Is(state, "completed");
        }

        private static bool IsClosed(MediaSfuTransportIceState state)
        {
            return Is(state, "closed");
        }

        private static bool IsChecking(MediaSfuTransportIceState state)
        {
            return Is(state, "connecting") || Is(state, "checking");
        }

        private static bool Is(MediaSfuTransportIceState state, string value)
        {
            return string.Equals(state?.ConnectionState, value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SafeInvoke(Func<bool> predicate)
        {
            try
            {
                return predicate();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
