using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// The shared members of an item container (a PlayerData field such as Tools) and how each one merges. Members that
/// are not listed stay with each player (uses left, equipped slots, seen and new-indicator flags).
/// </summary>
internal sealed record ComplexFieldSpec(string FieldName, IReadOnlyDictionary<string, MergeRule> SharedMembers) {
    /// <summary>
    /// (coop, optimization) The shared members' names, taken once: a foreach over the dictionary's Keys boxes a new
    /// enumerator, which the poll's no-change test did for every entry 5 times a second (mini PC job 111).
    /// </summary>
    public string[] MemberNames { get; } = SharedMembers.Keys.ToArray();
}
