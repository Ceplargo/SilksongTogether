using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.4 enemies A) Whether a SetRotation on an object is replayed to the copies. An entity's own turn was left to
/// its RotationComponent, which reads the scene host's angle in SSMP's update and sends it apart from the FSM's replayed
/// actions: a state that switches on a hurt box and turns the body (the Bell Goomba's Dive Launch: ActivateGameObject,
/// then SetRotation) reached the copy as the box first, at the old angle, and the turn a frame later (its Dive Collider
/// 1.29 by 2.3 for one frame, forced wall dives 3 of 4 with the fix on, 1 of 2 off; run 2026-11-19_061931). An entity
/// with a Rotation component now has its SetRotation replayed too, in its state's order with the box. Unity-free, so it
/// is tested without the game.
/// </summary>
internal static class OwnTurn {
    /// <summary>Whether a SetRotation on this object is replayed: any object not in the entity registry, and an entity
    /// whose registry entry gives it a Rotation component (turnsByComponent).</summary>
    public static bool Replays(bool inRegistry, bool turnsByComponent) =>
        !inRegistry || turnsByComponent && ReplaysEntityTurns();

    /// <summary>Not inlined: goomba_dive_turn's twin skips it, which gives false (an entity's own turn left to its
    /// RotationComponent, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ReplaysEntityTurns() => true;
}
