using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using SSMP.Game;
using SSMP.Game.Server.Auth;
using SSMP.Logging;
using SSMP.Networking.Transport.Common;
using Steamworks;

namespace SSMP.Networking.Transport.SteamP2P;

/// <summary>
/// Steam P2P implementation of <see cref="IEncryptedTransportServer"/>.
/// Manages multiple client connections via Steam P2P networking.
/// Optimized for maximum performance with minimal allocations.
/// </summary>
internal sealed class SteamEncryptedTransportServer : IEncryptedTransportServer {
    /// <summary>
    /// Maximum Steam P2P packet size.
    /// </summary>
    private const int MaxPacketSize = 1200;

    /// <summary>
    /// Polling interval in milliseconds for Steam P2P packet receive loop.
    /// 17.2ms achieves ~58Hz polling rate to balance responsiveness and CPU usage.
    /// </summary>
    private const double PollIntervalMS = 17.2;

    /// <summary>
    /// Maximum lag threshold before resetting timing to prevent spiral-of-death (ms).
    /// </summary>
    private const long MaxLagMS = 500;

    /// <inheritdoc />
    public event Action<IEncryptedTransportClient>? ClientConnectedEvent;

    /// <summary>
    /// Connected clients indexed by Steam ID.
    /// Uses optimal concurrency level for typical game server scenarios.
    /// </summary>
    private readonly ConcurrentDictionary<CSteamID, SteamEncryptedTransportClient> _clients = 
        new(Environment.ProcessorCount, 64);

    /// <summary>
    /// Buffer for receiving P2P packets (aligned for better cache performance).
    /// </summary>
    private readonly byte[] _receiveBuffer = new byte[MaxPacketSize];

    /// <summary>
    /// Whether the server is currently running.
    /// </summary>
    private volatile bool _isRunning;

    /// <summary>
    /// Cached flag for Steam initialization state.
    /// </summary>
    private volatile bool _steamInitialized;

    /// <summary>
    /// Callback for P2P session requests.
    /// </summary>
    private Callback<P2PSessionRequest_t>? _sessionRequestCallback;

    /// <summary>
    /// Callback for P2P session connection failures.
    /// </summary>
    private Callback<P2PSessionConnectFail_t>? _sessionConnectFailCallback;

    /// <summary>
    /// Callback for lobby member changes: a waiting request's sender may have entered the lobby.
    /// </summary>
    private Callback<LobbyChatUpdate_t>? _lobbyChatUpdateCallback;

    /// <summary>
    /// Token source for cancelling the receive loop.
    /// </summary>
    private CancellationTokenSource? _receiveTokenSource;

    /// <summary>
    /// Thread for receiving P2P packets.
    /// </summary>
    private Thread? _receiveThread;

    /// <summary>
    /// Cached loopback channel reference.
    /// </summary>
    private SteamLoopbackChannel? _cachedLoopbackChannel;

    /// <summary>
    /// Accepts only the lobby's members (locked: requests arrive on Steam's callback thread, rechecks on receive).
    /// </summary>
    private readonly LobbyGate _lobbyGate = new();

    /// <summary>
    /// Clock for a waiting request.
    /// </summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// Start listening for Steam P2P connections.
    /// </summary>
    /// <param name="port">Port parameter (unused for Steam P2P)</param>
    /// <exception cref="InvalidOperationException">Thrown if Steam is not initialized.</exception>
    public void Start(int port) {
        _steamInitialized = SteamManager.IsInitialized;

        if (!_steamInitialized) {
            throw new InvalidOperationException("Cannot start Steam P2P server: Steam is not initialized");
        }

        if (_isRunning) {
            Logger.Warn("Steam P2P server already running");
            return;
        }

        _isRunning = true;

        _sessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        _sessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(_ => AdmitWaiting());

        SteamNetworking.AllowP2PPacketRelay(true);

        Logger.Info("Steam P2P: Server started, listening for connections");

        _cachedLoopbackChannel = SteamLoopbackChannel.GetOrCreate();
        _cachedLoopbackChannel.RegisterServer(this);

        _receiveTokenSource = new CancellationTokenSource();
        _receiveThread = new Thread(ReceiveLoop) { 
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "Steam P2P Server Receive"
        };
        _receiveThread.Start();
    }

