using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MonoMod.RuntimeDetour;
using SSMP.Networking.Packet.Data;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// The clip a copied random sound plays: a boss's voice lines and other sounds come from a clip table
/// (<see cref="RandomAudioClipTable"/>) that picks at random, so each game picked its own clip and the friend heard
/// another grunt than the host at the same moment (the Last Judge, Lace, the Phantom, the Widow, Sister Splinter). The
/// scene host's game sends its table's pick with the action, and the other games' table gives that clip.
/// </summary>
internal static partial class EntityFsmActions {
    /// <summary>A clip table's last pick in this game and the frame it came in, for a copied action's network data.</summary>
    private static (RandomAudioClipTable? Table, AudioClip? Clip, int Frame) _lastPick;

    /// <summary>
    /// The scene host's pick a table gives while a copied action plays it: its clip, or none when the host's table played
    /// none (the action still stops the sound before it, as the host's did).
    /// </summary>
    private static (RandomAudioClipTable? Table, AudioClip? Clip, bool None) _forcedPick;

    /// <summary>
    /// A weighted pick's last index in this game and the frame it came in: an enemy's voice from a clip list
    /// (<see cref="AudioPlayRandomVoice"/>) picks its clip so, for a copied action's network data.
    /// </summary>
    private static (int Index, int Frame) _lastWeighted = (-1, -1);

    private static readonly AccessTools.FieldRef<RandomAudioClipTable, AudioClip> PreviousClip =
        AccessTools.FieldRefAccess<RandomAudioClipTable, AudioClip>("previousClip");

    // ReSharper disable NotAccessedField.Local
    private static Hook? _selectClipHook;
    private static Hook? _selectClipIgnoringProbabilityHook;
    private static Hook? _weightedPickHook;
    private static Hook? _audioEventRandomHook;

    /// <summary>
    /// Unity's random state just before a PlayAudioEventRandom of this game spawned its sound (and picked its clip and
    /// pitch), and the frame, for a copied action's network data.
    /// </summary>
    private static (PlayAudioEventRandom? Action, string State, int Frame) _lastEventRandom;

    /// <summary>The copied PlayAudioEventRandom that picks from the scene host's random state, and that state.</summary>
    private static (PlayAudioEventRandom? Action, string State) _replayEventRandom;
    // ReSharper restore NotAccessedField.Local

