using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

// Native Awake picks the body and corpse together; copies receive that choice without re-rolling it.
internal sealed class FlockComponent : EntityComponent {
    private static readonly FieldInfo Probability = AccessTools.Field(typeof(FlockFlyer), "activeProbability");
    private static readonly FieldInfo Appearances = AccessTools.Field(typeof(FlockFlyer), "appearances");
    private static readonly MethodInfo Flee = AccessTools.Method(typeof(FlockFlyer), "Flee");
    private readonly FlockFlyer _native;
    private readonly (tk2dSpriteAnimation Library, GameObject? Corpse)[] _choices;
    private readonly string[] _names;
    private readonly System.Action _refreshAnimation;
    private string? _sent;
    private int? _received;

    public FlockComponent(NetClient client, ushort id, HostClientPair<GameObject> objects,
        System.Action refreshAnimation) : base(client, id, objects) {
        _native = objects.Host.GetComponent<FlockFlyer>();
        _refreshAnimation = refreshAnimation;
        var choices = new List<(tk2dSpriteAnimation, GameObject?)>();
        foreach (var weighted in (Array) Appearances.GetValue(_native)) {
            var appearance = AccessTools.Field(weighted.GetType(), "Appearance").GetValue(weighted);
            var library = AccessTools.Field(appearance.GetType(), "AnimLib").GetValue(appearance) as tk2dSpriteAnimation;
            var corpse = AccessTools.Field(appearance.GetType(), "CorpsePrefab").GetValue(appearance) as GameObject;
            if (library != null) choices.Add((library, corpse));
        }
        _choices = choices.ToArray();
        _names = _choices.Select(c => c.Library.name).ToArray();
    }

    public override void OnUpdate() {
        if (IsControlled || GameObject.Host == null) return;
        var library = GameObject.Host.GetComponent<tk2dSpriteAnimator>().Library;
        if (library == null || library.name == _sent) return;
        _sent = library.name;
        var data = new EntityNetworkData { Type = EntityComponentType.Flock };
        data.Packet.Write(_sent);
        SendData(data);
    }

    public override void InitializeHost(uint sceneHostEpoch) {
        _received = null;
        _sent = null;
        OnUpdate();
    }

    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        var name = data.Packet.ReadString();
        if (!IsControlled) return;
        var choice = FlockCopies.AppearanceIndex(_names, name);
        if (choice < 0) {
            SSMP.Logging.Logger.Warn($"Unknown flock appearance '{name}' on {GameObject.Host.name}");
            return;
        }
        _received = choice;
        Apply(GameObject.Host, choice);
        if (Apply(GameObject.Client, choice)) _refreshAnimation();
    }

    private bool Apply(GameObject obj, int choice) {
        if (obj == null) return false;
        var animator = obj.GetComponent<tk2dSpriteAnimator>();
        var changed = animator.Library != _choices[choice].Library;
        animator.Library = _choices[choice].Library;
        if (obj.TryGetComponent<EnemyDeathEffects>(out var death)) death.CorpsePrefab = _choices[choice].Corpse;
        return changed;
    }

    public void ActivateHost(bool active, string clip) {
        var original = GameObject.Host;
        var position = original.transform.position;
        var scale = original.transform.localScale;
        var probability = Probability.GetValue(_native);
        // OnEnable still initializes native flight, but must not hide or resize a bird already seen by both players.
        Probability.SetValue(_native, 1f);
        try {
            original.SetActive(active);
        } finally {
            Probability.SetValue(_native, probability);
        }
        original.transform.position = position;
        original.transform.localScale = scale;
        if (_received is { } choice) Apply(original, choice);
        if (active) {
            original.GetComponent<tk2dSpriteAnimator>().Play(clip);
            if (FlockCopies.ResumesFlight(clip)) Flee.Invoke(_native, null);
        }
    }

    public override void Destroy() { }
}
