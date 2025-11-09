namespace UnishoxSharp.Common;

internal struct DummyOutput() : IUnishoxTextOutput, IUnishoxDataOutput
{
    public int RemainingBits { get; private set; } = 0;
    public int Position { get; private set; } = 0;
    public void RepeatLast(int count) => Position += count;
    public void CopyFrom(int offset, int count) => Position += count;
    public void WriteByte(byte value) => Position++;
    public void Write(ReadOnlySpan<byte> buffer) => Position += buffer.Length;
    public void SeekBit(int bytePos, int bitPos)
    {
        Position = bytePos;
        RemainingBits = bitPos;
    }
    public bool WriteBits(ushort bits, int count)
    {
        RemainingBits += count;
        Position += RemainingBits / 8;
        RemainingBits %= 8;
        return true;
    }
    public bool FlushBits()
    {
        if (RemainingBits > 0)
        {
            RemainingBits = 0;
            Position++;
        }
        return true;
    }
}