using System.Collections.Generic;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) Which parts of a followed object are on (its children, then its colliders, then its renderers), one bit each
/// in that order: the scene host's game sends them with the object's place and states, and the other games set their
/// copy's parts to match. A copy set straight into the host's FSM state skips what the states between switch on: the
/// Bell Beast's bells turn their collider and sprite on in Drop, so the friend's bell was missing in up to 43 of 85
/// samples; and a part picked at random (a Skull King boulder's sprite) is the host's pick.
/// </summary>
internal static class PartBits {
    /// <summary>
    /// The most parts an update carries; later ones are left as the copy's own FSM sets them.
    /// </summary>
    public const int Max = 256;

    /// <summary>
    /// The bits of the parts that are on, eight a byte, the first part lowest.
    /// </summary>
    public static byte[] Pack(IReadOnlyList<bool> on) {
        var count = on.Count < Max ? on.Count : Max;
        var bits = new byte[(count + 7) / 8];
        for (var i = 0; i < count; i++) {
            if (on[i]) {
                bits[i / 8] |= (byte) (1 << i % 8);
            }
        }

        return bits;
    }

    /// <summary>
    /// Whether an update carries the part at this index.
    /// </summary>
    public static bool Carried(int index) => index < Max;

    /// <summary>
    /// Whether the part at this index is on in the bits.
    /// </summary>
    public static bool IsOn(byte[] bits, int index) =>
        Carried(index) && index / 8 < bits.Length && (bits[index / 8] >> index % 8 & 1) != 0;
}
