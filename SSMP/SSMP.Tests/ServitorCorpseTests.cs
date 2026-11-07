using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class ServitorCorpseTests {
    [Fact]
    public void The_servitors_own_preinstantiated_corpse_is_kept() {
        Assert.True(ServitorCorpse.KeepsServitorCorpse("Corpse Weaver Servitor"));
    }

    [Theory]
    [InlineData("Weaver Servitor")]
    [InlineData("Corpse Weaver Servitor Large")]
    [InlineData("Corpse Last Judge")]
    [InlineData("")]
    public void No_other_registered_child_gets_this_exception(string name) {
        Assert.False(ServitorCorpse.KeepsServitorCorpse(name));
    }
}