    /// <inheritdoc />
    public void Stop() {
        if (!_isRunning) return;

        Logger.Info("Steam P2P: Stopping server");

        // Signal shutdown first
        _isRunning = false;
        _receiveTokenSource?.Cancel();

        if (_receiveThread != null) {
            if (!_receiveThread.Join(5000)) {
                Logger.Warn("Steam P2P Server: Receive thread did not terminate within 5 seconds");
            }

            _receiveThread = null;
        }

        _receiveTokenSource?.Dispose();
        _receiveTokenSource = null;

        // Disconnect all clients
        foreach (var client in _clients.Values) {
            DisconnectClientInternal(client);
        }
        _clients.Clear();

        // Cleanup loopback
        if (_cachedLoopbackChannel != null) {
            _cachedLoopbackChannel.UnregisterServer();
            SteamLoopbackChannel.ReleaseIfEmpty();
            _cachedLoopbackChannel = null;
        }

        _sessionRequestCallback?.Dispose();
        _sessionRequestCallback = null;

        _sessionConnectFailCallback?.Dispose();
        _sessionConnectFailCallback = null;

        _lobbyChatUpdateCallback?.Dispose();
        _lobbyChatUpdateCallback = null;

        Logger.Info("Steam P2P: Server stopped");
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisconnectClient(IEncryptedTransportClient client) {
        if (client is SteamEncryptedTransportClient steamClient) {
            DisconnectClientInternal(steamClient);
        }
    }

    /// <summary>
    /// Internal disconnect logic to avoid interface cast overhead in hot paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DisconnectClientInternal(SteamEncryptedTransportClient steamClient) {
        var steamId = new CSteamID(steamClient.SteamId);
        if (!_clients.TryRemove(steamId, out _)) return;

        if (_steamInitialized && SteamManager.IsInitialized) {
            SteamNetworking.CloseP2PSessionWithUser(steamId);
        }

        Logger.Info($"Steam P2P: Disconnected client {steamId}");
    }

    /// <summary>
    /// Callback handler for P2P session requests.
    /// Accepts a member of the host's current lobby; a non-member waits (see <see cref="LobbyGate"/>).
    /// </summary>
    private void OnP2PSessionRequest(P2PSessionRequest_t request) {
        if (!_isRunning) return;

        var remoteSteamId = request.m_steamIDRemote;
        Logger.Info($"Steam P2P: Received session request from {remoteSteamId}");
        Admit(remoteSteamId, false);
    }

    /// <summary>
    /// Decides a session request against the lobby's members, and accepts it if it is a member's.
    /// Called on each request (Steam posts it again while the joiner keeps sending), and while a request waits, on
    /// each lobby member change and each receive pass.
    /// </summary>
    /// <param name="remoteSteamId">The sender of the request.</param>
    /// <param name="ifWaiting">Only if its request still waits (a recheck, so two rechecks never accept it twice).</param>
    private void Admit(CSteamID remoteSteamId, bool ifWaiting) {
        LobbyVerdict verdict;
        double waited;
        var on = LobbyOnly.Enabled;
        lock (_lobbyGate) {
            if (ifWaiting && !_lobbyGate.IsWaiting(remoteSteamId.m_SteamID)) return;
            verdict = _lobbyGate.Decide(
                remoteSteamId.m_SteamID, SteamManager.LobbyMembers(), _clock.Elapsed.TotalSeconds, on, out waited
            );
        }

        switch (verdict) {
            case LobbyVerdict.Accept:
                Logger.Info(
                    $"Steam P2P: Accepted session from {remoteSteamId}, " +
                    (on ? $"a member of this lobby (waited {waited:F1} s)" : "lobby-only off")
                );
                Accept(remoteSteamId);
                return;
            case LobbyVerdict.Refuse:
                Logger.Warn(
                    $"Steam P2P: Refused session from {remoteSteamId}: not a member of this lobby (waited {waited:F1} s); " +
                    "/lobbyonly off accepts any Steam player"
                );
                return;
        }
    }

    /// <summary>
    /// Decides again each request left waiting.
    /// </summary>
    private void AdmitWaiting() {
        if (!_isRunning) return;

        ulong[] waiting;
        lock (_lobbyGate) waiting = _lobbyGate.Waiting();
        foreach (var id in waiting) Admit(new CSteamID(id), true);
    }

    /// <summary>
    /// Accepts a session and creates its client connection.
    /// </summary>
    private void Accept(CSteamID remoteSteamId) {
        // If we already have a connection for this user, remove the stale entry first so the new session
        // request can replace it.
        if (_clients.TryGetValue(remoteSteamId, out var existingClient)) {
            Logger.Warn($"Steam P2P: Received new session request from already connected client {remoteSteamId} - closing stale session and accepting new request");
            DisconnectClientInternal(existingClient);
        }

        // Accept the session
        if (!SteamNetworking.AcceptP2PSessionWithUser(remoteSteamId)) {
            Logger.Warn($"Steam P2P: Failed to accept session from {remoteSteamId}");
            return;
        }

        var client = new SteamEncryptedTransportClient(remoteSteamId.m_SteamID);
        
        // Use TryAdd to handle race conditions
        if (_clients.TryAdd(remoteSteamId, client)) {
            Logger.Info($"Steam P2P: New client connected: {remoteSteamId}");
            ClientConnectedEvent?.Invoke(client);
        }
    }

