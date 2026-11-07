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
}
