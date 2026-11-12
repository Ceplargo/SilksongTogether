using System;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (0.3.21) The Rock Rollers wish's scent piles (Rock Roller Dung, Rock Roller RockDebris): a player kneels at one to
/// sniff the trail to a Rock Roller (their Control FSM: Kneel, Sense Flash, Tracking). Their names hold "Rock Roller"
/// and they have a "Control" FSM, so the registry took them for the Rock Roller enemy (Flintbeetle): the other games
/// turned their own pile off for a copy whose FSMs never run, and no other player could sniff there (probe_untested,
/// probe_dung: every Marrow room's pile out in the scene host's game only). Each game keeps its own pile instead; the
/// wish they belong to is in the shared save.
/// </summary>
internal static class ScentPiles {
    private static readonly string[] Names = ["Rock Roller Dung", "Rock Roller RockDebris"];

    /// <summary>Whether an object of this name is a scent pile (a numbered copy in the room too).</summary>
    public static bool IsPile(string name) {
        foreach (var pile in Names) if (name.StartsWith(pile, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>The fix's switch (its twin skips it: the piles registered again, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Excludes() => true;
}
