using UnishoxSharp.Common;

namespace UnishoxSharp.V1;

public partial class Unishox
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
        0,         (byte)' ', (byte)'e',  0,         (byte)'t', (byte)'a', (byte)'o', (byte)'i',  (byte)'n', (byte)'s', (byte)'r',
        0,         (byte)'l', (byte)'c',  (byte)'d', (byte)'h', (byte)'u', (byte)'p', (byte)'m',  (byte)'b', (byte)'g', (byte)'w',
        (byte)'f', (byte)'y', (byte)'v',  (byte)'k', (byte)'q', (byte)'j', (byte)'x', (byte)'z',  0,         0,         0,
        0,         (byte)'9', (byte)'0',  (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5',  (byte)'6', (byte)'7', (byte)'8',
        (byte)'.', (byte)',', (byte)'-',  (byte)'/', (byte)'=', (byte)'+', (byte)' ', (byte)'(',  (byte)')', (byte)'$', (byte)'%',
        (byte)'&', (byte)';', (byte)':',  (byte)'<', (byte)'>', (byte)'*', (byte)'"', (byte)'{',  (byte)'}', (byte)'[', (byte)']',
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
}
