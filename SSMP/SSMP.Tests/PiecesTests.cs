using System;
using System.Collections.Generic;
using System.Linq;
using SSMP.Networking.Packet;
using Xunit;

namespace SSMP.Tests;

public class PiecesTests {
    private const int Mtu = 1200;

    // A packet as the update manager sends it: its ushort length (little-endian), then its bytes
    private static byte[] Packet(int size, byte seed) {
        var raw = new byte[2 + size];
        raw[0] = (byte) size;
        raw[1] = (byte) (size >> 8);
        for (var i = 2; i < raw.Length; i++) raw[i] = (byte) (seed + i * 7);
        return raw;
    }

    private static List<byte[]> Whole(PieceAssembler assembler, IEnumerable<byte[]> datagrams) =>
        datagrams.Select(d => assembler.Add(d, d.Length)).Where(w => w != null).Select(w => w!).ToList();

    [Fact]
    public void A_big_packet_comes_back_whole_from_its_pieces() {
        var raw = Packet(5000, 1);
        var pieces = Pieces.Split(raw, 7, Mtu);
        Assert.True(pieces.Count > 1);
        Assert.All(pieces, p => Assert.True(p.Length <= Mtu));
        Assert.All(pieces, p => Assert.True(Pieces.IsPiece(p, p.Length)));
        var whole = Whole(new PieceAssembler(), pieces);
        Assert.Single(whole);
        Assert.Equal(raw, whole[0]);
    }

    [Fact]
    public void A_whole_packet_is_never_taken_for_a_piece() {
        var raw = Packet(300, 2);
        Assert.False(Pieces.IsPiece(raw, raw.Length));
        Assert.False(Pieces.IsPiece(new byte[] { 0, 0, 1 }, 3)); // too short for a piece's header
    }

    [Fact]
    public void A_lost_piece_costs_only_that_packet() {
        var first = Pieces.Split(Packet(5000, 3), 1, Mtu);
        var second = Packet(4000, 4);
        var assembler = new PieceAssembler();
        // the first packet's second piece is lost on the way
        var whole = Whole(assembler, first.Where((_, i) => i != 1).Concat(Pieces.Split(second, 2, Mtu)));
        Assert.Single(whole);
        Assert.Equal(second, whole[0]);
    }

    [Fact]
    public void Pieces_out_of_order_still_make_the_packet() {
        var raw = Packet(5000, 5);
        var pieces = Pieces.Split(raw, 9, Mtu);
        pieces.Reverse();
        var whole = Whole(new PieceAssembler(), pieces);
        Assert.Single(whole);
        Assert.Equal(raw, whole[0]);
    }

    [Fact]
    public void A_piece_again_after_its_packet_came_whole_is_ignored() {
        var pieces = Pieces.Split(Packet(5000, 3), 9, Mtu);
        var assembler = new PieceAssembler();
        Assert.Single(Whole(assembler, pieces));
        Assert.Empty(Whole(assembler, pieces)); // every piece sent again: no second packet
        var next = Pieces.Split(Packet(5000, 4), 10, Mtu);
        Assert.Equal([Packet(5000, 4)], Whole(assembler, next));
    }

    [Fact]
    public void A_late_piece_of_an_older_packet_is_ignored() {
        var older = Pieces.Split(Packet(5000, 6), 65535, Mtu); // the number wraps: 0 comes after 65535
        var newer = Pieces.Split(Packet(5000, 7), 0, Mtu);
        var assembler = new PieceAssembler();
        var whole = Whole(assembler, new[] { newer[0], older[2] }.Concat(newer.Skip(1)));
        Assert.Single(whole);
        Assert.Equal(Packet(5000, 7), whole[0]);
    }

    [Fact]
    public void A_piece_with_a_broken_header_is_dropped() {
        var pieces = Pieces.Split(Packet(5000, 8), 3, Mtu);
        var assembler = new PieceAssembler();
        var badIndex = (byte[]) pieces[0].Clone();
        badIndex[4] = badIndex[5]; // the index past the count
        var otherCount = (byte[]) pieces[1].Clone();
        otherCount[5]++; // a count that differs from its packet's other pieces
        Assert.Null(assembler.Add(badIndex, badIndex.Length));
        Assert.Null(assembler.Add(pieces[0], pieces[0].Length));
        Assert.Null(assembler.Add(otherCount, otherCount.Length));
        // the packet with the changed count started over: its own pieces again make it whole
        Assert.Single(Whole(new PieceAssembler(), pieces));
    }

    [Fact]
    public void Where_SSMP_sends_nothing_again_every_piece_goes_reliably() {
        // Steam P2P: a reliable packet's piece overtaken by a newer packet's would be lost for good
        Assert.True(Pieces.SendsReliably(reliableData: false, ssmpResends: false));
        Assert.True(Pieces.SendsReliably(reliableData: true, ssmpResends: false));
        // UDP and hole punch: SSMP sends lost reliable data again, so an unreliable packet's pieces stay unreliable
        Assert.False(Pieces.SendsReliably(reliableData: false, ssmpResends: true));
        Assert.True(Pieces.SendsReliably(reliableData: true, ssmpResends: true));
    }

    [Fact]
    public void A_packet_too_big_for_its_pieces_is_refused() {
        Assert.Throws<ArgumentException>(() => Pieces.Split(new byte[(Mtu - Pieces.HeaderSize) * 256], 1, Mtu));
    }
}
