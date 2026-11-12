using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SSMP.Fsm;
using UnityEngine;

namespace SSMP.Game.Client;

/// <summary>
/// (coop) An enemy copy presses the bone segments its original's game sends (CopyPressRule). In the scene host's game,
/// GamePatcher's hook on BoneSegment's triggers tells each enemy (its Entity listens here) when it began or stopped
/// touching a segment, and the Entity sends it by the segment's place; in the other games the copy's own touches are
/// passed by, and its Entity presses and lets go the sent segment with the copy's box.
/// </summary>
internal static class CopyPresses {
    /// <summary>
    /// (coop) Not inlined: skipped (false), a copy presses by its own box, as before 0.3.21's fix.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool HoldsCopyPresses() => true;

    /// <summary>Whether this collider's own touches of a segment are passed by (an enemy's copy).</summary>
    internal static bool Holds(Collider2D other) =>
        HoldsCopyPresses() && CopyPressRule.Holds(
            other.GetComponentInParent<RemoteTouches>() != null,
            other.GetComponentInParent<PredictiveInterpolation>() != null
        );

    /// <summary>Each enemy's original and its Entity's send.</summary>
    private static readonly Dictionary<GameObject, Action<Vector2, bool>> Senders = new();

    internal static void Listen(GameObject original, Action<Vector2, bool> send) => Senders[original] = send;

    internal static void Stop(GameObject original) => Senders.Remove(original);

    /// <summary>The enemy original a collider belongs to, or null.</summary>
    internal static GameObject? RootOf(Collider2D other) {
        for (var t = other.transform; t != null; t = t.parent) {
            if (Senders.ContainsKey(t.gameObject)) return t.gameObject;
        }

        return null;
    }

    private static readonly FieldInfo? TouchingObjects =
        typeof(BoneSegment).GetField("touchingObjects", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>How many of the enemy's objects touch the segment.</summary>
    internal static int Count(BoneSegment segment, GameObject root) {
        if (TouchingObjects?.GetValue(segment) is not List<GameObject> touching) return 0;
        var count = 0;
        foreach (var obj in touching) {
            if (obj && obj.transform.IsChildOf(root.transform)) count++;
        }

        return count;
    }

    /// <summary>After a trigger call: the enemy's Entity sends the segment if it began or stopped touching it.</summary>
    internal static void Touched(BoneSegment segment, GameObject root, int before) {
        if (CopyPressRule.Touching(before, Count(segment, root)) is { } touching && Senders.TryGetValue(root, out var send)) {
            send(segment.transform.position, touching);
        }
    }

    /// <summary>A segment within this (units) of a sent place is that one: the segments stand still.</summary>
    private const float Reach = 0.1f;

    /// <summary>The segment at a sent place, or null.</summary>
    internal static BoneSegment? At(float x, float y) {
        var at = new Vector2(x, y);
        BoneSegment? found = null;
        var best = Reach;
        foreach (var segment in UnityEngine.Object.FindObjectsByType<BoneSegment>(FindObjectsSortMode.None)) {
            var d = Vector2.Distance(segment.transform.position, at);
            if (d <= best) {
                best = d;
                found = segment;
            }
        }

        return found;
    }

    /// <summary>Set while a sent press runs the segment's own trigger code (the hook lets it through).</summary>
    internal static bool Bypass { get; private set; }

    private static readonly MethodInfo? Enter =
        typeof(BoneSegment).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo? Exit =
        typeof(BoneSegment).GetMethod("OnTriggerExit2D", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>The copy's box presses or lets go the segment, as the game's own trigger would.</summary>
    internal static void Press(BoneSegment segment, Collider2D box, bool press) {
        if (!segment || !box || !segment.isActiveAndEnabled) return;
        Bypass = true;
        try {
            (press ? Enter : Exit)?.Invoke(segment, [box]);
        } finally {
            Bypass = false;
        }
    }
}
