using System;
using System.Collections.Generic;

namespace SSMP.Networking.Packet;

/// <summary>
/// (coop) How an update packet bigger than a datagram travels: in pieces, each its own datagram that starts with a
/// zero length (no packet is empty, so a whole packet's datagram never starts so), then the packet's number, this
/// piece's index and the count of pieces. Before, the pieces had no header and the receiver read them as one stream,
/// so a lost piece (or another player's datagram in between, on the server) shifted how every later datagram was read.
/// </summary>
internal static class Pieces {
    /// <summary>The zero length (2 bytes), the packet's number (2), the piece's index (1) and the count (1).</summary>
    public const int HeaderSize = 6;

    /// <summary>
    /// The datagrams, each at most <paramref name="mtu"/> bytes, for a packet's bytes (its length first, as a whole
    /// packet's datagram has them).
    /// </summary>
    public static List<byte[]> Split(byte[] packet, ushort number, int mtu) {
        var size = mtu - HeaderSize;
        var count = (packet.Length + size - 1) / size;
        if (count > byte.MaxValue) {
            throw new ArgumentException($"A packet of {packet.Length} bytes needs {count} pieces, more than {byte.MaxValue}");
        }

        var pieces = new List<byte[]>(count);
        for (var i = 0; i < count; i++) {
            var length = System.Math.Min(size, packet.Length - i * size);
            var piece = new byte[HeaderSize + length];
            piece[2] = (byte) number;
            piece[3] = (byte) (number >> 8);
            piece[4] = (byte) i;
            piece[5] = (byte) count;
            Array.Copy(packet, i * size, piece, HeaderSize, length);
            pieces.Add(piece);
        }

        return pieces;
    }

    /// <summary>
    /// Whether a packet's pieces go as reliable sends: when the packet has reliable data, and always on a transport
    /// where SSMP sends nothing again itself (Steam P2P, whose reliable messages keep their order). There a newer
    /// packet's unreliable piece could come in while a reliable one was still being resent, and the assembler, which
    /// keeps one packet at a time, would drop the reliable packet for good.
    /// </summary>
    public static bool SendsReliably(bool reliableData, bool ssmpResends) => reliableData || !ssmpResends;

    /// <summary>Whether a datagram is a piece: its first two bytes, where a whole packet has its length, are zero.</summary>
    public static bool IsPiece(byte[] data, int length) => length >= HeaderSize && data[0] == 0 && data[1] == 0;
}

/// <summary>
/// (coop) One sender's pieces back into whole packets, one packet at a time: a piece of a newer packet drops an
/// unfinished one (its reliable data is sent again), and a late piece of an older one is ignored.
/// </summary>
internal sealed class PieceAssembler {
    private int _number = -1;
    private byte[]?[] _pieces = [];
    private int _missing;
    private bool _whole;

    /// <summary>
    /// A piece in; the packet's bytes (its length first) once it is the last one missing, else null (a packet not yet
    /// whole, or a piece with a broken header).
    /// </summary>
    public byte[]? Add(byte[] data, int length) {
        if (!Pieces.IsPiece(data, length)) {
            return null;
        }

        var number = data[2] | (data[3] << 8);
        int index = data[4], count = data[5];
        if (index >= count) {
            return null;
        }

        if (number == _number && _whole) {
            return null; // a piece again after its packet came whole (a resend, a doubled datagram): delivered once
        }

        if (number != _number) {
            if (_number >= 0 && (short) (number - _number) < 0) {
                return null;
            }

            Start(number, count);
        } else if (count != _pieces.Length) {
            Start(number, count);
        }

        if (_pieces[index] == null) {
            var piece = new byte[length - Pieces.HeaderSize];
            Array.Copy(data, Pieces.HeaderSize, piece, 0, piece.Length);
            _pieces[index] = piece;
            _missing--;
        }

        if (_missing > 0) {
            return null;
        }

        var size = 0;
        foreach (var piece in _pieces) size += piece!.Length;
        var whole = new byte[size];
        var at = 0;
        foreach (var piece in _pieces) {
            Array.Copy(piece!, 0, whole, at, piece!.Length);
            at += piece.Length;
        }

        Start(number, 0);
        _whole = true;
        return whole;
    }

    private void Start(int number, int count) {
        _number = number;
        _pieces = new byte[]?[count];
        _missing = count;
        _whole = false;
    }
}
