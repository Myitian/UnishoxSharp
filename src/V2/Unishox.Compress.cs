using UnishoxSharp.Common;

namespace UnishoxSharp.V2;

internal partial class Unishox
{
    /// <summary>
    /// Appends specified number of bits to the output (out)
    /// </summary>
    static void append_bits<T>(ref T output, byte code, int clen)
        where T : IUnishoxDataOutput, allows ref struct
    {
        output.WriteBits(code, clen);
    }

    /// <summary>
    /// Appends switch code to out depending on the state (<see cref="SetAndState.Delta" /> or other)
    /// </summary>
    static void append_switch_code<T>(ref T output, SetAndState state)
        where T : IUnishoxDataOutput, allows ref struct
    {
        if (state == SetAndState.Delta)
        {
            append_bits(ref output, UNI_STATE_SPL_CODE, UNI_STATE_SPL_CODE_LEN);
            append_bits(ref output, UNI_STATE_SW_CODE, UNI_STATE_SW_CODE_LEN);
        }
        else
            append_bits(ref output, SW_CODE, SW_CODE_LEN);
    }

    /// <summary>
    /// Appends given horizontal and veritical code bits to out
    /// </summary>
    static void append_code<T, TPreset>(ref T output, byte code, ref SetAndState state, in TPreset preset)
        where T : IUnishoxDataOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        int hcode = code >> 5;
        int vcode = code & 0x1F;
        if (preset.HCodeLensSpan[hcode] == 0 && hcode != (int)SetAndState.Alpha)
            return;
        switch (hcode)
        {
            case (int)SetAndState.Alpha when state != SetAndState.Alpha:
                append_switch_code(ref output, state);
                append_bits(ref output, preset.HCodesSpan[hcode], preset.HCodeLensSpan[hcode]);
                state = SetAndState.Alpha;
                break;
            case (int)SetAndState.Sym:
                append_switch_code(ref output, state);
                append_bits(ref output, preset.HCodesSpan[hcode], preset.HCodeLensSpan[hcode]);
                break;
            case (int)SetAndState.Num when state != SetAndState.Num:
                append_switch_code(ref output, state);
                append_bits(ref output, preset.HCodesSpan[hcode], preset.HCodeLensSpan[hcode]);
                if (usx_sets[hcode * SetsDim1Length + vcode] is >= (byte)'0' and <= (byte)'9')
                    state = SetAndState.Num;
                break;
        }
        append_bits(ref output, usx_vcodes[vcode], usx_vcode_lens[vcode]);
    }


    /// <summary>
    /// Length of bits used to represent count for each level
    /// </summary>
    static ReadOnlySpan<int> count_bit_lens => [2, 4, 7, 11, 16];
    /// <summary>
    /// Cumulative counts represented at each level
    /// </summary>
    static ReadOnlySpan<int> count_adder => [4, 20, 148, 2196, 67732];
    /// <summary>
    /// Codes used to specify the level that the count belongs to
    /// </summary>
    static ReadOnlySpan<byte> count_codes => [0x01, 0x82, 0xC3, 0xE4, 0xF4];
    /// <summary>
    /// Encodes given count to out
    /// </summary>
    static void encodeCount<T>(ref T output, int count)
        where T : IUnishoxDataOutput, allows ref struct
    {
        // First five bits are code and Last three bits of codes represent length
        for (int i = 0; i < 5; i++)
        {
            if (count < count_adder[i])
            {
                append_bits(ref output, (byte)(count_codes[i] & 0xF8), count_codes[i] & 0x07);
                int count16 = (count - (i != 0 ? count_adder[i - 1] : 0)) << (16 - count_bit_lens[i]);
                if (count_bit_lens[i] > 8)
                {
                    append_bits(ref output, (byte)(count16 >> 8), 8);
                    append_bits(ref output, (byte)count16, count_bit_lens[i] - 8);
                }
                else
                    append_bits(ref output, (byte)(count16 >> 8), count_bit_lens[i]);
                return;
            }
        }
    }

    /// Length of bits used to represent delta code for each level
    static ReadOnlySpan<byte> uni_bit_len => [6, 12, 14, 16, 21];
    /// Cumulative delta codes represented at each level
    static ReadOnlySpan<int> uni_adder => [0, 64, 4160, 20544, 86080];

    /// Encodes the unicode code point given by code to out. prev_code is used to calculate the delta
    static void encodeUnicode<T>(ref T output, int code, int prev_code)
        where T : IUnishoxDataOutput, allows ref struct
    {
        // First five bits are code and Last three bits of codes represent length
        ReadOnlySpan<byte> codes = [0x01, 0x82, 0xC3, 0xE4, 0xF5, 0xFD];
        int till = 0;
        int diff = code - prev_code;
        if (diff < 0)
            diff = -diff;
        for (int i = 0; i < 5; i++)
        {
            till += 1 << uni_bit_len[i];
            if (diff < till)
            {
                append_bits(ref output, (byte)(codes[i] & 0xF8), codes[i] & 0x07);
                append_bits(ref output, prev_code > code ? (byte)0x80 : (byte)0, 1);
                int val = diff - uni_adder[i];
                if (uni_bit_len[i] > 16)
                {
                    val <<= 24 - uni_bit_len[i];
                    append_bits(ref output, (byte)(val >> 16), 8);
                    append_bits(ref output, (byte)(val >> 8), 8);
                    append_bits(ref output, (byte)val, uni_bit_len[i] - 16);
                }
                else if (uni_bit_len[i] > 8)
                {
                    val <<= 16 - uni_bit_len[i];
                    append_bits(ref output, (byte)(val >> 8), 8);
                    append_bits(ref output, (byte)val, uni_bit_len[i] - 8);
                }
                else
                {
                    val <<= 8 - uni_bit_len[i];
                    append_bits(ref output, (byte)val, uni_bit_len[i]);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Reads UTF-8 character from input. Also returns the number of bytes occupied by the UTF-8 character in utf8len
    /// </summary>
    static int readUTF8(ReadOnlySpan<byte> input, int l, out int utf8len)
    {
        int ret = 0;
        if (l < (input.Length - 1)
            && (input[l] & 0xE0) == 0xC0
            && (input[l + 1] & 0xC0) == 0x80)
        {
            utf8len = 2;
            ret = input[l] & 0x1F;
            ret <<= 6;
            ret += input[l + 1] & 0x3F;
            if (ret < 0x80)
                ret = 0;
        }
        else if (l < (input.Length - 2)
            && (input[l] & 0xF0) == 0xE0
            && (input[l + 1] & 0xC0) == 0x80
            && (input[l + 2] & 0xC0) == 0x80)
        {
            utf8len = 3;
            ret = input[l] & 0x0F;
            ret <<= 6;
            ret += input[l + 1] & 0x3F;
            ret <<= 6;
            ret += input[l + 2] & 0x3F;
            if (ret < 0x0800)
                ret = 0;
        }
        else if (l < (input.Length - 3)
            && (input[l] & 0xF8) == 0xF0
            && (input[l + 1] & 0xC0) == 0x80
            && (input[l + 2] & 0xC0) == 0x80
            && (input[l + 3] & 0xC0) == 0x80)
        {
            utf8len = 4;
            ret = input[l] & 0x07;
            ret <<= 6;
            ret += input[l + 1] & 0x3F;
            ret <<= 6;
            ret += input[l + 2] & 0x3F;
            ret <<= 6;
            ret += input[l + 3] & 0x3F;
            if (ret < 0x10000)
                ret = 0;
        }
        else
            utf8len = 0;
        return ret;
    }

    /// <summary>
    /// Finds the longest matching sequence from the beginning of the string.<br />
    /// If a match is found and it is longer than NICE_LEN, it is encoded as a repeating sequence to out<br />
    /// This is also used for Unicode strings<br />
    /// This is a crude implementation that is not optimized. Assuming only short strings are encoded, this is not much of an issue.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TPreset"></typeparam>
    /// <param name="input"></param>
    /// <param name="l"></param>
    /// <param name="output"></param>
    /// <param name="state"></param>
    /// <param name="preset"></param>
    /// <returns></returns>
    static int matchOccurance<T, TPreset>(ReadOnlySpan<byte> input, int l, ref T output, ref SetAndState state, in TPreset preset)
        where T : IUnishoxDataOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        int j, k;
        int longest_dist = 0;
        int longest_len = 0;
        for (j = l - NICE_LEN; j >= 0; j--)
        {
            for (k = l; k < input.Length && j + k - l < l; k++)
            {
                if (input[k] != input[j + k - l])
                    break;
            }
            if (k < input.Length)
                while ((input[k] >> 6) == 2)
                    k--; // Skip partial UTF-8 matches
            if ((k - l) > (NICE_LEN - 1))
            {
                int match_len = k - l - NICE_LEN;
                int match_dist = l - j - NICE_LEN + 1;
                if (match_len > longest_len)
                {
                    longest_len = match_len;
                    longest_dist = match_dist;
                }
            }
        }
        if (longest_len != 0)
        {
            append_switch_code(ref output, state);
            append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Dict], preset.HCodeLensSpan[(int)SetAndState.Dict]);
            encodeCount(ref output, longest_len);
            encodeCount(ref output, longest_dist);
            l += longest_len + NICE_LEN;
            l--;
            return l;
        }
        return -l;
    }

    /// <summary>
    /// This is used only when encoding a string array<br />
    /// Finds the longest matching sequence from the previous array element to the beginning of the string array.<br />
    /// If a match is found and it is longer than NICE_LEN, it is encoded as a repeating sequence to out<br />
    /// This is also used for Unicode strings<br />
    /// This is a crude implementation that is not optimized. Assuming only short strings are encoded, this is not much of an issue.
    /// </summary>
    static int matchLine<T, TPreset>(ReadOnlySpan<byte> input, int l, ref T output, UnishoxLinkList? prev_lines, ref SetAndState state, in TPreset preset)
        where T : IUnishoxDataOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        int last_op = output.Position;
        int last_ob = output.RemainingBits;
        int last_len = 0;
        int last_dist = 0;
        int line_ctr = 0;
        int j = 0;
        while (prev_lines is not null)
        {
            int i, k;
            ReadOnlySpan<byte> prev_lines_data = prev_lines.Data.Span;
            int line_len = prev_lines_data.Length;
            int limit = line_ctr == 0 ? l : line_len;
            for (; j < limit; j++)
            {
                for (i = l, k = j; k < line_len && i < input.Length; k++, i++)
                {
                    if (prev_lines_data[k] != input[i])
                        break;
                }
                if (k < line_len)
                    while ((prev_lines_data[k] >> 6) == 2)
                        k--; // Skip partial UTF-8 matches
                if ((k - j) >= NICE_LEN)
                {
                    if (last_len != 0)
                    {
                        if (j > last_dist)
                            continue;
                        output.SeekBit(last_op, last_ob);
                    }
                    last_len = k - j;
                    last_dist = j;
                    int last_ctx = line_ctr;
                    append_switch_code(ref output, state);
                    append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Dict], preset.HCodeLensSpan[(int)SetAndState.Dict]);
                    encodeCount(ref output, last_len - NICE_LEN);
                    encodeCount(ref output, last_dist);
                    encodeCount(ref output, last_ctx);
                    j += last_len;
                }
            }
            line_ctr++;
            prev_lines = prev_lines.Previous;
        }
        if (last_len != 0)
        {
            l += last_len;
            l--;
            return l;
        }
        return -l;
    }

    /// <summary>
    /// Returns 4 bit code assuming ch falls between '0' to '9', 'A' to 'F' or 'a' to 'f'
    /// </summary>
    byte getBaseCode(byte ch)
    {
        return ch switch
        {
            >= (byte)'0' and <= (byte)'9' => (byte)(ch - '0' << 4),
            >= (byte)'A' and <= (byte)'F' => (byte)(ch - 'A' + 10 << 4),
            >= (byte)'a' and <= (byte)'f' => (byte)(ch - 'a' + 10 << 4),
            _ => 0,
        };
    }

    /// <summary>
    /// Gets 4 bit code assuming ch falls between '0' to '9', 'A' to 'F' or 'a' to 'f'
    /// </summary>
    NibbleType getNibbleType(byte ch)
    {
        return ch switch
        {
            >= (byte)'0' and <= (byte)'9' => NibbleType.Num,
            >= (byte)'A' and <= (byte)'F' => NibbleType.HexUpper,
            >= (byte)'a' and <= (byte)'f' => NibbleType.HexLower,
            _ => NibbleType.Not,
        };
    }

    /// <summary>
    /// Starts coding of nibble sets
    /// </summary>
    static void append_nibble_escape<T, TPreset>(ref T output, SetAndState state, in TPreset preset)
        where T : IUnishoxDataOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        append_switch_code(ref output, state);
        append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Num], preset.HCodeLensSpan[(int)SetAndState.Num]);
        append_bits(ref output, 0, 2);
    }

    /// <summary>
    /// Appends the terminator code depending on the state, preset and whether full terminator needs to be encoded to out or not
    /// </summary>
    static void append_final_bits<T, TPreset>(ref T output, SetAndState state, bool is_all_upper, in TPreset preset)
        where T : IUnishoxDataOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        if (output.RemainingBits == 0)
            return;
        if (preset.HCodeLensSpan[(int)SetAndState.Alpha] != 0)
        {
            if (SetAndState.Num != state)
            {
                // for num state, append TERM_CODE directly
                // for other state, switch to Num Set first
                if (state == SetAndState.Delta)
                {
                    append_bits(ref output, UNI_STATE_SPL_CODE, Math.Min(UNI_STATE_SPL_CODE_LEN, 8 - output.RemainingBits));
                    if (output.RemainingBits == 0)
                        return;
                    append_bits(ref output, UNI_STATE_SW_CODE, Math.Min(UNI_STATE_SW_CODE_LEN, 8 - output.RemainingBits));
                }
                else
                    append_bits(ref output, SW_CODE, Math.Min(SW_CODE_LEN, 8 - output.RemainingBits));
                if (output.RemainingBits == 0)
                    return;
                append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Num], Math.Min(preset.HCodeLensSpan[(int)SetAndState.Num], 8 - output.RemainingBits));
                if (output.RemainingBits == 0)
                    return;
            }
            append_bits(ref output, usx_vcodes[TERM_CODE & 0x1F], Math.Min(usx_vcode_lens[TERM_CODE & 0x1F], 8 - output.RemainingBits));
        }
        else
        {
            // preset 1, terminate at 2 or 3 SW_CODE, i.e., 4 or 6 continuous 0 bits
            // see discussion: https://github.com/siara-cc/Unishox/issues/19#issuecomment-922435580
            append_bits(ref output, TERM_BYTE_PRESET_1, Math.Min(is_all_upper ? TERM_BYTE_PRESET_1_LEN_UPPER : TERM_BYTE_PRESET_1_LEN_LOWER, 8 - output.RemainingBits));
        }
        if (output.RemainingBits == 0)
            return;
        // fill uint8_t with the last bit
        append_bits(ref output, output.LastBit ? (byte)0 : (byte)0xFF, 8 - output.RemainingBits);
    }


    /// <summary>
    /// Appends the terminator code depending on the state, preset and whether full terminator needs to be encoded to out or not
    /// </summary>
    static void append_final_bits_full<T, TPreset>(ref T output, SetAndState state, bool is_all_upper, in TPreset preset)
        where T : IUnishoxDataOutput, allows ref struct
        where TPreset : IUnishoxPreset, allows ref struct
    {
        if (preset.HCodeLensSpan[(int)SetAndState.Alpha] != 0)
        {
            if (SetAndState.Num != state)
            {
                // for num state, append TERM_CODE directly
                // for other state, switch to Num Set first
                append_switch_code(ref output, state);
                append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Num], preset.HCodeLensSpan[(int)SetAndState.Num]);
            }
            append_bits(ref output, usx_vcodes[TERM_CODE & 0x1F], usx_vcode_lens[TERM_CODE & 0x1F]);
        }
        else
        {
            // preset 1, terminate at 2 or 3 SW_CODE, i.e., 4 or 6 continuous 0 bits
            // see discussion: https://github.com/siara-cc/Unishox/issues/19#issuecomment-922435580
            append_bits(ref output, TERM_BYTE_PRESET_1, is_all_upper ? TERM_BYTE_PRESET_1_LEN_UPPER : TERM_BYTE_PRESET_1_LEN_LOWER);
        }
        // fill uint8_t with the last bit
        append_bits(ref output, output.LastBit ? (byte)0 : (byte)0xFF, 8 - output.RemainingBits);
    }

    public void unishox2_compress_lines<T, TPreset>(ReadOnlySpan<byte> input, ref T output, in TPreset preset, UnishoxLinkList? prev_lines = null, bool need_full_term_codes = false, byte UNISHOX_MAGIC_BITS = 0xFF, int UNISHOX_MAGIC_BIT_LEN = 1)
         where T : IUnishoxDataOutput, allows ref struct
         where TPreset : IUnishoxPreset, allows ref struct
    {
        ArgumentOutOfRangeException.ThrowIfNegative(UNISHOX_MAGIC_BIT_LEN);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(UNISHOX_MAGIC_BIT_LEN, 8);
        SetAndState state;

        int l, ll;
        byte c_in, c_next;
        int prev_uni;
        bool is_upper, is_all_upper;
        prev_uni = 0;
        state = SetAndState.Alpha;
        is_all_upper = false;
        append_bits(ref output, UNISHOX_MAGIC_BITS, UNISHOX_MAGIC_BIT_LEN); // magic bit(s)
        for (l = 0; l < input.Length; l++)
        {

            if (preset.HCodeLensSpan[(int)SetAndState.Dict] != 0 && l < (input.Length - NICE_LEN + 1))
            {
                if (prev_lines is not null)
                {
                    l = matchLine(input, l, ref output, prev_lines, ref state, in preset);
                    if (l > 0)
                        continue;
                    l = -l;
                }
                else
                {
                    l = matchOccurance(input, l, ref output, ref state, in preset);
                    if (l > 0)
                        continue;
                    l = -l;
                }
            }

            c_in = input[l];
            if (l != 0 && input.Length > 4 && l < (input.Length - 4) && preset.HCodeLensSpan[(int)SetAndState.Num] != 0)
            {
                if (c_in == input[l - 1] && c_in == input[l + 1] && c_in == input[l + 2] && c_in == input[l + 3])
                {
                    int rpt_count = l + 4;
                    while (rpt_count < input.Length && input[rpt_count] == c_in)
                        rpt_count++;
                    rpt_count -= l;
                    append_code(ref output, RPT_CODE, ref state, in preset);
                    encodeCount(ref output, rpt_count - 4);
                    l += rpt_count;
                    l--;
                    continue;
                }
            }

            if (l <= (input.Length - 36) && preset.HCodeLensSpan[(int)SetAndState.Num] != 0)
            {
                if (input[l + 8] == '-' && input[l + 13] == '-' && input[l + 18] == '-' && input[l + 23] == '-')
                {
                    NibbleType hex_type = NibbleType.Num;
                    int uid_pos = l;
                    for (; uid_pos < l + 36; uid_pos++)
                    {
                        byte c_uid = input[uid_pos];
                        if (c_uid == '-' && (uid_pos is 8 or 13 or 18 or 23))
                            continue;
                        NibbleType nib_type = getNibbleType(c_uid);
                        if (nib_type == NibbleType.Not)
                            break;
                        if (nib_type != NibbleType.Num)
                        {
                            if (hex_type != NibbleType.Num && hex_type != nib_type)
                                break;
                            hex_type = nib_type;
                        }
                    }
                    if (uid_pos == l + 36)
                    {
                        append_nibble_escape(ref output, state, in preset);
                        append_bits(ref output, hex_type == NibbleType.HexLower ? (byte)0xC0 : (byte)0xF0,
                               hex_type == NibbleType.HexLower ? 3 : 5);
                        for (uid_pos = l; uid_pos < l + 36; uid_pos++)
                        {
                            byte c_uid = input[uid_pos];
                            if (c_uid != '-')
                                append_bits(ref output, getBaseCode(c_uid), 4);
                        }
                        l += 35;
                        continue;
                    }
                }
            }
            if (l < (input.Length - 5) && preset.HCodeLensSpan[(int)SetAndState.Num] != 0)
            {
                NibbleType hex_type = NibbleType.Num;
                int hex_len = 0;
                do
                {
                    NibbleType nib_type = getNibbleType(input[l + hex_len]);
                    if (nib_type == NibbleType.Not)
                        break;
                    if (nib_type != NibbleType.Num)
                    {
                        if (hex_type != NibbleType.Num && hex_type != nib_type)
                            break;
                        hex_type = nib_type;
                    }
                    hex_len++;
                } while (l + hex_len < input.Length);
                if (hex_len > 10 && hex_type == NibbleType.Num)
                    hex_type = NibbleType.HexLower;
                if ((hex_type == NibbleType.HexLower || hex_type == NibbleType.HexUpper) && hex_len > 3)
                {
                    append_nibble_escape(ref output, state, in preset);
                    append_bits(ref output, hex_type == NibbleType.HexLower ? (byte)0x80 : (byte)0xE0, hex_type == NibbleType.HexLower ? 2 : 4);
                    encodeCount(ref output, hex_len);
                    do
                    {
                        append_bits(ref output, getBaseCode(input[l++]), 4);
                    } while (--hex_len != 0);
                    l--;
                    continue;
                }
            }
            if (preset.HasTemplates)
            {
                int i;
                for (i = 0; i < 5; i++)
                {
                    ReadOnlySpan<byte> template = preset.GetTemplatesSpan(i);
                    if (template.IsEmpty)
                        continue;
                    int rem = template.Length;
                    int j = 0;
                    for (; j < rem && l + j < input.Length; j++)
                    {
                        byte c_t = template[j];
                        c_in = input[l + j];
                        if (c_t is (byte)'f' or (byte)'F')
                        {
                            if (getNibbleType(c_in) != (c_t == 'f' ? NibbleType.HexLower : NibbleType.HexUpper)
                                     && getNibbleType(c_in) != NibbleType.Num)
                            {
                                break;
                            }
                        }
                        else if (c_t is (byte)'r' or (byte)'t' or (byte)'o')
                        {
                            if (c_in < '0' || c_in > (c_t switch
                            {
                                (byte)'r' => '7',
                                (byte)'t' => '3',
                                _ => '1'
                            }))
                                break;
                        }
                        else if (c_t != c_in)
                            break;
                    }
                    if (((float)j / rem) > 0.66f)
                    {
                        rem = rem - j;
                        append_nibble_escape(ref output, state, in preset);
                        append_bits(ref output, 0, 1);
                        append_bits(ref output, (byte)(count_codes[i] & 0xF8), count_codes[i] & 0x07);
                        encodeCount(ref output, rem);
                        for (int k = 0; k < j; k++)
                        {
                            byte c_t = template[k];
                            if (c_t is (byte)'f' or (byte)'F')
                                append_bits(ref output, getBaseCode(input[l + k]), 4);
                            else if (c_t is (byte)'r' or (byte)'t' or (byte)'o')
                            {
                                c_t = c_t switch
                                {
                                    (byte)'r' => 3,
                                    (byte)'t' => 3,
                                    _ => 1
                                };
                                append_bits(ref output, (byte)((input[l + k] - '0') << (8 - c_t)), c_t);
                            }
                        }
                        l += j;
                        l--;
                        break;
                    }
                }
                if (i < 5)
                    continue;
            }
            if (preset.HasFreqSeq)
            {
                int i;
                for (i = 0; i < 6; i++)
                {
                    ReadOnlySpan<byte> seq = preset.GetFreqSeqSpan(i);
                    int seq_len = seq.Length;
                    if (input.Length - seq_len >= 0 && l <= input.Length - seq_len)
                    {
                        if (seq.SequenceEqual(input.Slice(l, seq_len)) && preset.HCodeLensSpan[usx_freq_codes[i] >> 5] != 0)
                        {
                            append_code(ref output, usx_freq_codes[i], ref state, in preset);
                            l += seq_len;
                            l--;
                            break;
                        }
                    }
                }
                if (i < 6)
                    continue;
            }

            c_in = input[l];

            is_upper = false;
            if (c_in is >= (byte)'A' and <= (byte)'Z')
                is_upper = true;
            else
            {
                if (is_all_upper)
                {
                    is_all_upper = false;
                    append_switch_code(ref output, state);
                    append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                    state = SetAndState.Alpha;
                }
            }
            if (is_upper && !is_all_upper)
            {
                if (state == SetAndState.Num)
                {
                    append_switch_code(ref output, state);
                    append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                    state = SetAndState.Alpha;
                }
                append_switch_code(ref output, state);
                append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                if (state == SetAndState.Delta)
                {
                    state = SetAndState.Alpha;
                    append_switch_code(ref output, state);
                    append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                }
            }
            c_next = 0;
            if (l + 1 < input.Length)
                c_next = input[l + 1];

            if (c_in >= 32 && c_in <= 126)
            {
                if (is_upper && !is_all_upper)
                {
                    for (ll = l + 4; ll >= l && ll < input.Length; ll--)
                    {
                        if (input[ll] is < (byte)'A' or > (byte)'Z')
                            break;
                    }
                    if (ll == l - 1)
                    {
                        append_switch_code(ref output, state);
                        append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                        state = SetAndState.Alpha;
                        is_all_upper = true;
                    }
                }
                if (state == SetAndState.Delta && (c_in is (byte)' ' or (byte)'.' or (byte)','))
                {
                    byte spl_code = c_in switch
                    {
                        (byte)',' => 0xC0,
                        (byte)'t' => 0xE0,
                        (byte)' ' => 0,
                        _ => 0xFF
                    };
                    if (spl_code != 0xFF)
                    {
                        int spl_code_len = c_in switch
                        {
                            (byte)',' => 3,
                            (byte)'t' => 4,
                            (byte)' ' => 1,
                            _ => 4
                        };
                        append_bits(ref output, UNI_STATE_SPL_CODE, UNI_STATE_SPL_CODE_LEN);
                        append_bits(ref output, spl_code, spl_code_len);
                        continue;
                    }
                }
                c_in -= 32;
                if (is_all_upper && is_upper)
                    c_in += 32;
                if (c_in == 0)
                {
                    if (state == SetAndState.Num)
                        append_bits(ref output, usx_vcodes[NUM_SPC_CODE & 0x1F], usx_vcode_lens[NUM_SPC_CODE & 0x1F]);
                    else
                        append_bits(ref output, usx_vcodes[1], usx_vcode_lens[1]);
                }
                else
                {
                    c_in--;
                    append_code(ref output, usx_code_94[(int)c_in], ref state, in preset);
                }
            }
            else if (c_in == 13 && c_next == 10)
            {
                append_code(ref output, CRLF_CODE, ref state, in preset);
                l++;
            }
            else if (c_in == 10)
            {
                if (state == SetAndState.Delta)
                {
                    append_bits(ref output, UNI_STATE_SPL_CODE, UNI_STATE_SPL_CODE_LEN);
                    append_bits(ref output, 0xF0, 4);
                }
                else
                    append_code(ref output, LF_CODE, ref state, in preset);
            }
            else if (c_in == 13)
                append_code(ref output, CR_CODE, ref state, in preset);
            else if (c_in == '\t')
                append_code(ref output, TAB_CODE, ref state, in preset);
            else
            {
                int uni = readUTF8(input, l, out int utf8len);
                if (uni != 0)
                {
                    l += utf8len;
                    if (state != SetAndState.Delta)
                    {
                        int uni2 = readUTF8(input, l, out utf8len);
                        if (uni2 != 0)
                        {
                            if (state != SetAndState.Alpha)
                            {
                                append_switch_code(ref output, state);
                                append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                            }
                            append_switch_code(ref output, state);
                            append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Alpha], preset.HCodeLensSpan[(int)SetAndState.Alpha]);
                            append_bits(ref output, usx_vcodes[1], usx_vcode_lens[1]); // code for space (' ')
                            state = SetAndState.Delta;
                        }
                        else
                        {
                            append_switch_code(ref output, state);
                            append_bits(ref output, preset.HCodesSpan[(int)SetAndState.Delta], preset.HCodeLensSpan[(int)SetAndState.Delta]);
                        }
                    }
                    encodeUnicode(ref output, uni, prev_uni);
                    prev_uni = uni;
                    l--;
                }
                else
                {
                    int bin_count = 1;
                    for (int bi = l + 1; bi < input.Length; bi++)
                    {
                        byte c_bi = input[bi];
                        if (readUTF8(input, bi, out utf8len) != 0)
                            break;
                        if (bi < (input.Length - 4) && c_bi == input[bi - 1] && c_bi == input[bi + 1] && c_bi == input[bi + 2] && c_bi == input[bi + 3])
                            break;
                        bin_count++;
                    }
                    append_nibble_escape(ref output, state, in preset);
                    append_bits(ref output, 0xF8, 5);
                    encodeCount(ref output, bin_count);
                    do
                    {
                        append_bits(ref output, input[l++], 8);
                    } while (--bin_count != 0);
                    l--;
                }
            }
        }
        if (need_full_term_codes)
            append_final_bits_full(ref output, state, is_all_upper, in preset);
        else
            append_final_bits(ref output, state, is_all_upper, in preset);
    }
}