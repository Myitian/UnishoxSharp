using System;
using System.Collections.Generic;
using UnishoxSharp.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UnishoxSharp.V2;

internal partial class Unishox
{
    /// <summary>
    /// Reads one bit from in
    /// </summary>
    static int readBit(ReadOnlySpan<byte> input, long bit_no)
    {
        return input[(int)(bit_no >> 3)] & (0x80 >> (int)(bit_no % 8));
    }

    /// <summary>
    /// Reads next 8 bits, if available
    /// </summary>
    static byte read8bitCode(ReadOnlySpan<byte> input, long bit_no)
    {
        int bit_pos = (int)(bit_no & 0x07);
        int char_pos = (int)(bit_no >> 3);
        byte code = (byte)(input[char_pos] << bit_pos);
        char_pos++;
        if (char_pos < input.Length)
            code |= (byte)(input[char_pos] >> (8 - bit_pos));
        else
            code |= (byte)(0xFF >> (8 - bit_pos));
        return code;
    }

    /// <summary>
    /// The list of veritical codes is split into 5 sections. Used by <see cref="readVCodeIdx" />
    /// </summary>
    const int SECTION_COUNT = 5;
    /// <summary>
    /// Used by <see cref="readVCodeIdx" /> for finding the section under which the code read using <see cref="read8bitCode" /> falls
    /// </summary>
    static ReadOnlySpan<byte> usx_vsections => [0x7F, 0xBF, 0xDF, 0xEF, 0xFF];
    /// <summary>
    /// Used by <see cref="readVCodeIdx" /> for finding the section vertical position offset
    /// </summary>
    static ReadOnlySpan<byte> usx_vsection_pos => [0, 4, 8, 12, 20];
    /// <summary>
    /// Used by <see cref="readVCodeIdx" /> for masking the code read by <see cref="read8bitCode" />
    /// </summary>
    static ReadOnlySpan<byte> usx_vsection_mask => [0x7F, 0x3F, 0x1F, 0x0F, 0x0F];
    /// <summary>
    /// Used by <see cref="readVCodeIdx" /> for shifting the code read by <see cref="read8bitCode" /> to obtain the vpos
    /// </summary>
    static ReadOnlySpan<byte> usx_vsection_shift => [5, 4, 3, 1, 0];

