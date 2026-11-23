using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Networking;

/// <summary>
/// (coop, 0.4 enemies Task 3 Step 5) How an update carries the reliable data of packets judged lost, and what happens to
/// one too big to send. A lost packet's resend used to copy everything it carried, its own resends too, so each late
/// packet's resend held all the ones before it: after a stall under lag every packet came late, the updates grew past
/// 65535 bytes in seconds, and the update manager kept the one it could not build, so nothing went out again and the
/// friend timed out (NOTES 535).
/// </summary>
internal static class ResendRules {
    /// <summary>
    /// Whether a lost packet's resend carries only its own reliable data, and each resend it carried under that
    /// resend's own number (which the other side drops when it already has it); and whether a late ack takes out of the
    /// next update what that packet carried. Off: the old copy of everything, resends inside resends.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Flat() => true;

    /// <summary>
    /// Whether an update too big to send goes out without its resends, or else is left out, and the next update goes
    /// out. Off: the update is kept as it is and every later send fails on it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Recovers() => true;

    /// <summary>
    /// Whether a lost packet's thrown objects (ThrownComponent's update, all of one enemy's thrown objects at once) go
    /// again only while no newer one for that enemy has gone out: the other side drops an older one unread
    /// (UpdateOrder.IsStale). Off: each lost packet's thrown objects go again until acked, and with the acks late for
    /// seconds the updates carry seconds of them (the Widow's 14 bells, 15 times a second).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DropsOldThrows() => true;

    /// <summary>
    /// Build an update. One too big to send (any build error) is, when <see cref="Recovers"/>, built again without its
    /// resends.
    /// </summary>
    /// <param name="packet">The update to build.</param>
    /// <param name="build">Builds it (into a fresh raw packet each call).</param>
    /// <param name="dropResends">Takes its resends out.</param>
    /// <param name="log">Logs an error.</param>
    /// <returns>Whether it was built; false: the caller leaves it out and, when <see cref="Recovers"/>, starts a fresh
    /// one.</returns>
    public static bool Built<T>(T packet, Action<T> build, Action<T> dropResends, Action<string> log) {
        try {
            build(packet);
            return true;
        } catch (Exception e) {
            log($"Failed to create packet: {e}");
            if (!Recovers()) {
                return false;
            }
        }

        dropResends(packet);
        try {
            build(packet);
            log("(coop) Sent the update without its resends");
            return true;
        } catch (Exception e) {
            log($"(coop) Left out an update too big to send without its resends: {e}");
            return false;
        }
    }
}

/// <summary>
/// (coop, ResendRules.DropsOldThrows) The packet number each enemy's thrown objects last went out under.
/// </summary>
internal sealed class LatestThrows {
    private readonly Dictionary<ushort, ushort> _last = new();

    /// <summary>An update with this enemy's thrown objects went out under this number.</summary>
    public void Note(ushort entity, ushort sequence) => _last[entity] = sequence;

    /// <summary>Whether a newer update with this enemy's thrown objects went out after this number.</summary>
    public bool Superseded(ushort entity, ushort sequence) =>
        _last.TryGetValue(entity, out var last) && last != sequence && (ushort) (last - sequence) < 32768;

    public void Clear() => _last.Clear();
}
