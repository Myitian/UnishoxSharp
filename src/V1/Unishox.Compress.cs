using System.Buffers;
using UnishoxSharp.Common;

namespace UnishoxSharp.V1;

partial class Unishox
{
    static void AppendBits<TOut>(ref TOut output, uint code, int clen, State state)
        where TOut : IUnishoxDataOutput, allows ref struct
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

    static ReadOnlySpan<byte> CountCodes => [0x01, 0x82, 0xC3, 0xE5, 0xED, 0xF5, 0xFD];
    static ReadOnlySpan<byte> CountBitLenE => [2, 5, 7, 9, 12, 16, 17];
    static ReadOnlySpan<ushort> CountAdderE => [0, 4, 36, 164, 676, 4772, 0];
    static void EncodeCount<TOut>(ref TOut output, int count)
        where TOut : IUnishoxDataOutput, allows ref struct
    {
        // First five bits are code and Last three bits of codes represent length
        int till = 0;
        for (int i = 0; i < 6; i++)
        {
            till += 1 << CountBitLenE[i];
            if (count < till)
            {
                AppendBits(ref output, (CountCodes[i] & 0xF8u) << 8, CountCodes[i] & 0x07, State.S1);
                AppendBits(ref output, (uint)(count - CountAdderE[i]) << (16 - CountBitLenE[i]), CountBitLenE[i], State.S1);
                return;
            }
        }
    }