    /// <summary>
    /// Vertical decoder lookup table - 3 bits code len, 5 bytes vertical pos
    /// </summary>
    /// <remarks>
    /// code len is one less as 8 cannot be accommodated in 3 bits
    /// </remarks>
    static ReadOnlySpan<byte> usx_vcode_lookup => [
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
    /// compared to using a 256 uint8_t buffer to decode the next 8 bits read by <see cref="read8bitCode" /> by splitting the list of vertical codes.<br />
    /// Decoder is designed for using less memory, not speed.<br />
    /// Returns the veritical code index or 99 if match could not be found.<br />
    /// Also updates bit_no_p with how many ever bits used by the vertical code.
    /// </summary>
    static int readVCodeIdx(ReadOnlySpan<byte> input, ref long bit_no_p)
    {
        long len = (long)input.Length << 3;
        if (bit_no_p < len)
        {
            byte code = read8bitCode(input, bit_no_p);
            int i = 0;
            do
            {
                if (code <= usx_vsections[i])
                {
                    byte vcode = usx_vcode_lookup[usx_vsection_pos[i] + ((code & usx_vsection_mask[i]) >> usx_vsection_shift[i])];
                    bit_no_p += (vcode >> 5) + 1;
                    if (bit_no_p > len)
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
    static ReadOnlySpan<byte> len_masks => [0x80, 0xC0, 0xE0, 0xF0, 0xF8, 0xFC, 0xFE, 0xFF];
    /// <summary>
    /// Decodes the horizontal code from the given bitstream at in
    /// depending on the hcodes defined using usx_hcodes and usx_hcode_lens<br />
    /// Returns the horizontal code index or 99 if match could not be found.<br />
    /// Also updates bit_no_p with how many ever bits used by the horizontal code.
    /// </summary>
    static int readHCodeIdx<TPreset>(ReadOnlySpan<byte> input, ref long bit_no_p, in TPreset preset)
     where TPreset : IUnishoxPreset, allows ref struct
    {
        if (preset.HCodeLensSpan[(int)SetAndState.Alpha] == 0)
            return (int)SetAndState.Alpha;
        long len = (long)input.Length << 3;
        if (bit_no_p < len)
        {
            byte code = read8bitCode(input, bit_no_p);
            for (int code_pos = 0; code_pos < 5; code_pos++)
            {
                if (preset.HCodeLensSpan[code_pos] != 0 && (code & len_masks[preset.HCodeLensSpan[code_pos] - 1]) == preset.HCodesSpan[code_pos])
                {
                    bit_no_p += preset.HCodeLensSpan[code_pos];
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
    static int getStepCodeIdx(ReadOnlySpan<byte> input, ref long bit_no_p, int limit)
    {
        long len = (long)input.Length << 3;
        int idx = 0;
        while (bit_no_p < len && readBit(input, bit_no_p) != 0)
        {
            idx++;
            bit_no_p++;
            if (idx == limit)
                return idx;
        }
        if (bit_no_p >= len)
            return 99;
        bit_no_p++;
        return idx;
    }

    /// <summary>
    /// Reads specified number of bits and builds the corresponding integer
    /// </summary>
    static int getNumFromBits(ReadOnlySpan<byte> input, long bit_no, int count)
    {
        long len = (long)input.Length << 3;
        int ret = 0;
        while (count-- > 0 && bit_no < len)
        {
            ret += readBit(input, bit_no) != 0 ? 1 << count : 0;
            bit_no++;
        }
        return count < 0 ? ret : -1;
    }

    /// <summary>
    /// Decodes the count from the given bit stream at in. Also updates bit_no_p
    /// </summary>
    static int readCount(ReadOnlySpan<byte> input, ref long bit_no_p)
    {
        long len = (long)input.Length << 3;
        int idx = getStepCodeIdx(input, ref bit_no_p, 4);
        if (idx == 99)
            return -1;
        if (bit_no_p + count_bit_lens[idx] - 1 >= len)
            return -1;
        int count = getNumFromBits(input, bit_no_p, count_bit_lens[idx]) + (idx != 0 ? count_adder[idx - 1] : 0);
        bit_no_p += count_bit_lens[idx];
        return count;
    }

    /// <summary>
    /// Decodes the Unicode codepoint from the given bit stream at in. Also updates bit_no_p<br />
    /// When the step code is 5, reads the next step code to find out the special code.
    /// </summary>
    static int readUnicode(ReadOnlySpan<byte> input, ref long bit_no_p)
    {
        long len = (long)input.Length << 3;
        int idx = getStepCodeIdx(input, ref bit_no_p, 5);
        if (idx == 99)
            return 0x7FFFFF00 + 99;
        if (idx == 5)
        {
            idx = getStepCodeIdx(input, ref bit_no_p, 4);
            return 0x7FFFFF00 + idx;
        }
        if (idx >= 0)
        {
            bool sign = bit_no_p < len && readBit(input, bit_no_p) != 0;
            bit_no_p++;
            if (bit_no_p + uni_bit_len[idx] - 1 >= len)
                return 0x7FFFFF00 + 99;
            int count = getNumFromBits(input, bit_no_p, uni_bit_len[idx]);
            count += uni_adder[idx];
            bit_no_p += uni_bit_len[idx];
            return sign ? -count : count;
        }
        return 0;
    }

    /// <summary>
    /// Write given unicode code point to out as a UTF-8 sequence
    /// </summary>
    static void writeUTF8<T>(ref T output, int uni)
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

    /// <summary>
    /// Decode repeating sequence and appends to out
    /// </summary>
    static bool decodeRepeat<T>(ReadOnlySpan<byte> input, ref T output, ref long bit_no, UnishoxLinkList? prev_lines)
        where T : IUnishoxTextOutput, allows ref struct
    {
        if (prev_lines is not null)
        {
            int dict_len = readCount(input, ref bit_no) + NICE_LEN;
            if (dict_len < NICE_LEN)
                return false;
            int dist = readCount(input, ref bit_no);
            if (dist < 0)
                return false;
            int ctx = readCount(input, ref bit_no);
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
            int dict_len = readCount(input, ref bit_no) + NICE_LEN;
            if (dict_len < NICE_LEN)
                return false;
            int dist = readCount(input, ref bit_no) + NICE_LEN - 1;
            if (dist < NICE_LEN - 1)
                return false;
            output.CopyFrom(dist, dict_len);
        }
        return true;
    }

    /// <summary>
    /// Returns hex character corresponding to the 4 bit nibble
    /// </summary>
    static byte getHexChar(int nibble, NibbleType hex_type)
    {
        if (nibble is >= 0 and <= 9)
            return (byte)('0' + nibble);
        else if (hex_type < NibbleType.HexUpper)
            return (byte)('a' + nibble - 10);
        return (byte)('A' + nibble - 10);
    }

    public static void unishox2_decompress_lines<T, TPreset>(ReadOnlySpan<byte> input, ref T output, in TPreset preset, UnishoxLinkList? prev_lines = null, int UNISHOX_MAGIC_BIT_LEN = 1)
          where T : IUnishoxTextOutput, allows ref struct
          where TPreset : IUnishoxPreset, allows ref struct
    {
        ArgumentOutOfRangeException.ThrowIfNegative(UNISHOX_MAGIC_BIT_LEN);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(UNISHOX_MAGIC_BIT_LEN, 8);
        SetAndState dstate = SetAndState.Alpha;
        long bit_no = UNISHOX_MAGIC_BIT_LEN; // ignore the magic bit
        int h = (int)SetAndState.Alpha, v;
        bool is_all_upper = false;
        int prev_uni = 0;

        long len = (long)input.Length << 3;
        while (bit_no < len)
        {
            long orig_bit_no = bit_no;
            if (dstate == SetAndState.Delta || h == (int)SetAndState.Delta)
            {
                if (dstate != SetAndState.Delta)
                    h = (int)dstate;
                int delta = readUnicode(input, ref bit_no);
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
                            h = readHCodeIdx(input, ref bit_no, in preset);
                            if (h == 99)
                            {
                                bit_no = len;
                                continue;
                            }
                            if (h is (int)SetAndState.Delta or (int)SetAndState.Alpha)
                            {
                                dstate = (SetAndState)h;
                                continue;
                            }
                            if (h == (int)SetAndState.Dict)
                            {
                                bool rpt_ret = decodeRepeat(input, ref output, ref bit_no, prev_lines);
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
                    writeUTF8(ref output, prev_uni);
                }
                if (dstate == SetAndState.Delta && h == (int)SetAndState.Delta)
                    continue;
            }
            else
                h = (int)dstate;
            byte c = 0;
            bool is_upper = is_all_upper;
            v = readVCodeIdx(input, ref bit_no);
            if (v == 99 || h == 99)
            {
                bit_no = orig_bit_no;
                break;
            }
            if (v == 0 && h != (int)SetAndState.Sym)
            {
                if (bit_no >= len)
                    break;
                if (h != (int)SetAndState.Num || dstate != SetAndState.Delta)
                {
                    h = readHCodeIdx(input, ref bit_no, in preset);
                    if (h == 99 || bit_no >= len)
                    {
                        bit_no = orig_bit_no;
                        break;
                    }
                }
                if (h == (int)SetAndState.Alpha)
                {
                    if (dstate == SetAndState.Alpha)
                    {
                        if (preset.HCodeLensSpan[(int)SetAndState.Alpha] == 0 && TERM_BYTE_PRESET_1 == (read8bitCode(input, bit_no - SW_CODE_LEN) & (0xFF << (8 - (is_all_upper ? TERM_BYTE_PRESET_1_LEN_UPPER : TERM_BYTE_PRESET_1_LEN_LOWER)))))
                            break; // Terminator for preset 1
                        if (is_all_upper)
                        {
                            is_all_upper = false;
                            continue;
                        }
                        v = readVCodeIdx(input, ref bit_no);
                        if (v == 99)
                        {
                            bit_no = orig_bit_no;
                            break;
                        }
                        if (v == 0)
                        {
                            h = readHCodeIdx(input, ref bit_no, in preset);
                            if (h == 99)
                            {
                                bit_no = orig_bit_no;
                                break;
                            }
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
                    bool rpt_ret = decodeRepeat(input, ref output, ref bit_no, prev_lines);
                    if (!rpt_ret)
                        break;
                    continue;
                }
                else if (h == (int)SetAndState.Delta)
                    continue;
                else
                {
                    if (h != (int)SetAndState.Num || dstate != SetAndState.Delta)
                        v = readVCodeIdx(input, ref bit_no);
                    if (v == 99)
                    {
                        bit_no = orig_bit_no;
                        break;
                    }
                    if (h == (int)SetAndState.Num && v == 0)
                    {
                        int idx = getStepCodeIdx(input, ref bit_no, 5);
                        if (idx == 99)
                            break;
                        if (idx == 0)
                        {
                            idx = getStepCodeIdx(input, ref bit_no, 4);
                            if (idx >= 5)
                                break;
                            int rem = readCount(input, ref bit_no);
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
                                    int raw_char = getNumFromBits(input, bit_no, nibble_len);
                                    if (raw_char < 0)
                                    {
                                        eof = true;
                                        break;
                                    }
                                    output.WriteByte(getHexChar((byte)raw_char,
                                        c_t == 'f' ? NibbleType.HexLower : NibbleType.HexUpper));
                                    bit_no += nibble_len;
                                }
                                else
                                    output.WriteByte(c_t);
                            }
                            if (eof) break; // reach input eof
                        }
                        else if (idx == 5)
                        {
                            int bin_count = readCount(input, ref bit_no);
                            if (bin_count < 0)
                                break;
                            if (bin_count == 0) // invalid encoding
                                break;
                            do
                            {
                                int raw_char = getNumFromBits(input, bit_no, 8);
                                if (raw_char < 0)
                                    break;
                                output.WriteByte((byte)raw_char);
                                bit_no += 8;
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
                                nibble_count = readCount(input, ref bit_no);
                                if (nibble_count < 0)
                                    break;
                                if (nibble_count == 0) // invalid encoding
                                    break;
                            }
                            do
                            {
                                int nibble = getNumFromBits(input, bit_no, 4);
                                if (nibble < 0)
                                    break;
                                output.WriteByte(getHexChar(nibble, idx < 3 ? NibbleType.HexLower : NibbleType.HexUpper));
                                if ((idx is 2 or 4) && (nibble_count is 25 or 21 or 17 or 13))
                                    output.WriteByte((byte)'-');
                                bit_no += 4;
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
                c = usx_sets[h * SetsDim1Length + v];
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
                        int count = readCount(input, ref bit_no);
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
    }
}