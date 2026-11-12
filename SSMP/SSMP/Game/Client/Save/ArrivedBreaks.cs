using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.3.21) An object that breaks by its FSM (Bone_07's tool_metal_deposit: Control's Idle takes hits by
/// ReceivedDamage and goes HIT, then BREAK at its count; Break sets its persistent "Activated") broken in one game
/// only: the other player's copied swing landed there while their own swing missed, and the arrived "Activated" only
/// set the variable on the live FSM, which stayed in Idle (the world sweep, both ways, 2026-11-10: state, solids,
/// break sound and shake in one game). An arrived break now sends the FSM its own BREAK when its state takes one, so
/// it breaks the game's own way here too. Unity-free, so it is tested without the game.
/// </summary>
internal static class ArrivedBreaks {
    /// <summary>
    /// The objects whose BREAK from Idle is their own break, by how the name starts: Bone_07's deposit, and Bone_01's
    /// Collapser Small (Antic Type, as BossRule's trap follow sends it). Not the Chain Drop Platform: its BREAK is the
    /// chain slash's, straight to Plat Fall, skipping the landing's antic the trap follow gives it (probe_chain_drop,
    /// 2026-11-11_025520-pair2: the other game fell 1.1 s early without the start move, lift arrive and shakes). Tonight's
    /// final-build runs sent it to these three alone.
    /// </summary>
    private static readonly string[] Breakers = ["tool_metal_deposit", "Collapser Small"];

    /// <summary>
    /// Whether an arrived value breaks the live FSM: one of the Breakers, true now, not before, and its state takes BREAK.
    /// </summary>
    public static bool Sends(string name, bool value, bool before, IEnumerable<string> stateEvents) =>
        value && !before && Breaks() && (!ByName() || Breakers.Any(name.StartsWith)) && stateEvents.Contains("BREAK");

    /// <summary>
    /// The name check's switch (local_traps' chain-drop twin skips it, which gives false: every object breaks on an
    /// arrived value again, the Chain Drop Platform without its antic).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ByName() => true;

    /// <summary>The switch the twin skips (a skipped bool method returns false): the variable alone is set again.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Breaks() => true;
}
