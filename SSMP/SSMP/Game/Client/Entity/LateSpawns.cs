using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.23) An update for an entity this game does not have yet waits in EntityManager's pending store and is
/// replayed when the entity registers. A boss scene that loads seconds after its room (a slow PC; BossRule's ReadyWait
/// holds the fight 5 s, then starts it anyway) replayed every spawn the scene host's boss made meanwhile: Moorwing's
/// sickles that had ended there showed in the late game, standing still at their spawn place for about 0.5 s
/// (boss_late_throws). A spawn that waited longer than <see cref="AfterS"/> is left out only when it is a throw this
/// entity's ThrownComponent follows (the scene host numbered it): one still out comes back from the scene host's next
/// thrown update, at its place (ThrownCopies.NeedsCopy, RestoresMissingThrows). Every other spawn (a hazard, a bomb, a
/// puddle that lasts) replays as before, since nothing else would bring it back (the project, MR6).
/// Unity-free, so it is tested without the game.
/// </summary>
internal static class LateSpawns {
    /// <summary>Seconds an update may wait for its entity before its spawns are old: past a room's own registration,
    /// a few frames after its first updates.</summary>
    public const float AfterS = 1f;

    /// <summary>Whether an update that waited <paramref name="waitedS"/> seconds for its entity is late.</summary>
    public static bool Late(float waitedS) => waitedS > AfterS;

    /// <summary>Whether a spawn in an update is left out: the update is late, the scene host numbered the spawned
    /// object (<paramref name="number"/> not 0: its ThrownComponent follows it) and this entity follows throws.</summary>
    public static bool Skips(bool late, byte number, bool follows) =>
        late && number != 0 && follows && SkipsLateSpawns();

    /// <summary>Not inlined: boss_late_throws' twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SkipsLateSpawns() => true;
}
