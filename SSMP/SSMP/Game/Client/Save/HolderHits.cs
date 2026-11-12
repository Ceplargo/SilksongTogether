using HarmonyLib;
using UnityEngine;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.3.21) Another player's hits on a BreakableHolder (a shell shard fossil), played here as the game's own hit
/// plays them (BreakableHolder.DoHit): each its sound, shake and payout, the last its break. Its shakes keep to the
/// owner's view rule as a remote hit's do (<see cref="RemoteShakes"/>).
/// </summary>
internal static class HolderHits {
    private static readonly AccessTools.FieldRef<BreakableHolder, int> HitsLeft =
        AccessTools.FieldRefAccess<BreakableHolder, int>("hitsLeft");

    private static readonly AccessTools.FieldRef<BreakableHolder, double> LastHitTime =
        AccessTools.FieldRefAccess<BreakableHolder, double>("lastHitTime");

    private static readonly System.Reflection.MethodInfo DoHit = AccessTools.Method(typeof(BreakableHolder), "DoHit",
        new[] { typeof(AttackTypes), typeof(float), typeof(GameObject), typeof(float) });

    public static int Left(BreakableHolder holder) => HitsLeft(holder);

    /// <summary>Its hits down to value, each past its hit cooldown (the doer's hits came apart in time).</summary>
    public static void Play(BreakableHolder holder, int value) {
        RemoteShakes.Begin();
        try {
            // shortcut: struck from its own place (its debris flies one way), not the hitter's side; send the side if
            // a player ever notices
            for (var hits = HitsLeft(holder) - Mathf.Max(value, 0); hits > 0; hits--) {
                LastHitTime(holder) = 0;
                if (!(bool) DoHit.Invoke(holder, new object[] { AttackTypes.Generic, 0f, holder.gameObject, 1f })) break;
            }
        } finally {
            RemoteShakes.End();
        }
    }
}
