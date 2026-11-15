using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class CorpseFusesTests {
    // the friend's copy of a Dock Bomber's corpse, as SSMP's death names it
    [Fact]
    public void A_dock_bombers_corpse_waits_for_the_hosts() {
        var fuse = CorpseFuses.Of("Corpse Dock Bomber(Clone)");
        Assert.NotNull(fuse);
        Assert.Equal("Explode Antic", fuse.Value.Held);
        Assert.Equal("Explode", fuse.Value.Next);
    }

    // a corpse SSMP does not follow keeps its own clock
    [Fact]
    public void Another_corpse_keeps_its_own_clock() => Assert.Null(CorpseFuses.Of("Corpse Tar Slug(Clone)"));

    // the scene host's gas went off: a copy still landing or in its antic goes off now (the host's game froze while
    // it flew, so the friend's own clock ran ahead, or the friend's froze and fell behind)
    [Fact]
    public void A_copy_before_its_explosion_goes_off_with_the_hosts() {
        var fuse = CorpseFuses.Of("Corpse Dock Bomber")!.Value;
        Assert.Equal("Explode", CorpseFuses.ReleaseTo(fuse, true, "Explode Antic"));
        Assert.Equal("Explode", CorpseFuses.ReleaseTo(fuse, true, "Land"));
    }

    // let go for another reason (its room taken over or unloaded), or already gone off or in lava: no state set
    [Fact]
    public void A_copy_let_go_otherwise_keeps_its_state() {
        var fuse = CorpseFuses.Of("Corpse Dock Bomber")!.Value;
        Assert.Null(CorpseFuses.ReleaseTo(fuse, false, "Explode Antic"));
        Assert.Null(CorpseFuses.ReleaseTo(fuse, true, "Explode"));
        Assert.Null(CorpseFuses.ReleaseTo(fuse, true, "Lava"));
        Assert.Null(CorpseFuses.ReleaseTo(fuse, true, null));
    }
}
