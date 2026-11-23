using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.4) The hero's place sent again after it stops. ClientManager sends the place only when it changes, as update
/// data nothing resends, so a lost last update left a still player shown off on the other screen until they moved
/// (the Act 1 enemy sweep under lag: 14 rows, the copy 0.2 to 0.5 units off for 3 s; still_copy forces it).
/// </summary>
internal static class StillResend {
    /// <summary>
    /// Milliseconds after the hero's last move at which its unchanged place goes again.
    /// </summary>
    public static readonly long[] AfterMs = [100, 300];

    /// <summary>
    /// Whether the unchanged place is due to go again, sinceMoveMs after the hero's last move, with sent resends gone
    /// already since that move (counted up here).
    /// </summary>
    public static bool Due(long sinceMoveMs, ref int sent) {
        if (!Resends() || sent >= AfterMs.Length || sinceMoveMs < AfterMs[sent]) {
            return false;
        }

        sent++;
        return true;
    }

    /// <summary>
    /// The switch (CoopQA skip; still_copy's twin): false sends nothing again, as before.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Resends() => true;
}
