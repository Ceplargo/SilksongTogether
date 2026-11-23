using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (coop) Each game shuts an ordinary wave arena's gates only once its own hero is inside the arena (the owner,
/// 2026-11-20: "letting the 2nd player enter at any time; gate closes for them when they join an ongoing arena"). The
/// fight is one, shared from whichever game started it (ArenaStarts, ArenaWaves) and ended in all (ArenaWins); but an
/// arena's start shuts its gates at once (BattleScene.LockInBattle, from DoStartBattle), so a game whose hero stood
/// outside shut its hero out, or with the start only in one game shut that hero in alone (Room_CrowCourt's lever row,
/// probe_cc_lever, b-cc-before-hostff-p4.log). Now a game whose hero is outside holds that close back: its gates stay
/// open, and they shut behind its hero as it walks in, as on a normal entry. An arena with no area of its own (started by
/// a detector or a lever) shuts as the game does. Its activeDuringBattle object waits the same way: the start switches it
/// on after its pause, outside LockInBattle. So does a Gate component under the arena, which its start event closes
/// (EventRegister: Room_CrowCourt's lever gate, grey_lever_gate crowcourt01, shut in a held game anyway: probe_gate_path
/// b-gt-gate0-p4.log, probe_lever_gate b-gt2-lever-p4.log).
/// </summary>
internal static class ArenaGates {
    private static readonly List<BattleScene> Held = new();
    private static readonly List<Gate> HeldGates = new();
    private static bool _letting;

    /// <summary>
    /// Whether a game's gates wait for its own hero. Not inlined: arena_join's twin switches it off with a patch, and a
    /// hero at the door is shut out again.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool GatesWaitForHero() => true;

    /// <summary>Prefix on <c>BattleScene.LockInBattle</c>: hold the close back while this game's hero is outside.</summary>
    public static bool LockPrefix(BattleScene __instance) {
        if (_letting || !GatesWaitForHero()) return true;
        var area = __instance.GetComponents<Collider2D>();
        var room = __instance.gameObject.scene.name;
        if (ArenaGate.ShutsNow(InSession(), ArenaWins.CatalogRoom?.Invoke(room) != false,
                StartGate.BattleCompleted(__instance), area.Length > 0, HeroInside(area))) {
            return true;
        }

        if (!Held.Contains(__instance)) Held.Add(__instance);
        Debug.Log($"[BossRule] The arena {__instance.name} in {room} started with this game's hero outside: its gates " +
                  $"stay open until the hero walks in ({Time.unscaledTime:F3})");
        return false;
    }

    /// <summary>
    /// Prefix on <c>Gate.ForceClose</c>, where Gate.Close ends too: a gate under a held arena waits for this game's hero.
    /// Its start event calls ForceClose straight from an EventResponder's UnityEvent (probe_lever_gate on 53d8f12e,
    /// b-gt3-lever-p4.log: a Gate.Close prefix never ran, the lever gate shut).
    /// </summary>
    public static bool GateClosePrefix(Gate __instance) {
        if (_letting || !Held.Any(b => b && __instance.transform.IsChildOf(b.transform))) return true;
        if (!HeldGates.Contains(__instance)) HeldGates.Add(__instance);
        Debug.Log($"[BossRule] The gate {__instance.name} waits, open, for this game's hero ({Time.unscaledTime:F3})");
        return false;
    }

    /// <summary>
    /// Once per frame: a held arena's activeDuringBattle stays off while this game's hero is outside, and its gates and
    /// that object come on as the hero walks in; a won or gone arena is dropped (its end switches the object off itself).
    /// </summary>
    public static void Tick() {
        for (var i = Held.Count - 1; i >= 0; i--) {
            var battle = Held[i];
            if (!battle || !battle.gameObject.activeInHierarchy || StartGate.BattleCompleted(battle)) {
                Held.RemoveAt(i);
                HeldGates.RemoveAll(g => !g || !battle || g.transform.IsChildOf(battle.transform));
                continue;
            }

            if (!HeroInside(battle.GetComponents<Collider2D>())) {
                if (battle.activeDuringBattle && battle.activeDuringBattle.activeSelf) battle.activeDuringBattle.SetActive(false);
                continue;
            }

            Held.RemoveAt(i);
            if (battle.activeDuringBattle && StartGate.BattleStarted(battle)) battle.activeDuringBattle.SetActive(true);
            Debug.Log($"[BossRule] This game's hero walked into the arena {battle.name}: its gates shut " +
                      $"({Time.unscaledTime:F3})");
            _letting = true;
            try {
                battle.LockInBattle();
                foreach (var gate in HeldGates.Where(g => g && g.transform.IsChildOf(battle.transform)).ToList()) {
                    gate.ForceClose();
                    HeldGates.Remove(gate);
                }
            } finally {
                _letting = false;
            }
        }
    }

    private static bool HeroInside(Collider2D[] area) =>
        HeroController.instance is { } hero && area.Any(c => c != null && Contains(Presence.ArenaBounds(c), hero.transform.position));

    private static bool Contains(Bounds b, Vector3 p) =>
        p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;

    /// <summary>In a session (ArenaWins' scene host tests are true only then): the game alone shuts as it does.</summary>
    private static bool InSession() => ArenaWins.SceneHost?.Invoke() == true || ArenaWins.NotSceneHost?.Invoke() == true;
}