    /// <summary>
    /// Hooks the clip tables' picks; from the static constructor. A hook that fails (a game update renamed the method)
    /// is logged, and the copies pick their own clips as before: a throw here would stop every copied action.
    /// </summary>
    private static void HookClipPicks() {
        try {
            _selectClipHook = new Hook(
                typeof(RandomAudioClipTable).GetMethod(nameof(RandomAudioClipTable.SelectClip))!, PickClip
            );
            _selectClipIgnoringProbabilityHook = new Hook(
                typeof(RandomAudioClipTable).GetMethod(nameof(RandomAudioClipTable.SelectClipIgnoreProbability))!, PickClip
            );
            _weightedPickHook = new Hook(
                typeof(ActionHelpers).GetMethod(nameof(ActionHelpers.GetRandomWeightedIndex), [typeof(FsmFloat[])])!,
                WeightedPick
            );
            _audioEventRandomHook = new Hook(
                typeof(PlayAudioEventRandom).GetMethod(
                    "SpawnAudioEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                )!,
                SpawnAudioEventRandom
            );
        } catch (Exception e) {
            Logging.Logger.Error($"Could not hook the clip tables' picks, copies pick their own:\n{e}");
        }
    }

    /// <summary>
    /// A clip table's pick: the scene host's while a copy of its action plays (its cooldown and chance were the scene
    /// host's to ask), else the table's own, kept for that action's network data.
    /// </summary>
    private static AudioClip PickClip(Func<RandomAudioClipTable, bool, AudioClip> orig, RandomAudioClipTable self,
        bool forcePlay) {
        if (_forcedPick.Table == self && _forcedPick.None) {
            return null!;
        }

        if (_forcedPick.Table == self && _forcedPick.Clip != null) {
            return PreviousClip(self) = _forcedPick.Clip;
        }

        var clip = orig(self, forcePlay);
        _lastPick = (self, clip, Time.frameCount);
        return clip;
    }

    /// <summary>
    /// A PlayAudioEventRandom's spawn, where it picks its clip and pitch: a copy of the scene host's action picks from the
    /// host's random state (this game's own put back after); else Unity's state before the pick, kept for the action's
    /// network data.
    /// </summary>
    private static AudioSource SpawnAudioEventRandom(
        Func<PlayAudioEventRandom, Vector3, System.Action, AudioSource> orig,
        PlayAudioEventRandom self,
        Vector3 position,
        System.Action onRecycle
    ) {
        if (_replayEventRandom.Action != self) {
            _lastEventRandom = (self, JsonUtility.ToJson(UnityEngine.Random.state), Time.frameCount);
            return orig(self, position, onRecycle);
        }

        var own = UnityEngine.Random.state;
        UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(_replayEventRandom.State);
        try {
            return orig(self, position, onRecycle);
        } finally {
            UnityEngine.Random.state = own;
        }
    }

    /// <summary>A weighted pick in this game, kept for a copied action's network data.</summary>
    private static int WeightedPick(Func<FsmFloat[], int> orig, FsmFloat[] weights) {
        var index = orig(weights);
        _lastWeighted = (index, Time.frameCount);
        return index;
    }

    /// <summary>
    /// The scene host's weighted pick for a copied random sound whose action got as far as its pick (asked): 1, the
    /// index (-1 when its weights gave none) and the source's pitch; or 2 when not known (the copy picks its own).
    /// </summary>
    private static bool WriteWeightedPick(EntityNetworkData data, bool asked, AudioSource? source) {
        if (!CopiesPick() || !asked || _lastWeighted.Frame != Time.frameCount) {
            data.Packet.Write((byte) 2);
            return true;
        }

        data.Packet.Write((byte) 1);
        data.Packet.Write(_lastWeighted.Index);
        data.Packet.Write(source != null ? source.pitch : 1f);
        _lastWeighted = (-1, -1);
        return true;
    }

    /// <summary>The scene host's weighted pick read back: its index and pitch, or none when not known.</summary>
    private static (int Index, float Pitch)? ReadWeightedPick(EntityNetworkData? data) {
        // a start-up state's action comes with no data (EntityInitializer)
        if (data == null || data.Packet.ReadByte() != 1) {
            return null;
        }

        return (data.Packet.ReadInt(), data.Packet.ReadFloat());
    }

    /// <summary>Whether a copied random sound carries the scene host's pick.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool CopiesPick() => true;

    /// <summary>
    /// The scene host's pick for a copied random sound of this table: 0 when its table played nothing this frame (its
    /// cooldown or chance said no), 1 and the clip's name, or 2 when not known (the copy then picks its own).
    /// </summary>
    private static bool WritePick(EntityNetworkData data, RandomAudioClipTable? table) {
        if (!CopiesPick() || table == null || _lastPick.Table != table || _lastPick.Frame != Time.frameCount) {
            data.Packet.Write((byte) 2);
            return true;
        }

        var clip = _lastPick.Clip;
        _lastPick = default;
        data.Packet.Write(clip != null ? (byte) 1 : (byte) 0);
        if (clip != null) {
            data.Packet.Write(clip.name);
        }

        return true;
    }

    /// <summary>
    /// Plays a copied random sound with the scene host's pick: the action runs, its table giving nothing if the host's
    /// gave nothing, the host's clip if this game's table has it, else a pick of this game's own.
    /// </summary>
    private static void PlayWithPick(EntityNetworkData? data, RandomAudioClipTable? table, System.Action play) {
        // a start-up state's action comes with no data (EntityInitializer): it plays as it did before picks were sent
        var kind = data?.Packet.ReadByte() ?? 2;
        if (kind == 0) {
            _forcedPick = (table, null, true);
        } else if (kind == 1) {
            var name = data!.Packet.ReadString();
            _forcedPick = (table, table != null ? ClipsOf(table).FirstOrDefault(c => c != null && c.name == name) : null,
                false);
        }

        try {
            play();
        } finally {
            _forcedPick = default;
        }
    }

    /// <summary>The clips a table picks from.</summary>
    private static IEnumerable<AudioClip?> ClipsOf(RandomAudioClipTable table) =>
        (AccessTools.Field(typeof(RandomAudioClipTable), "clips")?.GetValue(table) as Array ?? Array.Empty<object>())
        .Cast<object?>()
        .Select(entry => entry == null ? null : AccessTools.Field(entry.GetType(), "Clip")?.GetValue(entry) as AudioClip);
}
