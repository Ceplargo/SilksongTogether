using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

internal sealed class LoopStarts {
    // Weak keys let destroyed room sources go without retaining their Unity wrappers.
    private static readonly ConditionalWeakTable<object, LoopStarts> Sources = new();

    public uint Number { get; private set; }
    private bool _unclaimedPlay;

    public static LoopStarts Watch(object source) => Sources.GetValue(source, _ => new LoopStarts());

    public static void Played(object source) {
        if (Sources.TryGetValue(source, out var starts)) {
            starts.Number = unchecked(starts.Number + 1);
            starts._unclaimedPlay = true;
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FollowsLoopRestarts() => true;
}
