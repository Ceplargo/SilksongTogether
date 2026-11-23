using SSMP.Game.Client.Entity;
using SSMP.Game.Client.Entity.Component;
using Xunit;

public class SpecialDeathTests {
    [Theory]
    [InlineData(true, true, false, false)] // Wasp: marked dead, still playing its scripted animation.
    [InlineData(true, true, true, true)] // A boss can remove a summon past its special death.
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, false)] // Native death was prevented, for example after the hero died.
    [InlineData(false, true, true, false)]
    public void Copy_uses_the_native_removal_path(bool markedDead, bool scripted, bool overridden, bool regular) =>
        Assert.Equal(regular, SpecialDeath.UsesRegularPath(markedDead, scripted, overridden, true));

    [Theory]
    // The Widow: her scripted death is her end (bind, Needolin, memory), which runs in the scene host's game only; her
    // copy dies the regular way, or the friend's game ran her end and took the friend into her memory (NOTES 322)
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, false)]
    public void Only_the_kinds_that_follow_it_take_the_scripted_path(bool markedDead, bool scripted, bool overridden,
        bool regular) =>
        Assert.Equal(regular, SpecialDeath.UsesRegularPath(markedDead, scripted, overridden, false));

    // (0.4 enemies, CRAWLSPACE) The Crypt Worm (Gromling): its scripted death flies, rolls and lands with its damaging
    // body still on in the scene host's game; its copy follows it, or the friend's screen ended the box at the kill
    [Fact]
    public void The_crypt_worm_follows_its_scripted_death() => Assert.True(SpecialDeath.KindFollows(EntityType.Gromling));

    [Fact]
    public void The_wasp_still_follows_and_others_do_not() {
        Assert.True(SpecialDeath.KindFollows(EntityType.WoodWasp));
        Assert.False(SpecialDeath.KindFollows(EntityType.Widow));
    }
}
