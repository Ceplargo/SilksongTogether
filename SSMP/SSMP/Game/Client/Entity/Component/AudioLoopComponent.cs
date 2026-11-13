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
/// 69 missing at the Ant Trapper; the Dock Foremen's shield dockworkers'). (coop, 0.3.21) Each playing loop's clip goes
/// with it, and the copy's source takes it: a clip set in a state no copy runs (the pilgrims' Walk sets their idle voice)
/// was never set on the copy, which looped its prefab's (Bone_07's Pilgrim 01 sang P1_sing to the host's
/// P2_idle_loop_01).
internal class AudioLoopComponent : EntityComponent {
    /// <summary>(coop) The loaded clips by name, for a copy's loop to take the scene host's.</summary>
    private static readonly ByName<AudioClip> Clips = new(Resources.FindObjectsOfTypeAll<AudioClip>, clip => clip.name,
        clip => clip != null);

    /// <summary>(0.3.22) Clips holds no clip past its room: each scene change forgets them (a dictionary clear, no scan;
    /// the next unknown name scans once, as before). Holding every loaded clip kept their native audio memory for the
    /// session.</summary>
    static AudioLoopComponent() {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (_, _) => Clips.Clear();
    }

    /// <summary>The sound sources on the host object, in hierarchy order.</summary>
    private readonly AudioSource[] _hostSources;

    /// <summary>The sound sources on the client clone, in hierarchy order.</summary>
    private readonly AudioSource[] _clientSources;

    /// <summary>Whether each host source last played a loop, as last sent.</summary>
    private readonly bool[] _lastPlaying;

    /// <summary>(coop) The clip each host source last played a loop of, as last sent ("" for none).</summary>
    private readonly string[] _lastClips;
    private readonly LoopStarts[] _sourceStarts;

    /// <summary>(coop) The starts on the client clone's sources, for when the copy last played each (none for a source
    /// the clone lacks).</summary>
    private readonly LoopStarts?[] _copyStarts;
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
        _lastClips = hostSources.Select(ClipName).ToArray();
        _sourceStarts = hostSources.Select(LoopStarts.Watch).ToArray();
        _copyStarts = LoopStarts.WatchCopies(clientSources);
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

    /// <summary>(coop) The name of the clip a source holds, or "" for none.</summary>
    private static string ClipName(AudioSource? source) => source != null && source.clip != null ? source.clip.name : "";

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
            var clip = ClipName(_hostSources[i]);
            if (playing != _lastPlaying[i] || (playing && (starts != _lastSentStarts[i] || clip != _lastClips[i]))) {
                changed = true;
            }
            _lastPlaying[i] = playing;
            _lastSentStarts[i] = starts;
            _lastClips[i] = clip;
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
            data.Packet.Write(_lastClips[i]);
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
            var clip = data.Packet.ReadString();
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

            // the scene host's loop plays another clip: the copy takes it and plays it from its start
            if (playing && clip.Length > 0 && clip != ClipName(source) && FollowsLoopClips() &&
                Clips.Find(clip) is { } found) {
                source.clip = found;
                source.Play();
                continue;
            }

            // a position is read only from a clip: on any other sound resource Unity ignores it with a warning;
            // (coop, 0.3.21) a loop the copy itself played in the last LoopStarts.StartS has not moved yet (a source the
            // clone lacks was skipped above, so its starts are there)
            var moves = source.clip == null || source.timeSamples > 0
                || LoopStarts.JustStarted(_copyStarts[i]!.PlayedAt, Time.unscaledTime);
            var looping = LoopStarts.CopyLoops(source.isPlaying, moves, wasReportedPlaying);
            if (LoopStarts.NeedsPlay(playing, looping, previous, starts, wasReportedPlaying)) {
                source.Play();
            } else if (!playing && source.isPlaying) {
                source.Stop();
                _copyStarts[i]!.Stopped();
            }
        }
    }

    /// <summary>Whether a copy's loops follow the host's. Not inlined: the enemy_loops check's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FollowsLoops() => true;

    /// <summary>(coop) Whether a copy's loops take the scene host's clip. Not inlined: delayed_voice's twin skips it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FollowsLoopClips() => true;

    /// <inheritdoc />
    public override void Destroy() {
    }
}
