using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>How an arrived break breaks the live FSM: an event sent to it, or a state it is moved into.</summary>
internal readonly record struct ArrivedBreak(string? Event, string? State);

/// <summary>
/// (coop, 0.3.21) An object that breaks by its FSM (Bone_07's tool_metal_deposit: Control's Idle takes hits by
/// ReceivedDamage and goes HIT, then BREAK at its count; Break sets its persistent "Activated") broken in one game
/// only: the other player's copied swing landed there while their own swing missed, and the arrived "Activated" only
/// set the variable on the live FSM, which stayed in Idle (the world sweep, both ways, 2026-11-10: state, solids,
/// break sound and shake in one game). An arrived break now sends the FSM its own BREAK when its state takes one, so
/// it breaks the game's own way here too. (0.3.23) The same for the two walls whose idle state takes no BREAK: a One
/// Way Wall (break_floor) broken from outside stayed standing in the other game, whose hero was out of its range
/// (probe_remask_false, 3 of 3). Unity-free, so it is tested without the game.
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
    /// How an arrived value breaks the live FSM (fsm: its name; stateEvents: its active state's events): only true now,
    /// not before; one of the Breakers whose state takes BREAK, or one of the walls in its idle state; else nothing.
    /// sameKey: how many live objects here save under its id. (0.4, stage3c) More than one, nothing: Under_27's two
    /// "Shellwood Twig Wall (1)" share one id, and the game alone breaks only the one hit (the other stands till a
    /// reload), so an arrived value cannot say which; it opened the other one (the world sweep, four orders).
    /// </summary>
    public static ArrivedBreak? Of(string name, string fsm, bool value, bool before, ICollection<string> stateEvents,
        int sameKey = 1) {
        if (!value || before || sameKey > 1 || !Breaks()) return null;
        if (stateEvents.Contains("BREAK") && (!ByName() || Breakers.Any(name.StartsWith))) return new ArrivedBreak("BREAK", null);
        // (0.3.23) the walls whose idle state takes no BREAK, by their FSM: breakable_wall_v2's Idle takes the game's
        // own QUICK BREAK; break_floor's Idle takes hits only, each checking this game's hero range, so its Break state
        // (its last hit's) is entered
        if (fsm == "breakable_wall_v2" && stateEvents.Contains("QUICK BREAK")) return new ArrivedBreak("QUICK BREAK", null);
        if (fsm == "break_floor" && stateEvents.Contains("NAIL HIT")) return new ArrivedBreak(null, "Break");
        // (0.4) Shellwood's Twig Walls (Control): Idle takes hits, the load's ACTIVATED and the game's own DESTROY (PD
        // Bool, Hit 4, End): the friend broke Shellwood_25's (2) with a copied hit lost, the host's stayed standing
        if (name.StartsWith("Shellwood Twig Wall") && stateEvents.Contains("DESTROY")) return new ArrivedBreak("DESTROY", null);
        // (0.4.1) Greymoor_06's drop platform (Drop: Idle takes DROP, then Jitter, Go to BG, Drop, Land): the load hid it
        // in place in the other game, with no fall, shake or sounds (the mini PC's jobs 143 and 144)
        if (name.StartsWith("belltown_lever_drop_plat") && fsm == "Drop" && stateEvents.Contains("DROP")) return new ArrivedBreak("DROP", null);
        return null;
    }

    /// <summary>How long a break that arrived while the object took none (mid-hit) is tried again, each frame.</summary>
    public const float RetryS = 2f;

    /// <summary>Whether an arrived value that broke nothing now is tried again (a new break: true now, not before).</summary>
    public static bool Waits(bool value, bool before, int sameKey = 1) => value && !before && sameKey <= 1 && Breaks();

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
