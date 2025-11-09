using System.Buffers;
using UnishoxSharp.Common;

namespace UnishoxSharp.V1;

partial class Unishox
{
    static void AppendBits<T>(ref T output, uint code, int clen, State state)
        where T : IUnishoxDataOutput, allows ref struct
    {
        if (state == State.S2)
        {
            // remove change state prefix
            if ((code >> 9) == 0x1C)
            {
                code <<= 7;
                clen -= 7;
            }
        }
        output.WriteBits((ushort)code, clen);
    }

    static void EncodeCount<T>(ref T output, int count)
        where T : IUnishoxDataOutput, allows ref struct
    {
        // First five bits are code and Last three bits of codes represent length
        ReadOnlySpan<byte> codes = [0x01, 0x82, 0xC3, 0xE5, 0xED, 0xF5, 0xFD];
        ReadOnlySpan<byte> bit_len = [2, 5, 7, 9, 12, 16, 17];
        ReadOnlySpan<ushort> adder = [0, 4, 36, 164, 676, 4772, 0];
        int till = 0;
        for (int i = 0; i < 6; i++)
        {
            till += 1 << bit_len[i];
            if (count < till)
            {
                AppendBits(ref output, (codes[i] & 0xF8u) << 8, codes[i] & 0x07, State.S1);
                AppendBits(ref output, (uint)(count - adder[i]) << (16 - bit_len[i]), bit_len[i], State.S1);
                return;
            }
        }
    }

    static ReadOnlySpan<byte> UniBitLen => [6, 12, 14, 16, 21];
    static ReadOnlySpan<int> UniAdder => [0, 64, 4160, 20544, 86080];
    static void EncodeUnicode<T>(ref T output, int code, int prev_code)
        where T : IUnishoxDataOutput, allows ref struct
    {
        ushort spl_code = code switch
        {
            ',' => 0xE000,
            '.' or 0x3002 => 0xE800,
            ' ' => 0,
            13 => 0xF000,
            10 => 0xF800,
            _ => 0xFFFF
        };
        if (spl_code != 0xFFFF)
        {
            int spl_code_len = code switch
            {
                ',' => 5,
                '.' or 0x3002 => 5,
                ' ' => 1,
                13 => 5,
                10 => 5,
                _ => 0xFFFF
            };
            AppendBits(ref output, UNI_STATE_SPL_CODE, UNI_STATE_SPL_CODE_LEN, State.UNI);
            AppendBits(ref output, spl_code, spl_code_len, State.S1);
            return;
        }
        // First five bits are code and Last three bits of codes represent length
        ReadOnlySpan<byte> codes = [0x01, 0x82, 0xC3, 0xE4, 0xF5, 0xFD];
        int till = 0;
        for (int i = 0; i < 5; i++)
        {
            till += 1 << UniBitLen[i];
            int diff = Math.Abs(code - prev_code);
            if (diff < till)
            {
                AppendBits(ref output, (codes[i] & 0xF8u) << 8, codes[i] & 0x07, State.S1);
                AppendBits(ref output, prev_code > code ? 0x8000u : 0u, 1, State.S1);
                if (UniBitLen[i] > 16)
                {
                    int val = diff - UniAdder[i];
                    int excess_bits = UniBitLen[i] - 16;
                    AppendBits(ref output, (uint)(val >> excess_bits), 16, State.S1);
                    AppendBits(ref output, (uint)(val & ((1 << excess_bits) - 1)) << (16 - excess_bits), excess_bits, State.S1);
                }
                else
                    AppendBits(ref output, (uint)(diff - UniAdder[i]) << (16 - UniBitLen[i]), UniBitLen[i], State.S1);
                return;
            }
        }
    }

    static ReadOnlySpan<int> Utf8Mask => [0xE0, 0xF0, 0xF8];
    static ReadOnlySpan<int> Utf8Prefix => [0xC0, 0xE0, 0xF0];
    static int ReadUTF8(scoped ReadOnlySpan<byte> input, int l, out int utf8len)
    {
        int bc = 0;
        int uni = 0;
        if (l >= input.Length)
        {
            utf8len = 0;
            return 0;
        }
        byte c_in = input[l];
        for (; bc < 3; bc++)
        {
            if (Utf8Prefix[bc] == (c_in & Utf8Mask[bc]) && l + bc + 1 < input.Length)
            {
                int j = 0;
                uni = c_in & ~Utf8Mask[bc] & 0xFF;
                do
                {
                    uni <<= 6;
                    uni += input[l + j + 1] & 0x3F;
                } while (j++ < bc);
                break;
            }
        }
        if (bc < 3)
        {
            utf8len = bc + 1;
            return uni;
        }
        utf8len = 0;
        return 0;
    }

