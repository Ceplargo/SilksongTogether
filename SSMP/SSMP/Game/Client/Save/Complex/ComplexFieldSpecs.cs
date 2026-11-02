using System.Collections.Generic;
using System.Linq;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// The item containers that are shared, by PlayerData field name.
/// </summary>
internal static class ComplexFieldSpecs {
    /// <summary>
    /// Every shared item container and its shared members.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ComplexFieldSpec> All = new[] {
        Spec("Tools", ("IsUnlocked", MergeRule.Max)),
        Spec("ToolEquips", ("IsUnlocked", MergeRule.Max)),
        Spec("Collectables", ("Amount", MergeRule.Add), ("AmountWhileHidden", MergeRule.Add)),
        Spec("Relics", ("IsCollected", MergeRule.Max), ("IsDeposited", MergeRule.Max)),
        Spec("MementosDeposited", ("IsDeposited", MergeRule.Max)),
        Spec("MateriumCollected", ("IsCollected", MergeRule.Max)),
        Spec(
            "QuestCompletionData",
            ("IsAccepted", MergeRule.Last),
            ("CompletedCount", MergeRule.Add),
            ("IsCompleted", MergeRule.Last),
            ("WasEverCompleted", MergeRule.Max)
        ),
        Spec("QuestRumourData", ("IsAccepted", MergeRule.Last)),
        Spec("EnemyJournalKillData", ("Kills", MergeRule.Add))
    }.ToDictionary(spec => spec.FieldName);

    private static ComplexFieldSpec Spec(string fieldName, params (string Member, MergeRule Rule)[] members) =>
        new(fieldName, members.ToDictionary(member => member.Member, member => member.Rule));
}
