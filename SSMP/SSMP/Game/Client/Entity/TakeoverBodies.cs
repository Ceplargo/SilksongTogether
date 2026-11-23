using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// (coop, 0.4) One FSM state: the isKinematic its first SetIsKinematic2d on the enemy sets (null: none), and the
    /// states it goes to.
    /// </summary>
    public readonly record struct State(bool? Kinematic, IReadOnlyList<string> To);

    /// <summary>
    /// (coop, 0.4) Whether the runner's body was held kinematic in this state by the states before it: every way in,
    /// followed back, meets a SetIsKinematic2d of true before one of false, the FSM's start state or a global
    /// transition's target (reached from anywhere). Room_CrowCourt_02's crowd Dagger Jurors wait in Crowd Init, held
    /// kinematic in the stands by Crowd Start; the takeover never ran Crowd Start, made them dynamic (each has a solid
    /// collider) and all three fell out of the room (the takeover pass, 2026-11-16). A state with its own
    /// SetIsKinematic2d sets the body itself when entered.
    /// </summary>
    public static bool HeldByWayIn(string current, string start, IReadOnlyCollection<string> globalTargets,
        IReadOnlyDictionary<string, State> states) =>
        KeepsWayInHold() && SetByWayIn(true, current, start, globalTargets, states);

    /// <summary>
    /// (coop, 0.4) The mirror of <see cref="HeldByWayIn"/>: whether the runner's body was made dynamic in this state by
    /// the states before it (every way in meets a SetIsKinematic2d of false before one of true, the start state or a
    /// global target). Greymoor_16's sprung Mite Trap (Sprung, after Already Sprung, Activated or Snap 1) lies on its
    /// child Phys with no root solid, so MakesDynamic's root-only rule held it kinematic, and a hit sent it through the
    /// ceiling (takeover_rooms, 2026-11-17). Entity.MakeHost uses it only for a body a child's active solid holds up.
    /// </summary>
    public static bool ReleasedByWayIn(string current, string start, IReadOnlyCollection<string> globalTargets,
        IReadOnlyDictionary<string, State> states) =>
        ReleasesByWayIn() && SetByWayIn(false, current, start, globalTargets, states);

    private static bool SetByWayIn(bool kinematic, string current, string start,
        IReadOnlyCollection<string> globalTargets, IReadOnlyDictionary<string, State> states) {
        if (!states.TryGetValue(current, out var here) || here.Kinematic != null) return false;

        var held = false;
        var seen = new HashSet<string> { current };
        var ring = new List<string> { current };
        while (ring.Count > 0) {
            if (ring.Any(name => name == start || globalTargets.Contains(name))) return false;

            var next = new List<string>();
            foreach (var (name, state) in states) {
                if (!state.To.Any(ring.Contains)) continue;
                if (state.Kinematic == !kinematic) return false;
                if (state.Kinematic == kinematic) held = true;
                else if (seen.Add(name)) next.Add(name);
            }

            ring = next;
        }

        return held;
    }

    /// <summary>Not inlined: takeover_rooms' twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsWayInHold() => true;

    /// <summary>Not inlined: takeover_rooms' twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ReleasesByWayIn() => true;
}
