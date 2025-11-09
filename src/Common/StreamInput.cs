using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UnishoxSharp.Common;

[StructLayout(LayoutKind.Auto)]
public struct StreamInput(Stream stream) : IUnishoxDataInput
{
    private int last = 0;
    private bool read = false, eof = false;
    public Stream BaseStream { get; } = stream;
    public int BitPos { get; private set; } = 0;
    public int Position { get; private set; } = 0;

    public readonly bool CanRead(int count = 1)
    {
        if (!BaseStream.CanRead || eof)
            return false;
        if (!BaseStream.CanSeek)
            return true;
        long diff = ((BaseStream.Length - Position) << 3) - BitPos;
        return diff >= count;
    }

    public int ReadBit(bool autoForward)
    {
        if (!read)
            ReadByte();
        int result = last & (0x80 >> BitPos);
        if (autoForward && ++BitPos == 8)
        {
            ReadByte();
            BitPos = 0;
            Position++;
        }
        return result;
    }

    private void ReadByte()
    {
        int rb = BaseStream.ReadByte();
        if (rb < 0)
            eof = true;
        last = (last << 8) | (rb & 0xFF);
        read = true;
    }

    /// <param name="offset">offset out of range [24+BitPos, 0] is undefined behavior!</param>
    public int Read8Bit(bool autoForward, int offset)
    {
        int bit_pos = BitPos + offset;
        int char_pos = -(bit_pos >> 3);
        bit_pos &= 7;
        if (!read)
            ReadByte();
        byte code = (byte)((last >> (char_pos << 3)) << bit_pos);
        if (char_pos == 0)
            ReadByte();
        else
            char_pos--;
        code |= (byte)(((last >> (char_pos << 3)) & 0xFF) >> (8 - bit_pos));
        if (autoForward)
            Position++;
        return code;
    }
    public void Skip(int count)
    {
        int newPos = BitPos + count;
        if (!read)
            ReadByte();
        while (newPos > 8)
        {
            ReadByte();
            newPos -= 8;
            Position++;
        }
        BitPos = newPos;
    }
}