using SSMP.Networking;
using Xunit;

namespace SSMP.Tests;

public class LatestThrowsTests {
    private readonly LatestThrows _throws = new();

    [Fact]
    public void A_lost_packets_throws_go_again_while_none_newer_went_out() {
        _throws.Note(7, 100);
        Assert.False(_throws.Superseded(7, 100));
        Assert.False(_throws.Superseded(8, 100));
    }

    [Fact]
    public void A_lost_packets_throws_stay_out_once_a_newer_one_went_out() {
        _throws.Note(7, 100);
        _throws.Note(7, 104);
        Assert.True(_throws.Superseded(7, 100));
        Assert.False(_throws.Superseded(7, 104));
        Assert.False(_throws.Superseded(9, 100));
    }

    [Fact]
    public void Numbers_past_the_wrap_count_as_newer() {
        _throws.Note(7, 65534);
        _throws.Note(7, 2);
        Assert.True(_throws.Superseded(7, 65534));
        Assert.False(_throws.Superseded(7, 2));
    }

    [Fact]
    public void A_reset_forgets_them() {
        _throws.Note(7, 104);
        _throws.Clear();
        Assert.False(_throws.Superseded(7, 100));
    }
}
