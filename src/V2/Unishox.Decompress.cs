using System.Diagnostics;
using UnishoxSharp.Common;

namespace UnishoxSharp.V2;

partial class Unishox
{
    /// <summary>
    /// Reads one bit from in
    /// </summary>
    static int ReadBit<TIn>(ref TIn input, bool autoForward = false)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        return input.ReadBit(autoForward);
    }

    /// <summary>
    /// Reads next 8 bits, if available
    /// </summary>
    static byte Read8BitCode<TIn>(ref TIn input, int offset = 0)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        return (byte)input.Read8Bit(false, offset);
    }

    /// <summary>
    /// The list of veritical codes is split into 5 sections. Used by <see cref="ReadVCodeIdx" />
    /// </summary>
    const int SECTION_COUNT = 5;
    /// <summary>
    /// Used by <see cref="ReadVCodeIdx" /> for finding the section under which the code read using <see cref="Read8BitCode" /> falls
    /// </summary>
    static ReadOnlySpan<byte> VSections => [0x7F, 0xBF, 0xDF, 0xEF, 0xFF];
    /// <summary>
    /// Used by <see cref="ReadVCodeIdx" /> for finding the section vertical position offset
    /// </summary>
    static ReadOnlySpan<byte> VSectionPos => [0, 4, 8, 12, 20];
    /// <summary>
    /// Used by <see cref="ReadVCodeIdx" /> for masking the code read by <see cref="Read8BitCode" />
    /// </summary>
    static ReadOnlySpan<byte> VSectionMask => [0x7F, 0x3F, 0x1F, 0x0F, 0x0F];
    /// <summary>
    /// Used by <see cref="ReadVCodeIdx" /> for shifting the code read by <see cref="Read8BitCode" /> to obtain the vpos
    /// </summary>
    static ReadOnlySpan<byte> VSectionShift => [5, 4, 3, 1, 0];

    /// <summary>
    /// Vertical decoder lookup table - 3 bits code len, 5 bytes vertical pos
    /// </summary>
    /// <remarks>
    /// code len is one less as 8 cannot be accommodated in 3 bits
    /// </remarks>
    static ReadOnlySpan<byte> VCodeLookup => [
        (1 << 5) + 0,  (1 << 5) + 0,  (2 << 5) + 1,  (2 << 5) + 2,  // Section 1
        (3 << 5) + 3,  (3 << 5) + 4,  (3 << 5) + 5,  (3 << 5) + 6,  // Section 2
        (3 << 5) + 7,  (3 << 5) + 7,  (4 << 5) + 8,  (4 << 5) + 9,  // Section 3
        (5 << 5) + 10, (5 << 5) + 10, (5 << 5) + 11, (5 << 5) + 11, // Section 4
        (5 << 5) + 12, (5 << 5) + 12, (6 << 5) + 13, (6 << 5) + 14,
        (6 << 5) + 15, (6 << 5) + 15, (6 << 5) + 16, (6 << 5) + 16, // Section 5
        (6 << 5) + 17, (6 << 5) + 17, (7 << 5) + 18, (7 << 5) + 19,
        (7 << 5) + 20, (7 << 5) + 21, (7 << 5) + 22, (7 << 5) + 23,
        (7 << 5) + 24, (7 << 5) + 25, (7 << 5) + 26, (7 << 5) + 27];

    /// <summary>
    /// Decodes the vertical code from the given bitstream at in<br />
    /// This is designed to use less memory using a 36 uint8_t buffer
    /// compared to using a 256 uint8_t buffer to decode the next 8 bits read by <see cref="Read8BitCode" /> by splitting the list of vertical codes.<br />
    /// Decoder is designed for using less memory, not speed.<br />
    /// Returns the veritical code index or 99 if match could not be found.<br />
    /// Also updates bit_no_p with how many ever bits used by the vertical code.
    /// </summary>
    static int ReadVCodeIdx<TIn>(ref TIn input)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        if (input.CanRead())
        {
            byte code = Read8BitCode(ref input);
            int i = 0;
            do
            {
                if (code <= VSections[i])
                {
                    byte vcode = VCodeLookup[VSectionPos[i] + ((code & VSectionMask[i]) >> VSectionShift[i])];
                    input.Skip((vcode >> 5) + 1);
                    if (!input.CanRead(0))
                        return 99;
                    return vcode & 0x1F;
                }
            } while (++i < SECTION_COUNT);
        }
        return 99;
    }

    /// <summary>
    /// Mask for retrieving each code to be decoded according to its length
    /// </summary>
    static ReadOnlySpan<byte> LenMasks => [0x80, 0xC0, 0xE0, 0xF0, 0xF8, 0xFC, 0xFE, 0xFF];
    /// <summary>
    /// Decodes the horizontal code from the given bitstream at in
    /// depending on the hcodes defined using usx_hcodes and usx_hcode_lens<br />
    /// Returns the horizontal code index or 99 if match could not be found.<br />
    /// Also updates bit_no_p with how many ever bits used by the horizontal code.
    /// </summary>
    static int ReadHCodeIdx<TIn, TPreset>(ref TIn input, in TPreset preset)
        where TIn : IUnishoxDataInput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        if (preset.HCodeLensSpan[(int)SetAndState.Alpha] == 0)
            return (int)SetAndState.Alpha;
        if (input.CanRead())
        {
            byte code = Read8BitCode(ref input);
            for (int code_pos = 0; code_pos < 5; code_pos++)
            {
                if (preset.HCodeLensSpan[code_pos] != 0 && (code & LenMasks[preset.HCodeLensSpan[code_pos] - 1]) == preset.HCodesSpan[code_pos])
                {
                    input.Skip(preset.HCodeLensSpan[code_pos]);
                    return code_pos;
                }
            }
        }
        return 99;
    }

    /// <summary>
    /// Returns the position of step code (0, 10, 110, etc.) encountered in the stream
    /// </summary>
    /// <returns></returns>
    static int GetStepCodeIdx<TIn>(ref TIn input, int limit)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int idx = 0;
        while (input.CanRead() && ReadBit(ref input, true) != 0)
        {
            idx++;
            if (idx == limit)
                return idx;
        }
        if (!input.CanRead())
            return 99;
        return idx;
    }

    /// <summary>
    /// Reads specified number of bits and builds the corresponding integer
    /// </summary>
    static int GetNumFromBits<TIn>(ref TIn input, int count)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int ret = 0;
        while (count-- > 0 && input.CanRead())
        {
            ret += ReadBit(ref input, true) != 0 ? 1 << count : 0;
        }
        return count < 0 ? ret : -1;
    }

    /// <summary>
    /// Decodes the count from the given bit stream at in. Also updates bit_no_p
    /// </summary>
    static int ReadCount<TIn>(ref TIn input)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int idx = GetStepCodeIdx(ref input, 4);
        if (idx == 99)
            return -1;
        if (!input.CanRead(CountBitLens[idx]))
            return -1;
        int count = GetNumFromBits(ref input, CountBitLens[idx]) + (idx != 0 ? CountAdder[idx - 1] : 0);
        return count;
    }

    /// <summary>
    /// Decodes the Unicode codepoint from the given bit stream at in. Also updates bit_no_p<br />
    /// When the step code is 5, reads the next step code to find out the special code.
    /// </summary>
    static int ReadUnicode<TIn>(ref TIn input)
        where TIn : IUnishoxDataInput, allows ref struct
    {
        int idx = GetStepCodeIdx(ref input, 5);
        if (idx == 99)
            return 0x7FFFFF00 + 99;
        if (idx == 5)
        {
            idx = GetStepCodeIdx(ref input, 4);
            return 0x7FFFFF00 + idx;
        }
        if (idx >= 0)
        {
            bool sign = input.CanRead() && ReadBit(ref input, true) != 0;
            if (!input.CanRead(UniBitLen[idx]))
                return 0x7FFFFF00 + 99;
            int count = GetNumFromBits(ref input, UniBitLen[idx]);
            count += UniAdder[idx];
            return sign ? -count : count;
        }
        return 0;
    }

    /// <summary>
    /// Write given unicode code point to out as a UTF-8 sequence
    /// </summary>
    static void WriteUTF8<TOut>(ref TOut output, int uni)
        where TOut : IUnishoxTextOutput, allows ref struct
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

    /// <summary>
    /// Decode repeating sequence and appends to out
    /// </summary>
    static bool DecodeRepeat<TIn, TOut>(ref TIn input, ref TOut output, UnishoxLinkList? prev_lines)
        where TIn : IUnishoxDataInput, allows ref struct
        where TOut : IUnishoxTextOutput, allows ref struct
    {
        if (prev_lines is not null)
        {
            int dict_len = ReadCount(ref input) + NICE_LEN;
            if (dict_len < NICE_LEN)
                return false;
            int dist = ReadCount(ref input);
            if (dist < 0)
                return false;
            int ctx = ReadCount(ref input);
            if (ctx < 0)
                return false;
            UnishoxLinkList? cur_line = prev_lines;
            while (ctx-- > 0 && cur_line is not null)
                cur_line = cur_line.Previous;
            if (cur_line is null)
                return false;
            if (dist >= cur_line.Data.Length)
                return false;
            output.Write(cur_line.Data.Span.Slice(dist, dict_len));
        }
        else
        {
            int dict_len = ReadCount(ref input);
            if (dict_len < 0)
                return false;
            int dist = ReadCount(ref input);
            if (dist < 0)
                return false;
            output.CopyFrom(1 - NICE_LEN - dist, NICE_LEN + dict_len);
        }
        return true;
    }

    /// <summary>
    /// Returns hex character corresponding to the 4 bit nibble
    /// </summary>
    static byte GetHexChar(int nibble, NibbleType hex_type)
    {
        if (nibble is >= 0 and <= 9)
            return (byte)('0' + nibble);
        else if (hex_type < NibbleType.HexUpper)
            return (byte)('a' + nibble - 10);
        return (byte)('A' + nibble - 10);
    }

    public static int DecompressCount(Stream input, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
    {
        return DecompressCount(input, UnishoxPresets.Default, prev_lines, magic_bit_len);
    }
    public static int DecompressCount<TPreset>(Stream input, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TPreset : IUnishoxPreset, allows ref struct
    {
        StreamInput i = new(input);
        DummyOutput o = new();
        Decompress(ref i, ref o, in preset, prev_lines, magic_bit_len);
        return o.Position;
    }
    public static int Decompress(Stream input, Stream output, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
    {
        return Decompress(input, output, UnishoxPresets.Default, prev_lines, magic_bit_len);
    }
    public static int Decompress<TPreset>(Stream input, Stream output, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TPreset : IUnishoxPreset, allows ref struct
    {
        StreamInput i = new(input);
        StreamOutput o = new(output);
        Decompress(ref i, ref o, in preset, prev_lines, magic_bit_len);
        return o.Position;
    }
    public static int Decompress(Stream input, scoped Span<byte> output, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
    {
        return Decompress(input, output, UnishoxPresets.Default, prev_lines, magic_bit_len);
    }
    public static int Decompress<TPreset>(Stream input, scoped Span<byte> output, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TPreset : IUnishoxPreset, allows ref struct
    {
        StreamInput i = new(input);
        SpanOutput o = new(output);
        Decompress(ref i, ref o, in preset, prev_lines, magic_bit_len);
        return o.Position;
    }
    public static int DecompressCount(scoped ReadOnlySpan<byte> input, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
    {
        return DecompressCount(input, UnishoxPresets.Default, prev_lines, magic_bit_len);
    }
    public static int DecompressCount<TPreset>(scoped ReadOnlySpan<byte> input, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TPreset : IUnishoxPreset, allows ref struct
    {
        SpanInput i = new(input);
        DummyOutput o = new();
        Decompress(ref i, ref o, in preset, prev_lines, magic_bit_len);
        return o.Position;
    }
    public static int Decompress(scoped ReadOnlySpan<byte> input, Stream output, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
    {
        return Decompress(input, output, UnishoxPresets.Default, prev_lines, magic_bit_len);
    }
    public static int Decompress<TPreset>(scoped ReadOnlySpan<byte> input, Stream output, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TPreset : IUnishoxPreset, allows ref struct
    {
        SpanInput i = new(input);
        StreamOutput o = new(output);
        Decompress(ref i, ref o, in preset, prev_lines, magic_bit_len);
        return o.Position;
    }
    public static int Decompress(scoped ReadOnlySpan<byte> input, scoped Span<byte> output, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
    {
        return Decompress(input, output, UnishoxPresets.Default, prev_lines, magic_bit_len);
    }
    public static int Decompress<TPreset>(scoped ReadOnlySpan<byte> input, scoped Span<byte> output, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TPreset : IUnishoxPreset, allows ref struct
    {
        SpanInput i = new(input);
        SpanOutput o = new(output);
        Decompress(ref i, ref o, in preset, prev_lines, magic_bit_len);
        return o.Position;
    }
    public static void Decompress<TIn, TOut, TPreset>(ref TIn input, ref TOut output, in TPreset preset, UnishoxLinkList? prev_lines = null, int magic_bit_len = 1)
        where TIn : IUnishoxDataInput, allows ref struct
        where TOut : IUnishoxTextOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        ArgumentOutOfRangeException.ThrowIfNegative(magic_bit_len);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(magic_bit_len, 8);
        SetAndState dstate = SetAndState.Alpha;
        input.Skip(magic_bit_len); // ignore the magic bit
        int h = (int)SetAndState.Alpha, v;
        bool is_all_upper = false;
        int prev_uni = 0;

        while (input.CanRead())
        {
            if (dstate == SetAndState.Delta || h == (int)SetAndState.Delta)
            {
                if (dstate != SetAndState.Delta)
                    h = (int)dstate;
                int delta = ReadUnicode(ref input);
                if ((delta >> 8) == 0x7FFFFF)
                {
                    int spl_code_idx = delta & 0x000000FF;
                    if (spl_code_idx == 99)
                        break;
                    switch (spl_code_idx)
                    {
                        case 0:
                            output.WriteByte((byte)' ');
                            continue;
                        case 1:
                            h = ReadHCodeIdx(ref input, in preset);
                            if (h == 99)
                                goto FAILED;
                            if (h is (int)SetAndState.Delta or (int)SetAndState.Alpha)
                            {
                                dstate = (SetAndState)h;
                                continue;
                            }
                            if (h == (int)SetAndState.Dict)
                            {
                                bool rpt_ret = DecodeRepeat(ref input, ref output, prev_lines);
                                if (!rpt_ret)
                                    return; // if we break here it will only break out of switch
                                h = (int)dstate;
                                continue;
                            }
                            break;
                        case 2:
                            output.WriteByte((byte)',');
                            continue;
                        case 3:
                            output.WriteByte((byte)'.');
                            continue;
                        case 4:
                            output.WriteByte(10);
                            continue;
                    }
                }
                else
                {
                    prev_uni += delta;
                    WriteUTF8(ref output, prev_uni);
                }
                if (dstate == SetAndState.Delta && h == (int)SetAndState.Delta)
                    continue;
            }
            else
                h = (int)dstate;
            byte c = 0;
            bool is_upper = is_all_upper;
            v = ReadVCodeIdx(ref input);
            if (v == 99 || h == 99)
                break;
            if (v == 0 && h != (int)SetAndState.Sym)
            {
                if (!input.CanRead())
                    break;
                if (h != (int)SetAndState.Num || dstate != SetAndState.Delta)
                {
                    h = ReadHCodeIdx(ref input, in preset);
                    if (h == 99 || !input.CanRead())
                        break;
                }
                if (h == (int)SetAndState.Alpha)
                {
                    if (dstate == SetAndState.Alpha)
                    {
                        if (preset.HCodeLensSpan[(int)SetAndState.Alpha] == 0 && TERM_BYTE_PRESET_1 == (Read8BitCode(ref input, -SW_CODE_LEN) & (0xFF << (8 - (is_all_upper ? TERM_BYTE_PRESET_1_LEN_UPPER : TERM_BYTE_PRESET_1_LEN_LOWER)))))
                            break; // Terminator for preset 1
                        if (is_all_upper)
                        {
                            is_all_upper = false;
                            continue;
                        }
                        v = ReadVCodeIdx(ref input);
                        if (v == 99)
                            break;
                        if (v == 0)
                        {
                            h = ReadHCodeIdx(ref input, in preset);
                            if (h == 99)
                                break;
                            if (h == (int)SetAndState.Alpha)
                            {
                                is_all_upper = true;
                                continue;
                            }
                        }
                        is_upper = true;
                    }
                    else
                    {
                        dstate = SetAndState.Alpha;
                        continue;
                    }
                }
                else if (h == (int)SetAndState.Dict)
                {
                    bool rpt_ret = DecodeRepeat(ref input, ref output, prev_lines);
                    if (!rpt_ret)
                        break;
                    continue;
                }
                else if (h == (int)SetAndState.Delta)
                    continue;
                else
                {
                    if (h != (int)SetAndState.Num || dstate != SetAndState.Delta)
                        v = ReadVCodeIdx(ref input);
                    if (v == 99)
                        break;
                    if (h == (int)SetAndState.Num && v == 0)
                    {
                        int idx = GetStepCodeIdx(ref input, 5);
                        if (idx == 99)
                            break;
                        if (idx == 0)
                        {
                            idx = GetStepCodeIdx(ref input, 4);
                            if (idx >= 5)
                                break;
                            int rem = ReadCount(ref input);
                            if (rem < 0)
                                break;
                            if (!preset.HasTemplates)
                                break;
                            ReadOnlySpan<byte> template = preset.GetTemplatesSpan(idx);
                            int tlen = template.Length;
                            if (rem > tlen)
                                break;
                            rem = tlen - rem;
                            bool eof = false;
                            for (int j = 0; j < rem; j++)
                            {
                                byte c_t = template[j];
                                if (c_t is (byte)'f' or (byte)'r' or (byte)'t' or (byte)'o' or (byte)'F')
                                {
                                    byte nibble_len = c_t switch
                                    {
                                        (byte)'f' or (byte)'F' => 4,
                                        (byte)'r' => 3,
                                        (byte)'t' => 2,
                                        _ => 1
                                    };
                                    int raw_char = GetNumFromBits(ref input, nibble_len);
                                    if (raw_char < 0)
                                    {
                                        eof = true;
                                        break;
                                    }
                                    output.WriteByte(GetHexChar((byte)raw_char,
                                        c_t == 'f' ? NibbleType.HexLower : NibbleType.HexUpper));
                                }
                                else
                                    output.WriteByte(c_t);
                            }
                            if (eof) break; // reach input eof
                        }
                        else if (idx == 5)
                        {
                            int bin_count = ReadCount(ref input);
                            if (bin_count < 0)
                                break;
                            if (bin_count == 0) // invalid encoding
                                break;
                            do
                            {
                                int raw_char = GetNumFromBits(ref input, 8);
                                if (raw_char < 0)
                                    break;
                                output.WriteByte((byte)raw_char);
                            } while (--bin_count != 0);
                            if (bin_count > 0) break; // reach input eof
                        }
                        else
                        {
                            int nibble_count;
                            if (idx is 2 or 4)
                                nibble_count = 32;
                            else
                            {
                                nibble_count = ReadCount(ref input);
                                if (nibble_count < 0)
                                    break;
                                if (nibble_count == 0) // invalid encoding
                                    break;
                            }
                            do
                            {
                                int nibble = GetNumFromBits(ref input, 4);
                                if (nibble < 0)
                                    break;
                                output.WriteByte(GetHexChar(nibble, idx < 3 ? NibbleType.HexLower : NibbleType.HexUpper));
                                if ((idx is 2 or 4) && (nibble_count is 25 or 21 or 17 or 13))
                                    output.WriteByte((byte)'-');
                            } while (--nibble_count != 0);
                            if (nibble_count > 0) break; // reach input eof
                        }
                        if (dstate == SetAndState.Delta)
                            h = (int)SetAndState.Delta;
                        continue;
                    }
                }
            }
            if (is_upper && v == 1)
            {
                dstate = SetAndState.Delta; // continuous delta coding
                h = (int)SetAndState.Delta;
                continue;
            }
            if (h < 3 && v < 28)
                c = Sets[h * SetsDim1Length + v];
            if (c is >= (byte)'a' and <= (byte)'z')
            {
                dstate = SetAndState.Alpha;
                if (is_upper)
                    c -= 32;
            }
            else
            {
                if (c is >= (byte)'0' and <= (byte)'9')
                    dstate = SetAndState.Num;
                else if (c == 0)
                {
                    if (v == 8)
                    {
                        output.WriteByte((byte)'\r');
                        output.WriteByte((byte)'\n');
                    }
                    else if (h == (int)SetAndState.Num && v == 26)
                    {
                        int count = ReadCount(ref input);
                        if (count < 0)
                            break;
                        count += 4;
                        output.RepeatLast(count);
                    }
                    else if (h == (int)SetAndState.Sym && v > 24)
                    {
                        v -= 25;
                        ReadOnlySpan<byte> seq = preset.GetFreqSeqSpan(v);
                        output.Write(seq);
                    }
                    else if (h == (int)SetAndState.Num && v is > 22 and < 26)
                    {
                        v -= (23 - 3);
                        ReadOnlySpan<byte> seq = preset.GetFreqSeqSpan(v);
                        output.Write(seq);
                    }
                    else
                        break; // Terminator
                    if (dstate == SetAndState.Delta)
                        h = (int)SetAndState.Delta;
                    continue;
                }
            }
            if (dstate == SetAndState.Delta)
                h = (int)SetAndState.Delta;
            output.WriteByte(c);
        }
    FAILED:;
    }
}