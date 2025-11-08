namespace UnishoxSharp.Common;

internal ref struct SpanOutput(Span<byte> span) : IUnishoxTextOutput, IUnishoxDataOutput
{
    public Span<byte> BaseSpan { get; } = span;
    public int RemainingBits { get; private set; } = 0;
    public int Position { get; private set; } = 0;
    public void RepeatLast(int count)
    {
        BaseSpan.Slice(Position, count).Fill(BaseSpan[Position - 1]);
        Position += count;
    }
    public void CopyFrom(int offset, int count) => Write(BaseSpan.Slice(Position - offset, count));
    public void WriteByte(byte value) => BaseSpan[Position++] = value;
    public void Write(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(BaseSpan[Position..]);
        Position += buffer.Length;
    }
    public void SeekBit(int bytePos, int bitPos)
    {
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
                BaseSpan[Position] = a_byte;
            else
                BaseSpan[Position] |= a_byte;
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
            Position++;
            RemainingBits = 0;
        }
    }
}