namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop) An every-frame SetScale on a copy, set again each frame its variables move. SSMP replayed it once, at the
/// state's start: Moorwing's summon states ease the FSM float Sickle Scale 0.75 to 1.5 and scale her Summon Sickle from
/// it every frame, and the eased float reached the other games (SSMP syncs an entity's FSM floats) while their sickle
/// kept its first scale: its hurt box grew 1.1 to 2.2 units in the scene host's game and stayed 1.1 in the others
/// (boss round six's probe, 4 summons of 4).
/// </summary>
internal static class ScaleFollow {
    /// <summary>
    /// Whether a value the copy's every-frame SetScale reads has moved since the frame before: only then is the scale
    /// set, so a variable nothing moves never writes over the scale the scene host sent.
    /// </summary>
    public static bool Changed(float before, float now) => before != now;

    /// <summary>
    /// The switch the twin turns off (a skipped bool method returns false): the SetScale is applied once, as before.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Follows() => true;
}
