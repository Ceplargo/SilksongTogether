using System.Collections.Generic;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// The shared members of an item container (a PlayerData field such as Tools) and how each one merges. Members that
/// are not listed stay with each player (uses left, equipped slots, seen and new-indicator flags).
/// </summary>
internal sealed record ComplexFieldSpec(string FieldName, IReadOnlyDictionary<string, MergeRule> SharedMembers);
