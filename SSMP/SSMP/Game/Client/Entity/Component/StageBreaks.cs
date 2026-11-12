using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.21) A followed stage object's enemy damage (its DamageEnemies, its children's included): its FSM sets it
/// on the part it picked (a Skull King boulder's Drop gives its sprite 80), which breaks the room's props it falls
/// through. The other games' copies run their own Drop, which gives the 80 to their own pick, not the shown one (the scene host's,
/// PartBits), so a bone mound the boulder broke in the scene host's game stayed whole
/// there (Bone_15's Bone_mound_break_mid_tooth (2), probe_sk_sounds). The scene host's game sends the amounts with the
/// hero damage (StageDamage), the copies take them, and a copy's damage reaches props only (Breakable): the scene host's
/// game alone hits the enemies, whose health it sends. Unity-free, so it is tested without the game.
/// </summary>
internal static class StageBreaks {
    /// <summary>
    /// Whether a target in a damager's buffer is hit: a copy's hits only props and, while <see cref="Responds"/> is on,
    /// the room's HitResponses (Bone_04's activated lever: its tinker sparked, shook and sounded under the scene host's
    /// Rock Roller bomb, not under the copy's; a remote player's attack copy already hits every HitResponse).
    /// </summary>
    public static bool Keeps(bool copy, bool prop, bool response = false) => !copy || prop || response && Responds();

    /// <summary>Each damager's amount, in order (none while <see cref="Breaks"/> is off), as StageDamage packs them.</summary>
    public static byte[] Pack(IReadOnlyList<int> amounts) => Breaks() ? Clamp(amounts) : [];

    private static byte[] Clamp(IReadOnlyList<int> amounts) {
        var packed = new byte[amounts.Count < StageDamage.Max ? amounts.Count : StageDamage.Max];
        for (var i = 0; i < packed.Length; i++) {
            packed[i] = (byte) (amounts[i] < 0 ? 0 : amounts[i] > byte.MaxValue ? byte.MaxValue : amounts[i]);
        }

        return packed;
    }

    /// <summary>The switch the twin turns off (a skipped bool method returns false): no amounts go out.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Breaks() => true;

    /// <summary>The switch copy_blast_response's twin turns off: a copy's blast misses the HitResponses again.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Responds() => true;
}
