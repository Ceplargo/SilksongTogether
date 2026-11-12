using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.21) An entity hooks its host FSMs' actions by instance when it is made. A template FSM's own Init
/// (PlayMakerFSM.InitTemplate, at its Awake) builds a new Fsm with new actions after that, so none of its actions was
/// sent: the Pilgrims' pilgrim_behaviour, whose silk never reached the other games (only 17 of the Pilgrim's 295 actions
/// were the hooked ones). An entity whose FSM now holds another Fsm than the one it hooked hooks it again.
/// Unity-free, so it is tested without the game.
/// </summary>
internal static class TemplateRehook {
    /// <summary>Whether an FSM must be hooked again: it was hooked as one Fsm and now holds another.</summary>
    public static bool Needed(object? hooked, object? live) =>
        hooked != null && live != null && !ReferenceEquals(hooked, live);

    /// <summary>Whether rebuilt FSMs are hooked again. Not inlined: enemy_plays' twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Rehooks() => true;
}