    /// <summary>
    /// Callback handler for P2P session connection failures (timeouts, closed sessions, etc.).
    /// </summary>
    private void OnP2PSessionConnectFail(P2PSessionConnectFail_t result) {
        if (!_isRunning) return;

        var remoteSteamId = result.m_steamIDRemote;
        var error = (EP2PSessionError)result.m_eP2PSessionError;
        
        Logger.Info($"Steam P2P: Session connection failed for {remoteSteamId}: {error}");

        // If we have a client for this Steam ID, disconnect them
        if (_clients.TryGetValue(remoteSteamId, out var client)) {
            DisconnectClientInternal(client);
        }
    }

    /// <summary>
    /// Continuously polls for incoming P2P packets with precise timing.
    /// Uses Stopwatch + hybrid spin/sleep for consistent ~58Hz polling rate.
    /// Steam API limitation: no blocking receive or callback available for server-side, must poll.
    /// </summary>
    private void ReceiveLoop() {
        var token = _receiveTokenSource;
        if (token == null) return;

        var cancellationToken = token.Token;
        var stopwatch = Stopwatch.StartNew();
        var nextPollTime = stopwatch.ElapsedMilliseconds;
        const long pollInterval = (long)PollIntervalMS;

        using var waitHandle = new ManualResetEventSlim(false);

        while (_isRunning) {
            try {
                // Fast cancellation check without allocation
                if (cancellationToken.IsCancellationRequested) break;

                // Exit cleanly if Steam shuts down (e.g., during forceful game closure)
                if (!SteamManager.IsInitialized) {
                    _steamInitialized = false;
                    Logger.Info("Steam P2P Server: Steam shut down, exiting receive loop");
                    break;
                }

                var currentTime = stopwatch.ElapsedMilliseconds;

                // Lag spike recovery: if we're too far behind, reset to prevent spiral-of-death
                if (currentTime > nextPollTime + MaxLagMS) {
                    nextPollTime = currentTime;
                }

                var waitTime = nextPollTime - currentTime;
                if (waitTime > 0) {
                    try {
                        waitHandle.Wait((int)waitTime, cancellationToken);
                    } catch (OperationCanceledException) {
                        break;
                    }
                }

                // A request left open: its sender may have entered the lobby since
                AdmitWaiting();

                // Poll for available packets (hot path)
                ProcessIncomingPackets();

                // Schedule next poll
                nextPollTime += pollInterval;
            } catch (InvalidOperationException ex) when (ex.Message.Contains("Steamworks is not initialized")) {
                // Steam shut down during operation - exit gracefully
                _steamInitialized = false;
                Logger.Info("Steam P2P Server: Steamworks shut down during receive, exiting loop");
                break;
            } catch (Exception e) {
                Logger.Error($"Steam P2P: Error in server receive loop: {e}");
                // Continue polling on error, reset timing
                nextPollTime = stopwatch.ElapsedMilliseconds + pollInterval;
            }
        }

        Logger.Info("Steam P2P Server: Receive loop exited cleanly");
    }

    /// <summary>
    /// Processes available P2P packets.
    /// Optimized for minimal allocations and maximum throughput.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessIncomingPackets() {
        // Early exit checks (use bitwise OR to avoid branch prediction penalty)
        if (!_isRunning | !_steamInitialized) return;

        var receiveBuffer = _receiveBuffer;
        var clients = _clients;

        // Server listens for client packets on Channel 0
        while (SteamNetworking.IsP2PPacketAvailable(out var packetSize)) {
            if (!SteamNetworking.ReadP2PPacket(
                    receiveBuffer,
                    MaxPacketSize,
                    out packetSize,
                    out var remoteSteamId
                )) {
                continue;
            }

            // Fast path: direct dictionary lookup
            if (clients.TryGetValue(remoteSteamId, out var client)) {
                var size = (int)packetSize;
                
                // Allocate only for client delivery - unavoidable as client needs owned copy
                var data = new byte[size];
                Array.Copy(receiveBuffer, 0, data, 0, size);
                client.RaiseDataReceived(data, size);
            } else {
                Logger.Warn($"Steam P2P: Received packet from unknown client {remoteSteamId}");
            }
        }
    }

    /// <summary>
    /// Receives a packet from the loopback channel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReceiveLoopbackPacket(byte[] data, int length) {
        // Early exit checks (use bitwise OR to avoid branch prediction penalty)
        if (!_isRunning | !_steamInitialized) return;

        try {
            var steamId = SteamUser.GetSteamID();

            // Try to get existing client first (common case)
            if (!_clients.TryGetValue(steamId, out var client)) {
                // Create new loopback client
                client = new SteamEncryptedTransportClient(steamId.m_SteamID);
                
                // Use TryAdd to handle race conditions
                if (_clients.TryAdd(steamId, client)) {
                    ClientConnectedEvent?.Invoke(client);
                } else {
                    // Another thread added it, retrieve the instance
                    _clients.TryGetValue(steamId, out client);
                }
            }

            client?.RaiseDataReceived(data, length);
        } catch (InvalidOperationException) {
            // Steam shut down between check and API call - ignore silently
            _steamInitialized = false;
        }
    }
}
