using System.Runtime.InteropServices;

namespace UnishoxSharp.Common;

[StructLayout(LayoutKind.Auto)]
public ref struct SpanInput(ReadOnlySpan<byte> span) : IUnishoxTextInput, IUnishoxDataInput
{
    public ReadOnlySpan<byte> BaseSpan { get; } = span;
    public int BitPos { get; private set; } = 0;
    public int Position { readonly get; set; } = 0;
    public readonly int Length => BaseSpan.Length;
    public readonly int ReadByteAt(int position)
    {
        return BaseSpan[position];
    }

    public readonly bool CanRead(int count = 1)
    {
        int diff = ((BaseSpan.Length - Position) << 3) - BitPos;
        return diff >= count;
    }
    public int ReadBit(bool autoForward)
    {
        int result = BaseSpan[Position] & (0x80 >> BitPos);
#if EXDEBUG
        Console.WriteLine($"{autoForward}  {Position}+{BitPos} : {result}");
#endif
        if (autoForward && ++BitPos == 8)
        {
            BitPos = 0;
            Position++;
        }
        return result;
    }
    public int Read8Bit(bool autoForward, int offset)
    {
        ReadOnlySpan<byte> input = BaseSpan;
        int bit_pos = BitPos + offset;
        int char_pos = Position + (bit_pos >> 3);
        bit_pos &= 7;
        byte code = (byte)(input[char_pos] << bit_pos);
        char_pos++;
        if (char_pos < input.Length)
            code |= (byte)(input[char_pos] >> (8 - bit_pos));
        else
            code |= (byte)(0xFF >> (8 - bit_pos));
#if EXDEBUG
        Console.WriteLine($"{autoForward}  {Position}+{BitPos} : {code:X2}({code})");
#endif
        if (autoForward)
            Position++;
        return code;
    }
    public void Skip(int count)
    {
        int newPos = BitPos + count;
        Position += newPos >> 3;
        BitPos = newPos & 7;
    }
}