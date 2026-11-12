using SSMP.Game.Client;
using Xunit;

// Bone_04's Rock Roller row (pair 3, 2026-11-10 21:41): "spawned in the host's game only: Nail Terrain Hit Effect"
public class ThunkSparkTests {
    [Fact]
    public void A_sparks_place_comes_back_whole() {
        var at = ThunkSpark.Read(ThunkSpark.Info(-12.25f, 340.5f, 90f));
        Assert.NotNull(at);
        Assert.Equal(-12.25f, at!.Value.x);
        Assert.Equal(340.5f, at.Value.y);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(271.4f)]
    [InlineData(359.9f)]
    public void A_sparks_turn_comes_back_within_two_degrees(float angle) {
        var back = ThunkSpark.Read(ThunkSpark.Info(1f, 2f, angle))!.Value.angle;
        var off = System.Math.Abs(back - angle) % 360f;
        Assert.True(System.Math.Min(off, 360f - off) <= 2f, $"{angle} came back as {back}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    public void A_wrong_info_is_no_spark(byte[]? info) => Assert.Null(ThunkSpark.Read(info));
}