    static ReadOnlySpan<byte> UniCodes => [0x01, 0x82, 0xC3, 0xE4, 0xF5, 0xFD];
    static ReadOnlySpan<byte> UniBitLen => [6, 12, 14, 16, 21];
    static ReadOnlySpan<int> UniAdder => [0, 64, 4160, 20544, 86080];
    static void EncodeUnicode<TOut>(ref TOut output, int code, int prev_code)
        where TOut : IUnishoxDataOutput, allows ref struct
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
        int till = 0;
        for (int i = 0; i < 5; i++)
        {
            till += 1 << UniBitLen[i];
            int diff = Math.Abs(code - prev_code);
            if (diff < till)
            {
                AppendBits(ref output, (UniCodes[i] & 0xF8u) << 8, UniCodes[i] & 0x07, State.S1);
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
    static int ReadUTF8<TIn>(ref TIn input, out int utf8len, int offset = 0)
        where TIn : IUnishoxTextInput, allows ref struct
    {
        int input_pos = input.Position + offset;
        int input_len = input.Length;
        int bc = 0;
        int uni = 0;
        if (input_pos >= input_len)
        {
            utf8len = 0;
            return 0;
        }
        int c_in = input.ReadByteAt(input_pos);
        for (; bc < 3; bc++)
        {
            if (Utf8Prefix[bc] == (c_in & Utf8Mask[bc]) && input_pos + bc + 1 < input_len)
            {
                int j = 0;
                uni = c_in & ~Utf8Mask[bc] & 0xFF;
                do
                {
                    uni <<= 6;
                    uni += input.ReadByteAt(input_pos + j + 1) & 0x3F;
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

    static bool MatchOccurance<TIn, TOut>(ref TIn input, ref TOut output, ref State state, ref bool is_all_upper)
        where TIn : IUnishoxTextInput, allows ref struct
        where TOut : IUnishoxDataOutput, allows ref struct
    {
        int input_pos = input.Position;
        int input_len = input.Length;
        int longest_dist = 0;
        int longest_len = 0;
        for (int j = input_pos - NICE_LEN; j >= 0; j--)
        {
            int k1 = input_pos;
            int k2 = j;
            for (; k1 < input_len && k2 < input_pos; k1++, k2++)
            {
                if (input.ReadByteAt(k1) != input.ReadByteAt(k2))
                    break;
            }
            if (k1 < input_len)
                while ((input.ReadByteAt(k1) >> 6) == 2)
                    k1--; // Skip partial UTF-8 matches
            if (k1 - input_pos > NICE_LEN - 1)
            {
                int match_len = k1 - input_pos - NICE_LEN;
                int match_dist = input_pos - j - NICE_LEN + 1;
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
            input.Position = input_pos + longest_len + NICE_LEN - 1;
            return true;
        }
        return false;
    }

    static bool MatchLine<TIn, TOut>(ref TIn input, ref TOut output, UnishoxLinkList? prev_lines, ref State state, ref bool is_all_upper)
        where TIn : IUnishoxTextInput, allows ref struct
        where TOut : IUnishoxDataOutput, allows ref struct
    {
        int input_pos = input.Position;
        int input_len = input.Length;
        int last_op = output.Position;
        int last_ob = output.RemainingBits;
        int last_len = 0;
        int last_dist = 0;
        int line_ctr = 0;
        while (prev_lines is not null)
        {
            ReadOnlySpan<byte> prev_lines_data = prev_lines.Data.Span;
            int line_len = prev_lines_data.Length;
            int limit = line_ctr == 0 ? input_pos : line_len;
            for (int j = 0; j < limit; j++)
            {
                int k1 = j;
                int k2 = input_pos;
                for (; k1 < line_len && k2 < input_len; k1++, k2++)
                {
                    if (prev_lines_data[k1] != input.ReadByteAt(k2))
                        break;
                }
                if (k1 < line_len)
                    while ((prev_lines_data[k1] >> 6) == 2)
                        k1--; // Skip partial UTF-8 matches
                if ((k1 - j) >= NICE_LEN)
                {
                    if (last_len != 0)
                    {
                        if (j > last_dist)
                            continue;
                        output.SeekBit(last_op, last_ob);
                    }
                    last_len = k1 - j;
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
            return (input.Position = input_pos + last_len - 1) != 0;
        return false;
    }

    public static int CompressCount(
        Stream input,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        StreamInput i = new(input);
        DummyOutput o = new();
        Compress(ref i, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int Compress(
        Stream input,
        Stream output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        StreamInput i = new(input);
        StreamOutput o = new(output);
        Compress(ref i, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int Compress(
        Stream input,
        Span<byte> output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        StreamInput i = new(input);
        SpanOutput o = new(output);
        Compress(ref i, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int CompressCount(
        ReadOnlySpan<byte> input,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        SpanInput i = new(input);
        DummyOutput o = new();
        Compress(ref i, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int Compress(
        ReadOnlySpan<byte> input,
        Stream output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        SpanInput i = new(input);
        StreamOutput o = new(output);
        Compress(ref i, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static int Compress(
        ReadOnlySpan<byte> input,
        Span<byte> output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
    {
        SpanInput i = new(input);
        SpanOutput o = new(output);
        Compress(ref i, ref o, prev_lines, to_match_repeats, use_64k_lookup);
        return o.Position;
    }
    public static void Compress<TIn, TOut>(
        ref TIn input,
        ref TOut output,
        UnishoxLinkList? prev_lines = null,
        bool to_match_repeats = true,
        bool use_64k_lookup = true)
        where TIn : IUnishoxTextInput, allows ref struct
        where TOut : IUnishoxDataOutput, allows ref struct
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
        for (; input.Position < input.Length; input.Position++)
        {
            byte c_in = (byte)input.ReadByteAt(input.Position);
            if (state != State.UNI && input.Position != 0 && input.Position < input.Length - 4)
            {
                if (c_in == input.ReadByteAt(input.Position - 1)
                    && c_in == input.ReadByteAt(input.Position + 1)
                    && c_in == input.ReadByteAt(input.Position + 2)
                    && c_in == input.ReadByteAt(input.Position + 3))
                {
                    int rpt_count = input.Position + 4;
                    while (rpt_count < input.Length && input.ReadByteAt(rpt_count) == c_in)
                        rpt_count++;
                    rpt_count -= input.Position;
                    if (state == State.S2 || is_all_upper)
                    {
                        is_all_upper = false;
                        state = State.S1;
                        AppendBits(ref output, BACK2_STATE1_CODE, BACK2_STATE1_CODE_LEN, state);
                    }
                    AppendBits(ref output, RPT_CODE, RPT_CODE_LEN, State.S1);
                    EncodeCount(ref output, rpt_count - 4);
                    input.Position += rpt_count - 1;
                    continue;
                }
            }
            if (to_match_repeats && input.Position < (input.Length - NICE_LEN + 1))
            {
                if (prev_lines is not null)
                {
                    bool success = MatchLine(ref input, ref output, prev_lines, ref state, ref is_all_upper);
                    if (success)
                        continue;
                }
                else if (use_64k_lookup)
                {
                    int to_lookup = c_in ^ input.ReadByteAt(input.Position + 1) + ((input.ReadByteAt(input.Position + 2) ^ input.ReadByteAt(input.Position + 3)) << 8);
                    if (lookup[to_lookup] != 0)
                    {
                        bool success = MatchOccurance(ref input, ref output, ref state, ref is_all_upper);
                        if (success)
                            continue;
                    }
                    else
                        lookup[to_lookup] = 1;
                }
                else
                {
                    bool success = MatchOccurance(ref input, ref output, ref state, ref is_all_upper);
                    if (success)
                        continue;
                }
            }
            if (state == State.UNI && (c_in is (byte)'.' or (byte)' ' or (byte)',' or 13 or 10))
            {
                EncodeUnicode(ref output, c_in, prev_uni);
                continue;
            }
            if (state == State.S2)
            {
                if (c_in is not ((>= (byte)' ' and <= (byte)'@')
                              or (>= (byte)'[' and <= (byte)'`')
                              or (>= (byte)'{' and <= (byte)'~')))
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
            int c_next = 0;
            if (input.Position + 1 < input.Length)
                c_next = input.ReadByteAt(input.Position + 1);

            if (c_in is >= 32 and <= 126)
            {
                if (is_upper && !is_all_upper)
                {
                    int ll;
                    for (ll = input.Position + 5; ll >= input.Position && ll < input.Length; ll--)
                    {
                        if (input.ReadByteAt(ll) is >= (byte)'A' and <= (byte)'Z')
                            break;
                    }
                    if (ll == input.Position - 1)
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
                input.Position = input.Position + 1;
            }
            else if (c_in == 10)
                AppendBits(ref output, LF_CODE, LF_CODE_LEN, state);
            else if (c_in == '\t')
                AppendBits(ref output, TAB_CODE, TAB_CODE_LEN, state);
            else
            {
                int uni = ReadUTF8(ref input, out int utf8len);
                if (uni != 0)
                {
                    input.Position += utf8len;
                    if (state != State.UNI)
                    {
                        int uni2 = ReadUTF8(ref input, out utf8len, 1);
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
