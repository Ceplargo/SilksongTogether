using System.Linq;
using System.Runtime.CompilerServices;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using UnityEngine;

namespace SSMP.Game.Client.Entity.Component;

/// <inheritdoc />
/// This component starts and stops an entity's looping sounds on its copy as the scene host's start and stop: loops that
/// a component of the entity's own plays (a Crawler's or a Walker's walk and run loops, which SSMP takes off copies), so
/// no copied action plays them. The friend heard none of the walking enemies' loops (the Fields Goombas' run loop: 69 of
/// 69 missing at the Ant Trapper; the Dock Foremen's shield dockworkers').
internal class AudioLoopComponent : EntityComponent {
    /// <summary>The sound sources on the host object, in hierarchy order.</summary>
    private readonly AudioSource[] _hostSources;

    /// <summary>The sound sources on the client clone, in hierarchy order.</summary>
    private readonly AudioSource[] _clientSources;

    /// <summary>Whether each host source last played a loop, as last sent.</summary>
    private readonly bool[] _lastPlaying;
    private readonly LoopStarts[] _sourceStarts;
    private readonly uint[] _lastSentStarts;
    private readonly uint[] _lastReceivedStarts;
    private readonly bool[] _lastReceivedPlaying;
    private ushort _order;
    private int _lastOrder = -1;

    /// <summary>
    /// Whether the loops were sent once: the first update sends them all, so a copy made while a loop already plays (and
    /// a game that enters later, from the server's last data) starts it.
    /// </summary>
    private bool _sent;

    public AudioLoopComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject,
        AudioSource[] hostSources,
        AudioSource[] clientSources
    ) : base(netClient, entityId, gameObject) {
        _hostSources = hostSources;
        _clientSources = clientSources;
        _lastPlaying = hostSources.Select(source => PlaysLoop(source, false)).ToArray();
        _sourceStarts = hostSources.Select(LoopStarts.Watch).ToArray();
        _lastSentStarts = new uint[hostSources.Length];
        _lastReceivedStarts = new uint[clientSources.Length];
        _lastReceivedPlaying = new bool[clientSources.Length];
    }

    /// <summary>
    /// Whether the source plays its own looping clip: its position in that clip moves (a one-shot played on it makes it
    /// play without moving it: the Phantom's landing on her run loop's source started the loop in the friend's game),
    /// or it counted as playing it before (a loop that wraps round to its start does not stop). Its position is read only
    /// from a clip: on any other sound resource Unity ignores it with a warning, which every frame flooded the scene host's
    /// log in a long session (16,000 a round).
    /// </summary>
    private static bool PlaysLoop(AudioSource? source, bool before) =>
        source != null && source.loop && source.isPlaying && (before || source.clip == null || source.timeSamples > 0);

    /// <summary>Send loop starts and stops, including a Play call while the same loop is already playing.</summary>
    /// <inheritdoc />
    public override void OnUpdate() {
        if (IsControlled) {
            return;
        }

        var changed = false;
        for (var i = 0; i < _hostSources.Length; i++) {
            var playing = PlaysLoop(_hostSources[i], _lastPlaying[i]);
            var starts = _sourceStarts[i].Number;
            if (playing != _lastPlaying[i] || (playing && starts != _lastSentStarts[i])) {
                changed = true;
            }
            _lastPlaying[i] = playing;
            _lastSentStarts[i] = starts;
        }

        if (!changed && _sent) {
            return;
        }

        _sent = true;
        var data = new EntityNetworkData {
            Type = EntityComponentType.AudioLoop
        };
        data.Packet.Write(++_order);
        data.Packet.Write((byte) _lastPlaying.Length);
        for (var i = 0; i < _lastPlaying.Length; i++) {
            data.Packet.Write(_lastPlaying[i]);
            data.Packet.Write(_lastSentStarts[i]);
        }

        SendData(data);
    }

    /// <inheritdoc />
    protected override void InitializeHost() {
        _sent = false;
    }

    public override void InitializeClient(uint sceneHostEpoch) {
        _lastOrder = -1;
        System.Array.Clear(_lastReceivedStarts, 0, _lastReceivedStarts.Length);
        System.Array.Clear(_lastReceivedPlaying, 0, _lastReceivedPlaying.Length);
    }

    /// <summary>The copy's loops start or stop as the host's did; a source that loops nothing is left alone.</summary>
    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        var order = data.Packet.ReadUShort();
        if (UpdateOrder.IsStale(order, _lastOrder)) {
            return;
        }
        _lastOrder = order;
        var length = data.Packet.ReadByte();
        for (var i = 0; i < length; i++) {
            var playing = data.Packet.ReadBool();
            var starts = data.Packet.ReadUInt();
            if (!FollowsLoops() || i >= _clientSources.Length) {
                continue;
            }

            var previous = _lastReceivedStarts[i];
            _lastReceivedStarts[i] = starts;
            var wasReportedPlaying = _lastReceivedPlaying[i];
            _lastReceivedPlaying[i] = playing;

            // Unity's null: a source destroyed since the copy was made (a pattern match would not see it, and the throw
            // would drop the rest of the entity's data)
            var source = _clientSources[i];
            if (source == null || !source.loop) {
                continue;
            }

            if (LoopStarts.NeedsPlay(playing, source.isPlaying, previous, starts, wasReportedPlaying)) {
                source.Play();
            } else if (!playing && source.isPlaying) {
                source.Stop();
            }
        }
    }

    /// <summary>Whether a copy's loops follow the host's. Not inlined: the enemy_loops check's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FollowsLoops() => true;

    /// <inheritdoc />
    public override void Destroy() {
    }
}
