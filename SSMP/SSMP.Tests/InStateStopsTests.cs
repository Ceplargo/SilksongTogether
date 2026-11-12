using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class InStateStopsTests {
    /// <summary>A copy's in-state entries and the emitters left playing, as RegisterStateChange runs them.</summary>
    private sealed class Copy {
        private readonly List<(string State, object? Emitter)> _entries = [];
        public readonly HashSet<object> Playing = [];

        public void Play(string state, object emitter) {
            Playing.Add(emitter);
            _entries.Add((state, emitter));
        }

        public void Change(string entered) {
            foreach (var left in _entries.Where(e => e.State != entered).ToList()) {
                if (InStateStops.Stops(left.Emitter!, entered, _entries)) Playing.Remove(left.Emitter!);
                _entries.Remove(left);
            }
        }
    }

    // probe_goomba_walk_dust (2026-11-11_030046-pair3): each next state's play reached the copy before its change
    [Fact]
    public void Skid_then_walk_start_then_walking_leaves_the_walk_dust_playing() {
        var dust = new object();
        var copy = new Copy();
        copy.Play("Skid", dust);
        copy.Change("Skid");
        copy.Play("Walk Start", dust);
        copy.Change("Walk Start");
        copy.Play("Walking", dust);
        copy.Change("Walking");
        Assert.Contains(dust, copy.Playing);
    }

    // a state that does not play it stops it, as before (the Goomba turning from Walking to Idle)
    [Fact]
    public void A_state_without_the_emitter_stops_it() {
        var dust = new object();
        var copy = new Copy();
        copy.Play("Walking", dust);
        copy.Change("Walking");
        copy.Change("Idle");
        Assert.DoesNotContain(dust, copy.Playing);
    }

    // another emitter of the entered state keeps no other one playing
    [Fact]
    public void Only_the_shared_emitter_is_kept() {
        var (dust, sparks) = (new object(), new object());
        var copy = new Copy();
        copy.Play("Skid", sparks);
        copy.Change("Skid");
        copy.Play("Walk Start", dust);
        copy.Change("Walk Start");
        Assert.DoesNotContain(sparks, copy.Playing);
        Assert.Contains(dust, copy.Playing);
    }
}
