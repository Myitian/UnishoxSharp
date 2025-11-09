using UnishoxSharp.Common;

namespace UnishoxSharp.V1;

public partial class Unishox
{
    // Decoder is designed for using less memory, not speed
    // Decode lookup table for code index and length
    // First 2 bits 00, Next 3 bits indicate index of code from 0,
    // last 3 bits indicate code length in bits
    static ReadOnlySpan<byte> VCode => [2 + (0 << 3), 3 + (3 << 3), 3 + (1 << 3), 4 + (6 << 3), 0,
    //                                  0,            1,            2,            3,            4,
                                        4 + (4 << 3), 3 + (2 << 3), 4 + (8 << 3), 0, 0, 0,
    //                                  5,            6,            7,            8, 9, 10
                                        4 + (7 << 3), 0,  4 + (5 << 3),  0,  5 + (9 << 3),
    //                                  11,           12, 13,            14, 15
                                        0,  0,  0,  0,  0,  0,  0,  0,
    //                                  16, 17, 18, 19, 20, 21, 22, 23
                                        0,  0,  0,  0,  0,  0,  0,  5 + (10 << 3)];
    //                                  24, 25, 26, 27, 28, 29, 30, 31

    static ReadOnlySpan<byte> HCode => [1 + (1 << 3), 2 + (0 << 3), 0, 3 + (2 << 3), 0, 0, 0, 5 + (3 << 3),
    //                                  0,            1,            2, 3,            4, 5, 6, 7,
                                        0,  0,  0,  0,  0,  0,  0,  5 + (5 << 3),
    //                                  8,  9,  10, 11, 12, 13, 14, 15,
                                        0,  0,  0,  0,  0,  0,  0,  5 + (4 << 3),
    //                                  16, 17, 18, 19, 20, 21, 22, 23
                                        0,  0,  0,  0,  0,  0,  0,  5 + (6 << 3)];
    //                                  24, 25, 26, 27, 28, 29, 30, 31

    static int GetBitVal(ReadOnlySpan<byte> input, long bit_no, int count)
    {
        return (input[(int)(bit_no >> 3)] & (0x80 >> ((int)bit_no % 8))) != 0 ? 1 << count : 0;
    }

    static int GetCodeIdx(ReadOnlySpan<byte> code_type, ReadOnlySpan<byte> input, long len, ref long bit_no_p)
    {
        int code = 0;
        int count = 0;
        do
        {
            if (bit_no_p >= len)
                return 199;
            code += GetBitVal(input, bit_no_p, count);
            bit_no_p++;
            count++;
            if (code_type[code] != 0 &&
                (code_type[code] & 0x07) == count)
                return code_type[code] >> 3;
        } while (count < 5);
        return 1; // skip if code not found
    }

    static int GetNumFromBits(ReadOnlySpan<byte> input, long bit_no, int count)
    {
        int ret = 0;
        while (count-- != 0)
        {
            ret += GetBitVal(input, bit_no, count);
            bit_no++;
        }
        return ret;
    }

    static int ReadCount(ReadOnlySpan<byte> input, ref long bit_no_p, long len)
    {
        ReadOnlySpan<byte> bit_len = [5, 2, 7, 9, 12, 16, 17];
        ReadOnlySpan<ushort> adder = [4, 0, 36, 164, 676, 4772, 0];
        int idx = GetCodeIdx(HCode, input, len, ref bit_no_p);
        if (idx > 6)
            return 0;
        int count = GetNumFromBits(input, bit_no_p, bit_len[idx]) + adder[idx];
        bit_no_p += bit_len[idx];
        return count;
    }

