namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// A change to one member of one entry of an item container, such as Tools["Straight Pin"].IsUnlocked. For
/// <see cref="MergeRule.Add"/> members the value is a delta, otherwise the new value; bools are 0 or 1.
/// </summary>
internal readonly record struct EntryChange(string Entry, string Member, int Value);
