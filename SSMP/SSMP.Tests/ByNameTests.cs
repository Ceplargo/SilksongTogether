using System.Collections.Generic;
using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class ByNameTests {
    private sealed class Clip {
        public string Name = "";
        public bool Alive = true;
    }

    // Bone_07's Pilgrim 01: the copy looped its prefab's P1_sing where the host's played P2_idle_loop_01
    [Fact]
    public void A_loaded_clip_is_found_by_its_name() {
        var idle = new Clip { Name = "P2_idle_loop_01" };
        var names = new ByName<Clip>(() => new[] { new Clip { Name = "P1_sing" }, idle }, c => c.Name, c => c.Alive);
        Assert.Same(idle, names.Find("P2_idle_loop_01"));
    }

    [Fact]
    public void A_clip_loaded_after_a_scan_is_found() {
        var loaded = new List<Clip> { new() { Name = "P1_sing" } };
        var names = new ByName<Clip>(() => loaded, c => c.Name, c => c.Alive);
        Assert.NotNull(names.Find("P1_sing"));
        var idle = new Clip { Name = "P2_idle_loop_01" };
        loaded.Add(idle);
        Assert.Same(idle, names.Find("P2_idle_loop_01"));
    }

    [Fact]
    public void A_gone_clip_is_scanned_for_again() {
        var old = new Clip { Name = "P2_idle_loop_01" };
        var loaded = new List<Clip> { old };
        var names = new ByName<Clip>(() => loaded, c => c.Name, c => c.Alive);
        Assert.Same(old, names.Find("P2_idle_loop_01"));
        old.Alive = false;
        var fresh = new Clip { Name = "P2_idle_loop_01" };
        loaded.Add(fresh);
        Assert.Same(fresh, names.Find("P2_idle_loop_01"));
    }

    [Fact]
    public void A_missing_name_is_scanned_for_once() {
        var scans = 0;
        var names = new ByName<Clip>(() => { scans++; return new[] { new Clip { Name = "P1_sing" } }; }, c => c.Name,
            c => c.Alive);
        Assert.Null(names.Find("none"));
        Assert.Null(names.Find("none"));
        Assert.Equal(1, scans);
        Assert.NotNull(names.Find("P1_sing"));
    }

    // (0.3.22 memory) a room change forgets the last room's clips, so the cache holds none past its room
    [Fact]
    public void Clear_forgets_found_things() {
        var loaded = new List<Clip> { new() { Name = "P1_sing" } };
        var names = new ByName<Clip>(() => loaded, c => c.Name, c => c.Alive);
        Assert.NotNull(names.Find("P1_sing"));
        loaded.Clear();
        names.Clear();
        Assert.Null(names.Find("P1_sing"));
    }

    // a name missing in the last room is looked up again in the next, where its clip may be loaded
    [Fact]
    public void Clear_forgets_missed_names() {
        var scans = 0;
        var loaded = new List<Clip>();
        var names = new ByName<Clip>(() => { scans++; return loaded; }, c => c.Name, c => c.Alive);
        Assert.Null(names.Find("P2_idle_loop_01"));
        var idle = new Clip { Name = "P2_idle_loop_01" };
        loaded.Add(idle);
        names.Clear();
        Assert.Same(idle, names.Find("P2_idle_loop_01"));
        Assert.Equal(2, scans);
    }
}