    static int MatchOccurance<T>(scoped ReadOnlySpan<byte> input, int l, ref T output, ref State state, ref bool is_all_upper)
        where T : IUnishoxDataOutput, allows ref struct
    {
        int longest_dist = 0;
        int longest_len = 0;
        for (int j = l - NICE_LEN, k; j >= 0; j--)
        {
            for (k = l; k < input.Length && j + k - l < l; k++)
            {
                if (input[k] != input[j + k - l])
                    break;
            }
            if (k < input.Length)
                while ((input[k] >> 6) == 2)
                    k--; // Skip partial UTF-8 matches
            if (k - l > NICE_LEN - 1)
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
            if (state == State.S2 || is_all_upper)
            {
                is_all_upper = false;
                state = State.S1;
                AppendBits(ref output, BACK2_STATE1_CODE, BACK2_STATE1_CODE_LEN, state);
            }
            if (state == State.UNI)
                AppendBits(ref output, UNI_STATE_DICT_CODE, UNI_STATE_DICT_CODE_LEN, State.UNI);
            else
                AppendBits(ref output, DICT_CODE, DICT_CODE_LEN, State.S1);
            EncodeCount(ref output, longest_len);
            EncodeCount(ref output, longest_dist);
            l += longest_len + NICE_LEN;
            l--;
            return l;
        }
        return -l;
    }

