using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class PersonalCocoonTests {
    [Theory]
    [InlineData("Hornet Cocoon Corpse(Clone)", true)]
    [InlineData("Hornet Cocoon Corpse", true)]
    [InlineData("MossBone Cocoon", false)]
    [InlineData("Hero Corpse Marker (2)", false)]
    [InlineData("Corpse Hornet", false)]
    public void NamesOnlyHornetsCocoon(string name, bool expected) => Assert.Equal(expected, PersonalCocoon.Is(name));
}
