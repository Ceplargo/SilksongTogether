namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// How a shared member of an item container entry merges the values of several players.
/// </summary>
internal enum MergeRule {
    /// <summary>
    /// The highest value wins, so a flag once set stays set (owned, collected).
    /// </summary>
    Max,

    /// <summary>
    /// Changes add up, for counts that two players can raise at the same moment.
    /// </summary>
    Add,

    /// <summary>
    /// The latest value wins.
    /// </summary>
    Last
}
