using UnishoxSharp.Common;

namespace UnishoxSharp.V1;

partial class Unishox
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

    static int GetBitVal<TIn>(ref TIn input, int count)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        return input.ReadBit(true) != 0 ? 1 << count : 0;
    }

    static int GetCodeIdx<TIn>(scoped ReadOnlySpan<byte> code_type, ref TIn input)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int code = 0;
        int count = 0;
        do
        {
            if (!input.CanRead())
                return 199;
            code += GetBitVal(ref input, count);
            count++;
            if (code_type[code] != 0 &&
                (code_type[code] & 0x07) == count)
                return code_type[code] >> 3;
        } while (count < 5);
        return 1; // skip if code not found
    }

    static int GetNumFromBits<TIn>(ref TIn input, int count)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int ret = 0;
        while (count-- != 0)
            ret += GetBitVal(ref input, count);
        return ret;
    }

    static int ReadCount<TIn>(ref TIn input)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        ReadOnlySpan<byte> bit_len = [5, 2, 7, 9, 12, 16, 17];
        ReadOnlySpan<ushort> adder = [4, 0, 36, 164, 676, 4772, 0];
        int idx = GetCodeIdx(HCode, ref input);
        if (idx > 6)
            return 0;
        int count = GetNumFromBits(ref input, bit_len[idx]) + adder[idx];
        return count;
    }

    static int ReadUnicode<TIn>(ref TIn input)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int code = 0;
        for (int i = 0; i < 5; i++)
        {
            code += GetBitVal(ref input, i);
            int idx = code == 0 && i == 0 ? 0 : (code == 1 && i == 1 ? 1 :
                        (code == 3 && i == 2 ? 2 : (code == 7 && i == 3 ? 3 :
                        (code == 15 && i == 4 ? 4 :
                        (code == 31 && i == 4 ? 5 : -1)))));
            if (idx == 5)
                return 0x7FFFFF00 + GetCodeIdx(HCode, ref input);
            if (idx >= 0)
            {
                int sign = GetBitVal(ref input, 1);
                int count = GetNumFromBits(ref input, UniBitLen[idx]);
                count += UniAdder[idx];
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
            output.Write([(byte)(0xC0 | (uni >> 6)),
                          (byte)(0x80 | (uni & 0x3F))]);
        }
        else if (uni < (1 << 16))
        {
            output.Write([(byte)(0xE0 | (uni >> 12)),
                          (byte)(0x80 | ((uni >> 6) & 0x3F)),
                          (byte)(0x80 | (uni & 0x3F))]);
        }
        else
        {
            output.Write([(byte)(0xF0 | (uni >> 18)),
                          (byte)(0x80 | ((uni >> 12) & 0x3F)),
                          (byte)(0x80 | ((uni >> 6) & 0x3F)),
                          (byte)(0x80 | (uni & 0x3F))]);
        }
    }

    static void DecodeRepeat<TIn, TOut>(ref TIn input, ref TOut output, UnishoxLinkList? prev_lines)
        where TIn : IUnishoxDataInput, allows ref struct
        where TOut : IUnishoxTextOutput, allows ref struct
    {
        if (prev_lines is not null)
        {
            int dict_len = ReadCount(ref input) + NICE_LEN;
            int dist = ReadCount(ref input);
            int ctx = ReadCount(ref input);
            UnishoxLinkList cur_line = prev_lines;
            while (ctx-- != 0)
                cur_line = cur_line.Previous ?? throw new ArgumentException("prev_lines are not suitable for decompressing this data!", nameof(prev_lines));
            output.Write(cur_line.Data.Span.Slice(dist, dict_len));
        }
        else
        {
            int dict_len = ReadCount(ref input) + NICE_LEN;
            int dist = ReadCount(ref input) + NICE_LEN - 1;
            output.CopyFrom(-dist, dict_len);
        }
    }

    public static int DecompressCount(Stream input, UnishoxLinkList? prev_lines = null)
    {
        StreamInput i = new(input);
        DummyOutput o = new();
        Decompress(ref i, ref o, prev_lines);
        return o.Position;
    }
    public static int Decompress(Stream input, Stream output, UnishoxLinkList? prev_lines = null)
    {
        StreamInput i = new(input);
        StreamOutput o = new(output);
        Decompress(ref i, ref o, prev_lines);
        return o.Position;
    }
    public static int Decompress(Stream input, scoped Span<byte> output, UnishoxLinkList? prev_lines = null)
    {
        StreamInput i = new(input);
        SpanOutput o = new(output);
        Decompress(ref i, ref o, prev_lines);
        return o.Position;
    }
    public static int DecompressCount(scoped ReadOnlySpan<byte> input, UnishoxLinkList? prev_lines = null)
    {
        SpanInput i = new(input);
        DummyOutput o = new();
        Decompress(ref i, ref o, prev_lines);
        return o.Position;
    }
    public static int Decompress(scoped ReadOnlySpan<byte> input, Stream output, UnishoxLinkList? prev_lines = null)
    {
        SpanInput i = new(input);
        StreamOutput o = new(output);
        Decompress(ref i, ref o, prev_lines);
        return o.Position;
    }
    public static int Decompress(scoped ReadOnlySpan<byte> input, scoped Span<byte> output, UnishoxLinkList? prev_lines = null)
    {
        SpanInput i = new(input);
        SpanOutput o = new(output);
        Decompress(ref i, ref o, prev_lines);
        return o.Position;
    }
    public static void Decompress<TIn, TOut>(ref TIn input, ref TOut output, UnishoxLinkList? prev_lines = null)
        where TIn : IUnishoxDataInput, allows ref struct
        where TOut : IUnishoxTextOutput, allows ref struct
    {
        Set dstate = Set.S1;
        bool is_all_upper = false;
        int prev_uni = 0;
        while (input.CanRead())
        {
            int h, v;
            byte c = 0;
            bool is_upper = is_all_upper;
            v = GetCodeIdx(VCode, ref input);
            if (v == 199)
                break;
            h = (int)dstate;
            if (v == 0)
            {
                h = GetCodeIdx(HCode, ref input);
                if (h == 199)
                    break;
                if (h == (int)Set.S1)
                {
                    if (dstate == Set.S1)
                    {
                        if (is_all_upper)
                        {
                            is_all_upper = false;
                            continue;
                        }
                        v = GetCodeIdx(VCode, ref input);
                        if (v == 199)
                            break;
                        if (v == 0)
                        {
                            h = GetCodeIdx(HCode, ref input);
                            if (h == 199)
                                break;
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
                    v = GetCodeIdx(VCode, ref input);
                    if (v == 199)
                        break;
                }
            }
            if (v == 0 && h == (int)Set.S1A)
            {
                if (is_upper)
                    output.WriteByte((byte)ReadCount(ref input));
                else
                    DecodeRepeat(ref input, ref output, prev_lines);
                continue;
            }
            if (h == (int)Set.S1 && v == 3)
            {
                do
                {
                    int delta = ReadUnicode(ref input);
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
                                DecodeRepeat(ref input, ref output, prev_lines);
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
                                int count = ReadCount(ref input);
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
