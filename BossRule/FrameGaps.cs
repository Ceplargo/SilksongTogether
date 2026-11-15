using System.Collections.Generic;

namespace BossRule;

/// <summary>
/// (coop, 0.3.23 enemies Task 18) This game's longest frame of the last WindowS: how long it was frozen just before now.
/// A friend's catch is sent after its own game's freeze, so it reaches the scene host's game that much later than
/// LateCatches' KeepS allows, and the boss moved on while the friend was wounded through (the multi-hit row of known
/// differences, 2026-11-01). The catch carries it (MultiHitPath.Stamp) and the scene host's game keeps the box's catch
/// that much longer, up to MaxGraceS. Unity-free (times in seconds, given), so it is tested without the game.
/// </summary>
internal sealed class FrameGaps {
    /// <summary>Seconds back a frame still counts: a catch is sent in its freeze's frame, or a few frames after.</summary>
    public const float WindowS = 0.5f;

    /// <summary>The most a freeze adds to LateCatches.KeepS: past about 1 s she is in another move.</summary>
    public const float MaxGraceS = 0.65f;

    private readonly Queue<(float At, float Length)> _frames = new();

    /// <summary>A frame that ended at now and lasted length.</summary>
    public void Add(float now, float length) {
        _frames.Enqueue((now, length));
        while (_frames.Count > 0 && now - _frames.Peek().At > WindowS) _frames.Dequeue();
    }

    /// <summary>The longest frame that ended within WindowS of now, 0 if none.</summary>
    public float Longest(float now) {
        var longest = 0f;
        foreach (var (at, length) in _frames) {
            if (now - at <= WindowS && length > longest) longest = length;
        }

        return longest;
    }

    /// <summary>The time a catch reported with that freeze gets on top of KeepS.</summary>
    public static float Grace(float frozenS) => frozenS <= 0f ? 0f : frozenS < MaxGraceS ? frozenS : MaxGraceS;
}
