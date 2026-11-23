using System;
using System.Collections.Generic;
using System.Linq;

namespace BossRule;

/// <summary>
/// (0.4 world Task 17) A memory scene's reward given once, by the game whose player set it off. Each game that followed
/// the Bell Beast's Silk Heart into its memory comes back through its own Silk Heart Memory Return, whose heart then adds
/// one to the shared silkRegenMax (AddToMaxSilkRegen) on top of the leader's write that already arrived: 0 to 2 in both
/// games, the game alone 0 to 1 (heart4-on-host-p3, heart4-on-friend-p3). A run this game followed of an entry marked
/// <c>one_reward</c> holds its reward from the follow until the hero is in a room that is neither the entry's nor a
/// memory; the leader's write brings the value. Unity-free, so BossRule.Tests compiles it.
/// </summary>
internal sealed class MemoryReward {
    private const string MemoryPrefix = "Memory_";
    private string? _entry;
    private IReadOnlyCollection<string> _scenes = Array.Empty<string>();

    /// <summary>The entry whose followed run holds this game's reward, or null.</summary>
    public string? Held => _entry;

    /// <summary>This game's run of the entry started by a follow: its reward is the leader's, if the entry says so.</summary>
    public void Followed(CutsceneEntry entry) {
        if (!entry.OneReward) return;
        _entry = entry.Name;
        _scenes = entry.Scenes;
    }

    /// <summary>This game's hero came into a room (null: between rooms, which keeps the hold).</summary>
    public void Moved(string? scene) {
        if (_entry == null || scene == null) return;
        if (!_scenes.Contains(scene) && !scene.StartsWith(MemoryPrefix, StringComparison.Ordinal)) _entry = null;
    }
}
