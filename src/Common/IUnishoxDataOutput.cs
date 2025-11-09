namespace UnishoxSharp.Common;

public interface IUnishoxDataOutput
{
    public static ReadOnlySpan<uint> Mask => [0x8000, 0xC000, 0xE000, 0xF000, 0xF800, 0xFC00, 0xFE00, 0xFF00];
    bool LastBit { get; }
    int RemainingBits { get; }
    int Position { get; }
    void SeekBit(int bytePos, int bitPos);
    void WriteBits(ushort bits, int count);
    void FlushBits();
}
