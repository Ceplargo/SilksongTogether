using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BossRule;

internal static partial class QuestRewards {
    /// <summary>
    /// Whether another player's hand-in pays this player too. Not inlined: quest_rewards' twin skips it in both games,
    /// which gives false: only the player who hands in is paid, as in 0.3.21.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PaysEveryone() => true;

    /// <summary>When this game last handed in each quest.</summary>
    private static readonly Dictionary<string, double> HandIns = new();

    /// <summary>Other players' notes waiting for the lock's answer on this game's own hand-in of the same quest.</summary>
    private static readonly List<string> Waiting = [];

    /// <summary>SSMP's ClaimHooks.PutBackAt(marker), or null when it is missing.</summary>
    private static readonly MethodInfo? PutBackAt = typeof(SSMP.Api.Client.ClientAddon).Assembly
        .GetType("SSMP.Game.Client.Save.ClaimHooks")?.GetMethod("PutBackAt", BindingFlags.Public | BindingFlags.Static);

    /// <summary>SSMP's ClaimHooks.OpenMarker(), or null when it is missing.</summary>
    private static readonly MethodInfo? OpenMarker = typeof(SSMP.Api.Client.ClientAddon).Assembly
        .GetType("SSMP.Game.Client.Save.ClaimHooks")?.GetMethod("OpenMarker", BindingFlags.Public | BindingFlags.Static);

    /// <summary>This game handed in a quest (its TryEndQuest ended it).</summary>
    public static void HandedIn(string questKey) => HandIns[questKey] = Time.unscaledTimeAsDouble;

    /// <summary>Another player handed in a quest: pay this player its reward too, when it is money or a rosary set.</summary>
    /// <param name="questKey">The quest's asset name.</param>
    public static void Received(string questKey) {
        if (!PaysEveryone()) {
            return;
        }

        switch (Decide(questKey)) {
            case Payout.Pay:
                Pay(questKey);
                break;
            case Payout.Wait:
                Waiting.Add(questKey);
                break;
            default:
                Debug.Log($"[BossRule] Quest reward: {questKey}, handed in here too and kept here first: not paid again");
                break;
        }
    }

    /// <summary>Each frame: the waiting notes whose lock answer is in, or whose wait is over.</summary>
    public static void Tick() {
        for (var i = Waiting.Count - 1; i >= 0; i--) {
            var questKey = Waiting[i];
            var payout = Decide(questKey);
            if (payout == Payout.Wait) {
                continue;
            }

            Waiting.RemoveAt(i);
            if (payout == Payout.Pay) {
                Pay(questKey);
            } else {
                Debug.Log($"[BossRule] Quest reward: {questKey}, handed in here too and kept here first: not paid again");
            }
        }
    }

    private static Payout Decide(string questKey) {
        double? own = HandIns.TryGetValue(questKey, out var at) ? at : null;
        double? back = null;
        if (own != null && PutBackAt?.Invoke(null, [$"quest:{questKey}"]) is double putBack && putBack >= 0) {
            back = putBack;
        }

        var open = OpenMarker?.Invoke(null, null) as string == $"quest:{questKey}";
        return OnNote(Time.unscaledTimeAsDouble, own, back, open);
    }

    private static void Pay(string questKey) {
        try {
            if (QuestManager.GetQuest(questKey) is not { } quest || quest.RewardItem is not { } reward ||
                !PaysOthers(reward.GetType().Name, reward.name, reward.IsUnique)) {
                return;
            }

            var count = quest.RewardCount;
            Debug.Log($"[BossRule] Quest reward: {questKey}, handed in by another player, pays {count} {reward.name} here too");
            reward.Get(count);
        } catch (Exception e) {
            Debug.LogError($"[BossRule] Could not pay {questKey}'s reward here: {e}");
        }
    }
}
