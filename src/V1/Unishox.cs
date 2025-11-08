using System.Buffers;
using UnishoxSharp.Common;

namespace UnishoxSharp.V1;

public class Unishox
{
    enum State
    {
        S1 = 1,
        S2,
        UNI
    };
    enum Set
    {
        S1,
        S1A,
        S1B,
        S2,
        S3,
        S4,
        S4A
    };

    static ReadOnlySpan<byte> VCodes => [0, 2, 3, 4, 10, 11, 12, 13, 14, 30, 31];
    static ReadOnlySpan<byte> VCodeLens => [2, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5];
    static ReadOnlySpan<byte> Sets => [
        0, (byte)' ', (byte)'e',   0, (byte)'t', (byte)'a', (byte)'o', (byte)'i', (byte)'n', (byte)'s', (byte)'r',
        0, (byte)'l', (byte)'c', (byte)'d', (byte)'h', (byte)'u', (byte)'p', (byte)'m', (byte)'b', (byte)'g', (byte)'w',
        (byte)'f', (byte)'y', (byte)'v', (byte)'k', (byte)'q', (byte)'j', (byte)'x', (byte)'z',   0,   0,   0,
        0, (byte)'9', (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8',
        (byte)'.', (byte)',', (byte)'-', (byte)'/', (byte)'=', (byte)'+', (byte)' ', (byte)'(', (byte)')', (byte)'$', (byte)'%',
        (byte)'&', (byte)';', (byte)':', (byte)'<', (byte)'>', (byte)'*', (byte)'"', (byte)'{', (byte)'}', (byte)'[', (byte)']',
        (byte)'@', (byte)'?', (byte)'\'', (byte)'^', (byte)'#', (byte)'_', (byte)'!', (byte)'\\', (byte)'|', (byte)'~', (byte)'`'];

    static readonly Array95<uint> C95 = new();
    static readonly Array95<byte> L95 = new();

    const int NICE_LEN = 5;

    const int TERM_CODE = 0x37C0;
    const int TERM_CODE_LEN = 10;
    const int DICT_CODE = 0x0000;
    const int DICT_CODE_LEN = 5;
    const int DICT_OTHER_CODE = 0x0000;// not used
    const int DICT_OTHER_CODE_LEN = 6;
    const int RPT_CODE = 0x2370;
    const int RPT_CODE_LEN = 13;
    const int BACK2_STATE1_CODE = 0x2000;
    const int BACK2_STATE1_CODE_LEN = 4;
    const int BACK_FROM_UNI_CODE = 0xFE00;
    const int BACK_FROM_UNI_CODE_LEN = 8;
    const int CRLF_CODE = 0x3780;
    const int CRLF_CODE_LEN = 10;
    const int LF_CODE = 0x3700;
    const int LF_CODE_LEN = 9;
    const int TAB_CODE = 0x2400;
    const int TAB_CODE_LEN = 7;
    const int UNI_CODE = 0x8000;
    const int UNI_CODE_LEN = 3;
    const int UNI_STATE_SPL_CODE = 0xF800;
    const int UNI_STATE_SPL_CODE_LEN = 5;
    const int UNI_STATE_DICT_CODE = 0xFC00;
    const int UNI_STATE_DICT_CODE_LEN = 7;
    const int CONT_UNI_CODE = 0x2800;
    const int CONT_UNI_CODE_LEN = 7;
    const int ALL_UPPER_CODE = 0x2200;
    const int ALL_UPPER_CODE_LEN = 8;
    const int SW2_STATE2_CODE = 0x3800;
    const int SW2_STATE2_CODE_LEN = 7;
    const int ST2_SPC_CODE = 0x3B80;
    const int ST2_SPC_CODE_LEN = 11;
    const int BIN_CODE = 0x2000;
    const int BIN_CODE_LEN = 9;

    static Unishox()
    {
        for (Set i = Set.S1; i <= Set.S4A; i++)
        {
            for (int j = 0; j < 11; j++)
            {
                byte c = Sets[(int)i * 11 + j];
                if (c != 0 && c != 32)
                {
                    int ascii = c - 32;
                    switch (i)
                    {
                        case Set.S1: // just us_vcode
                            C95[ascii] = (uint)VCodes[j] << (16 - VCodeLens[j]);
                            L95[ascii] = VCodeLens[j];
                            if (c >= 'a' && c <= 'z')
                            {
                                ascii -= 'a' - 'A';
                                C95[ascii] = (2u << 12) + ((uint)VCodes[j] << (12 - VCodeLens[j]));
                                L95[ascii] = (byte)(4 + VCodeLens[j]);
                            }
                            break;
                        case Set.S1A: // 000 + us_vcode
                            C95[ascii] = 0 + ((uint)VCodes[j] << (13 - VCodeLens[j]));
                            L95[ascii] = (byte)(3 + VCodeLens[j]);
                            if (c >= 'a' && c <= 'z')
                            {
                                ascii -= 'a' - 'A';
                                C95[ascii] = (2 << 12) + 0 + ((uint)VCodes[j] << (9 - VCodeLens[j]));
                                L95[ascii] = (byte)(4 + 3 + VCodeLens[j]);
                            }
                            break;
                        case Set.S1B: // 00110 + us_vcode
                            C95[ascii] = (6 << 11) + ((uint)VCodes[j] << (11 - VCodeLens[j]));
                            L95[ascii] = (byte)(5 + VCodeLens[j]);
                            if (c >= 'a' && c <= 'z')
                            {
                                ascii -= 'a' - 'A';
                                C95[ascii] = (2 << 12) + (6 << 7) + ((uint)VCodes[j] << (7 - VCodeLens[j]));
                                L95[ascii] = (byte)(4 + 5 + VCodeLens[j]);
                            }
                            break;
                        case Set.S2: // 0011100 + us_vcode
                            C95[ascii] = (28 << 9) + ((uint)VCodes[j] << (9 - VCodeLens[j]));
                            L95[ascii] = (byte)(7 + VCodeLens[j]);
                            break;
                        case Set.S3: // 0011101 + us_vcode
                            C95[ascii] = (29 << 9) + ((uint)VCodes[j] << (9 - VCodeLens[j]));
                            L95[ascii] = (byte)(7 + VCodeLens[j]);
                            break;
                        case Set.S4: // 0011110 + us_vcode
                            C95[ascii] = (30 << 9) + ((uint)VCodes[j] << (9 - VCodeLens[j]));
                            L95[ascii] = (byte)(7 + VCodeLens[j]);
                            break;
                        case Set.S4A: // 0011111 + us_vcode
                            C95[ascii] = (31 << 9) + ((uint)VCodes[j] << (9 - VCodeLens[j]));
                            L95[ascii] = (byte)(7 + VCodeLens[j]);
                            break;
                    }
                }
            }
        }
        C95[0] = 16384;
        L95[0] = 3;
    }

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
    static int ReadUTF8(ReadOnlySpan<byte> input, int l, out int utf8len)
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

    static int MatchOccurance<T>(ReadOnlySpan<byte> input, int l, ref T output, ref State state, ref bool is_all_upper)
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

    static int MatchLine<T>(ReadOnlySpan<byte> input, int l, ref T output, UnishoxLinkList? prev_lines, ref State state, ref bool is_all_upper)
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
                if (k < prev_lines_data.Length)
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
