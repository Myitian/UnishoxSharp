namespace UnishoxSharp.Common;

internal struct DummyOutput() : IUnishoxTextOutput, IUnishoxDataOutput
{
    public readonly bool LastBit => false;
    public int RemainingBits { get; private set; } = 0;
    public int Position { get; private set; } = 0;
    public void RepeatLast(int count) => Position += count;
    public void CopyFrom(int offset, int count) => Position += count;
    public void WriteByte(byte value) => Position++;
    public void Write(scoped ReadOnlySpan<byte> buffer) => Position += buffer.Length;
    public void SeekBit(int bytePos, int bitPos)
    {
        Position = bytePos;
        RemainingBits = bitPos;
    }
    public void WriteBits(ushort bits, int count)
    {
        RemainingBits += count;
        Position += RemainingBits / 8;
        RemainingBits %= 8;
    }
    public void FlushBits()
    {
        if (RemainingBits > 0)
        {
            RemainingBits = 0;
            Position++;
        }
    }
}