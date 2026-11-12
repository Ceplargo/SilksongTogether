using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) The Silk Heart's Control FSM plays its hero's get-up sounds from clip tables
/// (PlayRandomAudioClipTable: Hero Up Memory, Play Audio on the get-up's PLAY AUDIO triggers, and its other states'
/// voice and impacts) in the game of the hero that took it. SSMP sent none, so the other games showed that hero getting
/// up silently (Bone_05 door_cinematicEnd: the host's game played two hornet_run_start from the Silk Heart, the
/// friend's none; probe_heart_getup). Her game sends each such play (its state and action), and the copies in the
/// other games play the same table from their own Silk Heart at her copy. Unity-free, so it is tested without the game.
/// </summary>
internal static class HeartGetUp {
    /// <summary>Whether a clip table played by this FSM is sent: the Silk Heart's Control.</summary>
    public static bool Sends(string? objectName, string? fsmName) => objectName == "Silk Heart" && fsmName == "Control";

    /// <summary>The effect info for a play: its state's index and its action's index in that state, a byte each.</summary>
    public static byte[]? Info(int state, int action) =>
        state is >= 0 and <= byte.MaxValue && action is >= 0 and <= byte.MaxValue ? [(byte) state, (byte) action] : null;

    /// <summary>The state and action index of an effect info, or null when it is not one.</summary>
    public static (int state, int action)? Read(byte[]? info) => info is { Length: 2 } ? (info[0], info[1]) : null;

    /// <summary>Whether a copy plays it. Not inlined: the check's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Plays() => true;
}
