using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) Which hits on an enemy this game notes the kind of (HealthManagerComponent.NoteOwnHit), to send with
/// the health drop the hit makes: its own hero's, and the world's (lava, spikes, another enemy), but not another
/// player's, whose own game sends that. Only the hero's were noted, so a drop from the lava went out as no hit and the
/// other games played the enemy's death alone, without its hit's flash and sounds (Bone_16's Bone Circler killed in the
/// lava: "hit: Bone Circler from Lava Box" in the host's game, none in the friend's, NOTES 368 and the project
/// 2026-11-10 14:10). Only a hit that took health is noted: a trail pop's blast, which Bone_07's Rock Roller is immune
/// to, hit the host's copy of it in the frame of the host's slash (friend first), and its kind went out with the
/// slash's drop, so the friend's game played an explosion's hit, which the roller ignores: no hit sound for that slash
/// (2026-11-11_095929-enemies-pair2, "kind 16777227"). Unity-free, so it is tested without the game.
/// </summary>
internal static class HitNotes {
    /// <summary>Whether a hit's kind is noted: local, this game's own hero's; remotePlayer, another player's; tookHealth,
    /// whether it lowered the enemy's health.</summary>
    public static bool Notes(bool local, bool remotePlayer, bool tookHealth) =>
        (local || !remotePlayer && NotesWorldHits()) && (tookHealth || !NotesOnlyDrops());

    /// <summary>The switch lava_kill's twin turns off (a skipped bool method returns false): only the hero's noted.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool NotesWorldHits() => true;

    /// <summary>The switch hit_kind_same_frame's twin turns off: a hit that took no health noted too.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool NotesOnlyDrops() => true;
}