    static int MatchLine<T>(scoped ReadOnlySpan<byte> input, int l, ref T output, UnishoxLinkList? prev_lines, ref State state, ref bool is_all_upper)
        where T : IUnishoxDataOutput, allows ref struct
    {
        int last_op = output.Position;
        int last_ob = output.RemainingBits;
        int last_len = 0;
        int last_dist = 0;
        int line_ctr = 0;
        while (prev_lines is not null)
        {
            ReadOnlySpan<byte> prev_lines_data = prev_lines.Data.Span;
            int line_len = prev_lines_data.Length;
            int limit = line_ctr == 0 ? l : line_len;
            for (int j = 0; j < limit; j++)
            {
                int i, k;
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
                    if (state == State.S2 || is_all_upper)
                    {
                        is_all_upper = false;
                        state = State.S1;
                        AppendBits(ref output, BACK2_STATE1_CODE, BACK2_STATE1_CODE_LEN, state);
                    }
                    if (state == State.UNI)
                        AppendBits(ref output, UNI_STATE_DICT_CODE, UNI_STATE_DICT_CODE_LEN, State.UNI);
                    else
                        AppendBits(ref output, DICT_CODE, DICT_CODE_LEN, State.S1);
                    EncodeCount(ref output, last_len - NICE_LEN);
                    EncodeCount(ref output, last_dist);
                    EncodeCount(ref output, last_ctx);
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

    public static int CompressCount(
        ReadOnlySpan<byte> input,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        DummyOutput o = new();
        Compress(input, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int Compress(
        ReadOnlySpan<byte> input,
        Stream output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        StreamOutput o = new(output);
        Compress(input, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int Compress(
        ReadOnlySpan<byte> input,
        Span<byte> output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        SpanOutput o = new(output);
        Compress(input, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static void Compress<T>(
        ReadOnlySpan<byte> input,
        ref T output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
        where T : IUnishoxDataOutput, allows ref struct
    {
        State state = State.S1;
        int prev_uni = 0;
        bool is_all_upper = false;
        IMemoryOwner<byte>? mem = null;
        Span<byte> lookup = [];
        if (use_64k_lookup)
        {
            mem = MemoryPool<byte>.Shared.Rent(65536);
            lookup = mem.Memory.Span;
            lookup.Clear();
        }
        using IMemoryOwner<byte>? memo = mem;
        for (int l = 0; l < input.Length; l++)
        {
            byte c_in = input[l];
            if (state != State.UNI && l != 0 && l < input.Length - 4)
            {
                if (c_in == input[l - 1] && c_in == input[l + 1] && c_in == input[l + 2] && c_in == input[l + 3])
                {
                    int rpt_count = l + 4;
                    while (rpt_count < input.Length && input[rpt_count] == c_in)
                        rpt_count++;
                    rpt_count -= l;
                    if (state == State.S2 || is_all_upper)
                    {
                        is_all_upper = false;
                        state = State.S1;
                        AppendBits(ref output, BACK2_STATE1_CODE, BACK2_STATE1_CODE_LEN, state);
                    }
                    AppendBits(ref output, RPT_CODE, RPT_CODE_LEN, State.S1);
                    EncodeCount(ref output, rpt_count - 4);
                    l += rpt_count;
                    l--;
                    continue;
                }
            }
            if (to_match_repeats && l < (input.Length - NICE_LEN + 1))
            {
                if (prev_lines is not null)
                {
                    l = MatchLine(input, l, ref output, prev_lines, ref state, ref is_all_upper);
                    if (l > 0)
                    {
                        continue;
                    }
                    l = -l;
                }
                else if (use_64k_lookup)
                {
                    int to_lookup = c_in ^ input[l + 1] + ((input[l + 2] ^ input[l + 3]) << 8);
                    if (lookup[to_lookup] != 0)
                    {
                        l = MatchOccurance(input, l, ref output, ref state, ref is_all_upper);
                        if (l > 0)
                            continue;
                        l = -l;
                    }
                    else
                        lookup[to_lookup] = 1;
                }
                else
                {
                    l = MatchOccurance(input, l, ref output, ref state, ref is_all_upper);
                    if (l > 0)
                        continue;
                    l = -l;
                }
            }
            if (state == State.UNI && (c_in is (byte)'.' or (byte)' ' or (byte)',' or 13 or 10))
            {
                EncodeUnicode(ref output, c_in, prev_uni);
                continue;
            }
            if (state == State.S2)
            {
                if (c_in is (>= (byte)' ' and <= (byte)'@')
                    or (>= (byte)'[' and <= (byte)'`')
                    or (>= (byte)'{' and <= (byte)'~'))
                {
                }
                else
                {
                    state = State.S1;
                    AppendBits(ref output, BACK2_STATE1_CODE, BACK2_STATE1_CODE_LEN, state);
                }
            }
            if (state == State.UNI && c_in is >= 0 and <= 127)
            {
                AppendBits(ref output, BACK_FROM_UNI_CODE, BACK_FROM_UNI_CODE_LEN, state);
                state = State.S1;
            }
            bool is_upper = false;
            if (c_in is >= (byte)'A' and <= (byte)'Z')
                is_upper = true;
            else
            {
                if (is_all_upper)
                {
                    is_all_upper = false;
                    AppendBits(ref output, BACK2_STATE1_CODE, BACK2_STATE1_CODE_LEN, state);
                }
            }
            byte c_next = 0;
            if (l + 1 < input.Length)
                c_next = input[l + 1];

            if (c_in is >= 32 and <= 126)
            {
                if (is_upper && !is_all_upper)
                {
                    int ll;
                    for (ll = l + 5; ll >= l && ll < input.Length; ll--)
                    {
                        if (input[ll] is >= (byte)'A' and <= (byte)'Z')
                            break;
                    }
                    if (ll == l - 1)
                    {
                        AppendBits(ref output, ALL_UPPER_CODE, ALL_UPPER_CODE_LEN, state);
                        is_all_upper = true;
                    }
                }
                if (state == State.S1 && c_in is >= (byte)'0' and <= (byte)'9')
                {
                    AppendBits(ref output, SW2_STATE2_CODE, SW2_STATE2_CODE_LEN, state);
                    state = State.S2;
                }
                c_in -= 32;
                if (is_all_upper && is_upper)
                    c_in += 32;
                if (c_in == 0 && state == State.S2)
                    AppendBits(ref output, ST2_SPC_CODE, ST2_SPC_CODE_LEN, state);
                else
                    AppendBits(ref output, C95[c_in], L95[c_in], state);
            }
            else if (c_in == 13 && c_next == 10)
            {
                AppendBits(ref output, CRLF_CODE, CRLF_CODE_LEN, state);
                l++;
            }
            else if (c_in == 10)
                AppendBits(ref output, LF_CODE, LF_CODE_LEN, state);
            else if (c_in == '\t')
                AppendBits(ref output, TAB_CODE, TAB_CODE_LEN, state);
            else
            {
                int uni = ReadUTF8(input, l, out int utf8len);
                if (uni != 0)
                {
                    l += utf8len;
                    if (state != State.UNI)
                    {
                        int uni2 = ReadUTF8(input, l + 1, out utf8len);
                        if (uni2 != 0)
                        {
                            state = State.UNI;
                            AppendBits(ref output, CONT_UNI_CODE, CONT_UNI_CODE_LEN, State.S1);
                        }
                        else
                            AppendBits(ref output, UNI_CODE, UNI_CODE_LEN, State.S1);
                    }
                    EncodeUnicode(ref output, uni, prev_uni);
                    if (uni != 0x3002)
                        prev_uni = uni;
                }
                else
                {
                    if (state == State.UNI)
                    {
                        state = State.S1;
                        AppendBits(ref output, BACK_FROM_UNI_CODE, BACK_FROM_UNI_CODE_LEN, state);
                    }
                    AppendBits(ref output, BIN_CODE, BIN_CODE_LEN, state);
                    EncodeCount(ref output, c_in);
                }
            }
        }
        if (state == State.UNI)
            AppendBits(ref output, BACK_FROM_UNI_CODE, BACK_FROM_UNI_CODE_LEN, state);
        if (output.RemainingBits > 0)
            AppendBits(ref output, TERM_CODE, 8 - output.RemainingBits, State.S1);
        output.FlushBits();
    }
}
