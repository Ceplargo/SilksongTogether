using System.Collections.Generic;
using System.Linq;
using SSMP.Game.Client.Save;
using SSMP.Game.Client.Save.Complex;
using Xunit;

namespace SSMP.Tests;

// (0.3.22) A1: a delivery's item is the carrier's own entry of the shared Collectables container
public class DeliveryCarrierTests {
    private const string Supplies = "Courier Supplies";
    private const string Rag = "Pilgrim Rag";
    private static readonly ComplexFieldSpec Spec = ComplexFieldSpecs.All["Collectables"];
    private static readonly HashSet<string> Own = [Supplies];

    private static Dictionary<string, int> Amounts(byte[] snapshot) =>
        ProjectionOps.Decode(snapshot).Changes.Where(c => c.Member == "Amount").ToDictionary(c => c.Entry, c => c.Value);

    private static byte[] Snapshot(params (string Entry, int Amount)[] entries) =>
        ProjectionOps.Encode(true, entries.Select(e => new EntryChange(e.Entry, "Amount", e.Amount)).ToList());

    [Fact]
    public void A_delivery_items_entry_is_applied_only_to_its_carrier() {
        // the carrier's game took the delivery (5 supplies) and a rag at once
        var (shared, own) = DeliveryCarrier.Split([new EntryChange(Supplies, "Amount", 5), new EntryChange(Rag, "Amount", 1)], Own);
        Assert.Equal([new EntryChange(Rag, "Amount", 1)], shared);
        Assert.Equal([new EntryChange(Supplies, "Amount", 5)], own);

        var carrierStore = ProjectionOps.MergeIntoSnapshot(null, ProjectionOps.Encode(false, own), Spec);
        var global = ProjectionOps.MergeIntoSnapshot(Snapshot((Rag, 2)), ProjectionOps.Encode(false, shared), Spec);
        Assert.Equal(5, Amounts(DeliveryCarrier.JoinSnapshot(global, carrierStore, Own, Spec))[Supplies]);
        // the other player joins with no store of its own: none of the item, even if its own game held some
        var other = Amounts(DeliveryCarrier.JoinSnapshot(global, null, Own, Spec));
        Assert.Equal(0, other[Supplies]);
        Assert.Equal(3, other[Rag]);
    }

    [Fact]
    public void A_host_saves_own_entries_leave_the_shared_container() {
        var (shared, own) = DeliveryCarrier.Separate(Snapshot((Supplies, 4), (Rag, 2)), Own, Spec);
        Assert.Equal(new Dictionary<string, int> { [Rag] = 2 }, Amounts(shared));
        Assert.Equal(new Dictionary<string, int> { [Supplies] = 4 }, Amounts(own));
    }

    [Fact]
    public void The_carrier_record_survives_the_mod_save_written_and_read_back() {
        // the store is kept as its encoded snapshot; read back, the other games are told the same carrier
        var store = ProjectionOps.MergeIntoSnapshot(null, ProjectionOps.Encode(false, [new EntryChange(Supplies, "Amount", 5)]), Spec);
        var readBack = ProjectionOps.MergeIntoSnapshot(store, ProjectionOps.Encode(false, []), Spec);
        var tags = DeliveryCarrier.Tags(readBack, "Client");
        Assert.Equal([new EntryChange("Courier Supplies@Client", "Amount", 5)], tags);
        var (item, player) = DeliveryCarrier.Untag(tags[0].Entry)!.Value;
        Assert.Equal((Supplies, "Client"), (item, player));
        Assert.Equal("Client", DeliveryCarrier.CarrierOf(0, "Host", [new KeyValuePair<string, int>(player, tags[0].Value)]));
        Assert.Equal("Host", DeliveryCarrier.CarrierOf(5, "Host", []));
        Assert.Null(DeliveryCarrier.CarrierOf(0, "Host", [new KeyValuePair<string, int>("Client", 0)]));
        Assert.Null(DeliveryCarrier.Untag(Supplies));
    }

    [Fact]
    public void A_delivery_with_no_carrier_on_record_is_reset_on_load() {
        (string, string)[] underWay = [("Courier Delivery Bonebottom", Supplies)];
        // a 0.3.21 mod save: no carrier is on record
        Assert.Equal(["Courier Delivery Bonebottom"], DeliveryCarrier.ResetsAtStart(underWay, true, false, []));
        // carriers kept, its item left with a player who was away at the save: a missed night
        var uncarried = DeliveryCarrier.Uncarried(
            [new KeyValuePair<string, byte[]>("friend", Snapshot((Supplies, 5)))], key => key != "friend");
        Assert.Equal([Supplies], uncarried);
        Assert.Equal(["Courier Delivery Bonebottom"], DeliveryCarrier.ResetsAtStart(underWay, true, true, uncarried));
        // carried at the save, or a save only ever played alone: kept
        Assert.Empty(DeliveryCarrier.Uncarried([new KeyValuePair<string, byte[]>("friend", Snapshot((Supplies, 5)))], _ => true));
        Assert.Empty(DeliveryCarrier.ResetsAtStart(underWay, true, true, []));
        Assert.Empty(DeliveryCarrier.ResetsAtStart(underWay, false, false, []));
        // the away player's item goes with it
        Assert.Equal(0, Amounts(DeliveryCarrier.Cleared(Snapshot((Supplies, 5)), [Supplies], Spec))[Supplies]);
    }

    [Fact]
    public void A_rosary_set_is_each_players_own() {
        // (0.3.22, A2) each player in the game is paid its own set: shared, two payments would make two for everyone
        var own = DeliveryCarrier.OwnNames([Supplies]);
        Assert.Contains("Rosary_Set_Medium", own);
        Assert.Contains("Rosary_Set_Large", own);
        Assert.Contains(Supplies, own);
        var (shared, owned) = DeliveryCarrier.Split([new EntryChange("Rosary_Set_Medium", "Amount", 1)], own);
        Assert.Empty(shared);
        Assert.Single(owned);
        // a player with no store of its own joins with none, whatever its own game held: an old save's friend starts at 0
        Assert.Equal(0, Amounts(DeliveryCarrier.JoinSnapshot(Snapshot(("Rosary_Set_Medium", 2)), null, own, Spec))["Rosary_Set_Medium"]);
    }

    [Fact]
    public void A_non_delivery_collectable_is_shared_as_before() {
        var (shared, own) = DeliveryCarrier.Split([new EntryChange(Rag, "Amount", 3)], Own);
        Assert.Equal([new EntryChange(Rag, "Amount", 3)], shared);
        Assert.Empty(own);
        Assert.Equal(3, Amounts(DeliveryCarrier.JoinSnapshot(Snapshot((Rag, 3)), null, Own, Spec))[Rag]);
    }
}
