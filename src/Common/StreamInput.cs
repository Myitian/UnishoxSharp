using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UnishoxSharp.Common;

[StructLayout(LayoutKind.Auto)]
public struct StreamInput(Stream stream) : IUnishoxDataInput
{
    private int last = 0;
    private bool eof = false;
    private byte read = 0;
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
        if (read == 0)
            ReadByte();
        int result = (last >> ((read - 1) << 3)) & (0x80 >> BitPos);
        if (autoForward && ++BitPos == 8)
        {
            if (read == 0)
                ReadByte();
            else
                read--;
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
        read++;
    }

    /// <param name="offset">offset out of range [-16, 0] is undefined behavior!</param>
    public int Read8Bit(bool autoForward, int offset)
    {
        int bit_pos = BitPos + offset;
        if (read == 0)
            ReadByte();
        int char_pos = read - (bit_pos >> 3) - 1;
        bit_pos &= 7;
        byte code = (byte)((last >> (char_pos << 3)) << bit_pos);
        if (char_pos == 0)
            ReadByte();
        else
            char_pos--;
        code |= (byte)(((last >> (char_pos << 3)) & 0xFF) >> (8 - bit_pos));
        if (autoForward)
        {
            read--;
            Position++;
        }
        return code;
    }
    public void Skip(int count)
    {
        int newPos = BitPos + count;
        if (read == 0)
            ReadByte();
        while (newPos > 8)
        {
            if (read == 0)
                ReadByte();
            else
                read--;
            newPos -= 8;
            Position++;
        }
        BitPos = newPos;
    }
}