    static int ReadUnicode(ReadOnlySpan<byte> input, ref long bit_no_p, long len)
    {
        int code = 0;
        for (int i = 0; i < 5; i++)
        {
            code += GetBitVal(input, bit_no_p, i);
            bit_no_p++;
            int idx = code == 0 && i == 0 ? 0 : (code == 1 && i == 1 ? 1 :
                        (code == 3 && i == 2 ? 2 : (code == 7 && i == 3 ? 3 :
                        (code == 15 && i == 4 ? 4 :
                        (code == 31 && i == 4 ? 5 : -1)))));
            if (idx == 5)
                return 0x7FFFFF00 + GetCodeIdx(HCode, input, len, ref bit_no_p);
            if (idx >= 0)
            {
                int sign = GetBitVal(input, bit_no_p, 1);
                bit_no_p++;
                int count = GetNumFromBits(input, bit_no_p, UniBitLen[idx]);
                count += UniAdder[idx];
                bit_no_p += UniBitLen[idx];
                return sign != 0 ? -count : count;
            }
        }
        return 0;
    }

    static void WriteUTF8<T>(ref T output, int uni)
        where T : IUnishoxTextOutput, allows ref struct
    {
        if (uni < (1 << 11))
        {
            output.WriteByte((byte)(0xC0 | (uni >> 6)));
            output.WriteByte((byte)(0x80 | (uni & 63)));
        }
        else if (uni < (1 << 16))
        {
            output.WriteByte((byte)(0xE0 | (uni >> 12)));
            output.WriteByte((byte)(0x80 | ((uni >> 6) & 63)));
            output.WriteByte((byte)(0x80 | (uni & 63)));
        }
        else
        {
            output.WriteByte((byte)(0xF0 | (uni >> 18)));
            output.WriteByte((byte)(0x80 | ((uni >> 12) & 63)));
            output.WriteByte((byte)(0x80 | ((uni >> 6) & 63)));
            output.WriteByte((byte)(0x80 | (uni & 63)));
        }
    }

    static void DecodeRepeat<T>(ReadOnlySpan<byte> input, long len, ref T output, ref long bit_no, UnishoxLinkList? prev_lines)
        where T : IUnishoxTextOutput, allows ref struct
    {
        if (prev_lines is not null)
        {
            int dict_len = ReadCount(input, ref bit_no, len) + NICE_LEN;
            int dist = ReadCount(input, ref bit_no, len);
            int ctx = ReadCount(input, ref bit_no, len);
            UnishoxLinkList cur_line = prev_lines;
            while (ctx-- != 0)
                cur_line = cur_line.Previous ?? throw new ArgumentException("prev_lines are not suitable for decompressing this data!", nameof(prev_lines));
            output.Write(cur_line.Data.Span.Slice(dist, dict_len));
        }
        else
        {
            int dict_len = ReadCount(input, ref bit_no, len) + NICE_LEN;
            int dist = ReadCount(input, ref bit_no, len) + NICE_LEN - 1;
            output.CopyFrom(dist, dict_len);
        }
    }

