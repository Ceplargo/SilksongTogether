using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Networking.Transport.SteamP2P;

/// <summary>What the Steam host does with a P2P session request.</summary>
internal enum LobbyVerdict {
    /// <summary>A member of the host's lobby: accept the session.</summary>
    Accept,
    /// <summary>Not a member yet: leave the request open and decide again later.</summary>
    Wait,
    /// <summary>Not a member after the wait, or no lobby: refuse (log it once).</summary>
    Refuse,
    /// <summary>Refused before and still not a member: refuse without a new log line.</summary>
    StillRefused
}

/// <summary>
/// Accepts a Steam P2P session only from a member of the host's current lobby (0.4.2; before, every request was
/// accepted, lobby or not). A joiner enters the lobby before it connects, but the host's member list can update
/// after the request arrives, so a non-member waits up to <see cref="WaitSeconds"/> before it is refused. Free of
/// Steam types so it unit-tests; the caller passes the lobby's members (null for no lobby), a clock in seconds and
/// the host's /lobbyonly setting (off: every request is accepted, as before).
/// Not thread-safe: the caller locks.
/// </summary>
internal sealed class LobbyGate {
    public const double WaitSeconds = 15;

    /// <summary>Requests left open, with the time each was first seen.</summary>
    private readonly Dictionary<ulong, double> _waiting = new();

    /// <summary>Refused and not a member since, so a re-posted request logs nothing new.</summary>
    private readonly HashSet<ulong> _refused = new();

    public LobbyVerdict Decide(ulong id, IReadOnlyCollection<ulong>? members, double now, bool on, out double waited) {
        waited = _waiting.TryGetValue(id, out var since) ? now - since : 0;
        if (!on || !MembersOnly() || members != null && members.Contains(id)) {
            _waiting.Remove(id);
            _refused.Remove(id);
            return LobbyVerdict.Accept;
        }

        if (_refused.Contains(id)) return LobbyVerdict.StillRefused;

        if (members != null) {
            if (!_waiting.TryGetValue(id, out var first)) {
                _waiting[id] = now;
                return LobbyVerdict.Wait;
            }

            if (now - first <= WaitSeconds) return LobbyVerdict.Wait;
        }

        _waiting.Remove(id);
        _refused.Add(id);
        return LobbyVerdict.Refuse;
    }

    /// <summary>Whether a request from this ID is left open.</summary>
    public bool IsWaiting(ulong id) => _waiting.ContainsKey(id);

    /// <summary>The requests left open, to decide again on each receive pass.</summary>
    public ulong[] Waiting() => _waiting.Count == 0 ? System.Array.Empty<ulong>() : _waiting.Keys.ToArray();

    /// <summary>Twin switch: CoopQA skips it (false) to accept every request again, as before 0.4.2.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool MembersOnly() => true;
}
