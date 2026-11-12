using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

internal sealed class LoopStarts {
    // Weak keys let destroyed room sources go without retaining their Unity wrappers.
    private static readonly ConditionalWeakTable<object, LoopStarts> Sources = new();

    public uint Number { get; private set; }
    private bool _unclaimedPlay;

    /// <summary>(coop, 0.3.21) When a Play last started this source's loop (Time.unscaledTime), or never.</summary>
    public float PlayedAt { get; private set; } = float.NegativeInfinity;

    public static LoopStarts Watch(object source) => Sources.GetValue(source, _ => new LoopStarts());

    /// <summary>(coop, 0.3.21) Each copy source's starts, in its order; none for a source the clone lacks, which
    /// DamagePairs.Paired leaves null (Dock_09's Dock Guard Slasher: its Projectile's sound) and the loop follow
    /// skips.</summary>
    public static LoopStarts?[] WatchCopies(object?[] sources) =>
        sources.Select(source => source is null ? null : Watch(source)).ToArray();

    public static void Played(object source, float now) {
        if (Sources.TryGetValue(source, out var starts)) {
            starts.Number = unchecked(starts.Number + 1);
            starts._unclaimedPlay = true;
            starts.PlayedAt = now;
        }
    }

    public static void SentAction(object source) {
        if (Sources.TryGetValue(source, out var starts) && starts._unclaimedPlay && SkipsSentLoopStarts()) {
            // The reliable action already replays this Play; only other starts need the loop counter.
            starts.Number = unchecked(starts.Number - 1);
            starts._unclaimedPlay = false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool SkipsSentLoopStarts() => true;

    public static bool NeedsPlay(bool playing, bool wasPlaying, uint previous, uint current, bool wasReportedPlaying) =>
        playing && (!wasPlaying || (previous != current && FollowsLoopRestarts()
            && (wasReportedPlaying || !KeepsFirstLoopStart())));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KeepsFirstLoopStart() => true;

    /// <summary>
    /// (coop, 0.3.21) Whether a copy's source plays its loop: it plays and its clip moves, or the scene host's was playing
    /// as last sent. A one-shot on a stopped loop's source plays it without moving it: Bone_18's Pilgrim 05 lands with a
    /// one-shot on his walk loop's source, and a walk of the host's that began while the copy's land still sounded was
    /// taken for a loop already started, so the friend heard no walk (one of 11, 2026-11-10_143452-pair2).
    /// </summary>
    public static bool CopyLoops(bool isPlaying, bool moves, bool wasReportedPlaying) =>
        isPlaying && (moves || wasReportedPlaying || !SeesOneShots());

    /// <summary>Not inlined: the loop_oneshots check's twin skips it (a sounding source counts as its loop again).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool SeesOneShots() => true;

    /// <summary>
    /// (coop, 0.3.21) How long a loop a copy started itself counts as moving before its clip moves: Unity's audio
    /// starts a played clip a frame or two late, so a loop a copied FSM had just started (the beastflies' charge and
    /// fly loops, the Skull King's run loop) still stood at its first sample when the scene host's start came, and the
    /// loop follow played it again (loop_action_starts: 2 of 2 runs on ac947db, 2026-11-11).
    /// </summary>
    public const float StartS = 0.25f;

    /// <summary>(coop, 0.3.21) Whether a copy's loop played at <paramref name="playedAt"/> is still starting.</summary>
    public static bool JustStarted(float playedAt, float now) => now - playedAt < StartS;

    /// <summary>(coop, 0.3.21) The loop follow stopped this source: a one-shot on it next is no loop start.</summary>
    public void Stopped() => PlayedAt = float.NegativeInfinity;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FollowsLoopRestarts() => true;
}
