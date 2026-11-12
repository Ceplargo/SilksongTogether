namespace SSMP.Game.Client.Save;

/// <summary>
/// (0.3.21) A Geo Rock's hits left, sent by the scene host's game, held a moment before this game's live rock takes it.
/// This game's rock also takes the hit of the other player's attack copy, a moment after the count arrives; setting
/// the count at once and then counting that hit took two hits off for one, so a rock the scene host broke on its third
/// hit broke on the second here (probe_geo_rock: the count 2 arrived 11 ms before this game's own hit made it 1). The
/// newest count waits <see cref="GraceS"/> and is set only if the live rock still differs then (a hit the copy missed).
/// </summary>
internal sealed class GeoRockHold {
    /// <summary>How long a count waits for the copy's own hit (the network and a swing's travel, well under one swing
    /// apart: 0.44 s between the sweep's swings).</summary>
    public const float GraceS = 0.5f;

    private int _value;
    private float _due = -1f;

    /// <summary>A count arrived at now; it replaces any count still waiting.</summary>
    public void Received(int value, float now) {
        _value = value;
        _due = now + GraceS;
    }

    /// <summary>The count to set on the live rock at now (whose hits left are local), or null: none waiting, not due
    /// yet, or the rock already at it.</summary>
    public int? Due(float now, int local) {
        if (_due < 0f || now < _due) return null;
        _due = -1f;
        // (0.3.21) hits only go down: a count above this game's is an older hit's, and setting it undid this game's own
        // newer hit (Bone_01c's Geo Rock 1 (1), the friend the doer, 2026-11-10_180008-world-pair3)
        return _value < local || !CountsGoDown() && _value != local ? _value : null;
    }

    /// <summary>Whether arriving counts are held (the geo_rock_hits check's twin skips it: set at once, as before).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Holds() => true;

    /// <summary>(0.3.21) Whether an arrived count breaks this game's rock: 0 on a rock that still stands here (its Hits
    /// variable set to 0 left the rock in Idle: Bone_01c's Geo Rock 1 (1), 2026-11-10_180008-world-pair3).</summary>
    public static bool Breaks(int local, int arrived) => arrived == 0 && local > 0 && CountsGoDown();

    /// <summary>Whether a held count only lowers this game's and an arrived 0 breaks a standing rock (the
    /// geo_rock_follow check's twin skips it: any differing count set, the count only, as before).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool CountsGoDown() => true;
}