    public static int DecompressCount(ReadOnlySpan<byte> input, UnishoxLinkList? prev_lines = null)
    {
        DummyOutput o = new();
        Decompress(input, ref o, prev_lines);
        return o.Position;
    }
    public static int Decompress(ReadOnlySpan<byte> input, Stream output, UnishoxLinkList? prev_lines = null)
    {
        StreamOutput o = new(output);
        Decompress(input, ref o, prev_lines);
        return o.Position;
    }
    public static int Decompress(ReadOnlySpan<byte> input, Span<byte> output, UnishoxLinkList? prev_lines = null)
    {
        SpanOutput o = new(output);
        Decompress(input, ref o, prev_lines);
        return o.Position;
    }
    public static void Decompress<T>(ReadOnlySpan<byte> input, ref T output, UnishoxLinkList? prev_lines = null)
        where T : IUnishoxTextOutput, allows ref struct
    {
        Set dstate = Set.S1;
        long bit_no = 0;
        bool is_all_upper = false;
        int prev_uni = 0;
        long len = (long)input.Length << 3;
        while (bit_no < len)
        {
            int h, v;
            byte c = 0;
            bool is_upper = is_all_upper;
            long orig_bit_no = bit_no;
            v = GetCodeIdx(VCode, input, len, ref bit_no);
            if (v == 199)
            {
                bit_no = orig_bit_no;
                break;
            }
            h = (int)dstate;
            if (v == 0)
            {
                h = GetCodeIdx(HCode, input, len, ref bit_no);
                if (h == 199)
                {
                    bit_no = orig_bit_no;
                    break;
                }
                if (h == (int)Set.S1)
                {
                    if (dstate == Set.S1)
                    {
                        if (is_all_upper)
                        {
                            is_all_upper = false;
                            continue;
                        }
                        v = GetCodeIdx(VCode, input, len, ref bit_no);
                        if (v == 199)
                        {
                            bit_no = orig_bit_no;
                            break;
                        }
                        if (v == 0)
                        {
                            h = GetCodeIdx(HCode, input, len, ref bit_no);
                            if (h == 199)
                            {
                                bit_no = orig_bit_no;
                                break;
                            }
                            if (h == (int)Set.S1)
                            {
                                is_all_upper = true;
                                continue;
                            }
                        }
                        is_upper = true;
                    }
                    else
                    {
                        dstate = Set.S1;
                        continue;
                    }
                }
                else if (h == (int)Set.S2)
                {
                    if (dstate == Set.S1)
                        dstate = Set.S2;
                    continue;
                }
                if (h != (int)Set.S1)
                {
                    v = GetCodeIdx(VCode, input, len, ref bit_no);
                    if (v == 199)
                    {
                        bit_no = orig_bit_no;
                        break;
                    }
                }
            }
            if (v == 0 && h == (int)Set.S1A)
            {
                if (is_upper)
                    output.WriteByte((byte)ReadCount(input, ref bit_no, len));
                else
                    DecodeRepeat(input, len, ref output, ref bit_no, prev_lines);
                continue;
            }
            if (h == (int)Set.S1 && v == 3)
            {
                do
                {
                    int delta = ReadUnicode(input, ref bit_no, len);
                    if ((delta >> 8) == 0x7FFFFF)
                    {
                        int spl_code_idx = delta & 0x000000FF;
                        if (spl_code_idx == 2)
                            break;
                        switch (spl_code_idx)
                        {
                            case 1:
                                output.WriteByte((byte)' ');
                                break;
                            case 0:
                                DecodeRepeat(input, len, ref output, ref bit_no, prev_lines);
                                break;
                            case 3:
                                output.WriteByte((byte)',');
                                break;
                            case 4:
                                if (prev_uni > 0x3000)
                                    WriteUTF8(ref output, 0x3002);
                                else
                                    output.WriteByte((byte)'.');
                                break;
                            case 5:
                                output.WriteByte(13);
                                break;
                            case 6:
                                output.WriteByte(10);
                                break;
                        }
                    }
                    else
                    {
                        prev_uni += delta;
                        WriteUTF8(ref output, prev_uni);
                    }
                } while (is_upper);
                continue;
            }
            if (h < 64 && v < 32)
                c = Sets[h * 11 + v];
            if (c is >= (byte)'a' and <= (byte)'z')
            {
                if (is_upper)
                    c -= 32;
            }
            else
            {
                if (is_upper && dstate == Set.S1 && v == 1)
                    c = (byte)'\t';
                if (h == (int)Set.S1B)
                {
                    switch (v)
                    {
                        case 9:
                            output.WriteByte((byte)'\r');
                            output.WriteByte((byte)'\n');
                            continue;
                        case 8:
                            if (is_upper)
                            {   // rpt
                                int count = ReadCount(input, ref bit_no, len);
                                count += 4;
                                output.RepeatLast(count);
                            }
                            else
                            {
                                output.WriteByte((byte)'\n');
                            }
                            continue;
                        case 10:
                            continue;
                    }
                }
            }
            output.WriteByte(c);
        }
    }
}
