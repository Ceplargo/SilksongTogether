namespace SSMP.Game.Client.Entity.Action;

/// <summary>A copied random sound's pick, Unity-free so SSMP.Tests compiles it.</summary>
internal static class PickRules {
    /// <summary>
    /// The pick (the scene host's, or this game's own) if it is in the copy's list of <paramref name="count"/> clips,
    /// else none, and the copy plays nothing: the host's weights gave none, or the lists differ.
    /// </summary>
    public static int? PickIn(int? index, int count) => index >= 0 && index < count ? index : null;
}
