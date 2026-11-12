using System.Collections.Generic;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.21) The culled Animators under other games' thrown copies set to always animate, each with the culling
/// mode to give back. Off camera a culled Animator holds its frame, so a copy's explosion held the frame that has its
/// hero damager on and wrote it once in view, after the parts follow had turned it off: the friend's one-frame late Rock
/// Roller blast (probe_blast_follow, 2026-11-10_180442). A pooled object goes back to the pool and out again as this
/// game's own: its mode is given back when its copy ends, its room unloads or this game becomes the scene host.
/// </summary>
internal sealed class CopyCulling<TAnimator, TMode> where TAnimator : class {
    private readonly Dictionary<TAnimator, TMode> _kept = new();

    /// <summary>Whether to set it to always animate now: not if it is held already (its first mode is kept).</summary>
    public bool Hold(TAnimator animator, TMode mode) {
        if (_kept.ContainsKey(animator)) return false;
        _kept[animator] = mode;
        return true;
    }

    /// <summary>The mode to give back, if it was held; it is no longer held.</summary>
    public bool Release(TAnimator animator, out TMode mode) {
        if (_kept.TryGetValue(animator, out mode!)) return _kept.Remove(animator);
        mode = default!;
        return false;
    }
}
