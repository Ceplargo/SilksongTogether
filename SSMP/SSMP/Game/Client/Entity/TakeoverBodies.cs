using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.3.23) A takeover (Entity.MakeHost) turned every original's body dynamic, for originals like Hornet 1 whose
/// FSM turns them dynamic in a state the takeover skips. An original hidden in another game never ran its FSM, so its body
/// is as the scene placed it. Ant_05b's Wave 5 Bone Hunter Child waits for its wave hidden: its FSM's hiding states (Ambush
/// Ready, Dig In, Crowd Idle, Jump In Ready) turn it kinematic and its collider off together, and the takeover replays none
/// of them, so it came out dynamic with no collider and fell through the floor. Below y -400 HealthManager.Update called
/// Die every frame, its special death only sent ZERO HP, its FSM went to Death and it never died, so the arena it counts
/// in stayed shut (takeover_arena). An original with gravity and no solid collider now comes out kinematic, as those
/// states leave it; its FSM turns it dynamic when it wakes. Unity-free, so it is tested without the game.
/// shortcut: a takeover in the middle of a jump-in arc (dynamic, its collider a trigger) holds it still until its next
/// state; send the scene host's body type with the takeover if a check ever shows one stuck.
/// shortcut: only the root's colliders count. probe_takeover_census (153 Act 1 rooms, 1 398 originals with a body,
/// 2026-11-14): at load none is dynamic on a child's solid alone, and a woken Bone Hunter Tiny turns its root collider on
/// (takeover_bodies' probe passes). One is, later: a sprung Mite Trap (Greymoor_16) lies on its child "Phys", but the new
/// runner's original never had that child switched on (a takeover carries no child's active state), so counting children
/// would not help; it comes out kinematic and lies still, and a hit's velocity would carry it through walls. Carry the
/// copy's child active states over at a takeover (0.3.24) if a check shows one.
/// </summary>
internal static class TakeoverBodies {
    /// <summary>Whether the takeover turns an original's body dynamic (else kinematic): when it has an enabled collider
    /// that is not a trigger, so it stands on the floor, or no gravity, so it cannot fall.</summary>
    public static bool MakesDynamic(bool hasSolidCollider, bool hasGravity) =>
        hasSolidCollider || !hasGravity || !KeepsUnsupportedStill();

    /// <summary>Not inlined: takeover_arena's twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsUnsupportedStill() => true;
}
