using System.Buffers;

namespace UnishoxSharp.Common;

/// <remarks>
/// mixing calling to methods from <see cref="IUnishoxDataOutput"/> and <see cref="IUnishoxTextOutput"/> is undefined behavior!
/// </remarks>
internal struct StreamOutput(Stream stream)
    : IUnishoxTextOutput, IUnishoxDataOutput
{
    private byte last = 0, bits = 0;
    public readonly Stream BaseStream { get; } = stream;
    public readonly bool LastBit => RemainingBits == 0 ?
        Position != 0 && (last & 1) != 0 :
        ((bits >> (8 - RemainingBits)) & 1) != 0;
    public int RemainingBits { get; private set; } = 0;
    public int Position { get; private set; } = 0;
    public void RepeatLast(int count)
    {
        Span<byte> buffer = stackalloc byte[Math.Min(count, 128)];
        buffer.Fill(last);
        while (count > 128)
        {
            Write(buffer);
            count -= 128;
        }
        Write(buffer[..count]);
    }
    public void CopyFrom(int offset, int count)
    {
        using IMemoryOwner<byte> mem = MemoryPool<byte>.Shared.Rent(count);
        Span<byte> buffer = mem.Memory.Span[..count];
        BaseStream.Seek(-offset, SeekOrigin.Current);
        BaseStream.ReadExactly(buffer);
        BaseStream.Seek(offset - count, SeekOrigin.Current);
        Write(buffer);
    }
    public void WriteByte(byte value)
    {
        BaseStream.WriteByte(last = value);
        Position++;
    }
    public void Write(ReadOnlySpan<byte> buffer)
    {
        BaseStream.Write(buffer);
        Position += buffer.Length;
        last = buffer[^1];
    }
    public void SeekBit(int bytePos, int bitPos)
    {
        BaseStream.Seek(bytePos - Position, SeekOrigin.Current);
        if (bitPos > 0)
        {
            bits = (byte)BaseStream.ReadByte();
            BaseStream.Seek(-1, SeekOrigin.Current);
        }
        Position = bytePos;
        RemainingBits = bitPos;
    }
    public void WriteBits(ushort bits, int count)
    {
        while (count > 0)
        {
            int blen = count > 8 ? 8 : count;
            byte a_byte = (byte)((bits & IUnishoxDataOutput.Mask[blen - 1]) >> 8);
            a_byte >>= RemainingBits;
            if (blen + RemainingBits > 8)
                blen = 8 - RemainingBits;
            if (RemainingBits == 0)
                this.bits = a_byte;
            else
                this.bits |= a_byte;
            RemainingBits += blen;
            if (RemainingBits == 8)
                FlushBits();
            bits <<= blen;
            count -= blen;
        }
    }
    public void FlushBits()
    {
        if (RemainingBits > 0)
        {
            WriteByte(bits);
            RemainingBits = 0;
        }
    }
}