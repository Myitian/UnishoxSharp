using UnishoxSharp.Common;

namespace UnishoxSharp.V2;

public partial class Unishox
{
    /// <summary>
    /// possible horizontal sets and states
    /// </summary>
    enum SetAndState
    {
        Alpha,
        Sym,
        Num,
        Dict,
        Delta,
        NumTemp
    };
    /// <summary>
    /// Enum indicating nibble type -<br />
    /// <see cref="Num" /> means ch is a number '0' to '9',<br />
    /// <see cref="HexLower" /> means ch is between 'a' to 'f',<br />
    /// <see cref="HexUpper" /> means ch is between 'A' to 'F'
    /// </summary>
    enum NibbleType
    {
        Num,
        HexLower,
        HexUpper,
        Not
    };

    /// <summary>
    /// This 2D array has the characters for the sets <see cref="SetAndState.Alpha" />, <see cref="SetAndState.Sym" /> and <see cref="SetAndState.Num" />.
    /// Where a character cannot fit into a <see cref="byte" />, 0 is used and handled in code.
    /// </summary>
    static ReadOnlySpan<byte> Sets => [
        0,         (byte)' ', (byte)'e', (byte)'t', (byte)'a', (byte)'o', (byte)'i', (byte)'n',
        (byte)'s', (byte)'r', (byte)'l', (byte)'c', (byte)'d', (byte)'h', (byte)'u', (byte)'p', (byte)'m', (byte)'b',
        (byte)'g', (byte)'w', (byte)'f', (byte)'y', (byte)'v', (byte)'k', (byte)'q', (byte)'j', (byte)'x', (byte)'z',

        (byte)'"', (byte)'{', (byte)'}', (byte)'_', (byte)'<', (byte)'>', (byte)':', (byte)'\n',
        0,         (byte)'[', (byte)']', (byte)'\\',(byte)';', (byte)'\'',(byte)'\t',(byte)'@', (byte)'*', (byte)'&',
        (byte)'?', (byte)'!', (byte)'^', (byte)'|', (byte)'\r',(byte)'~', (byte)'`', 0,         0,         0,

        0,         (byte)',', (byte)'.', (byte)'0', (byte)'1', (byte)'9', (byte)'2', (byte)'5',
        (byte)'-', (byte)'/', (byte)'3', (byte)'4', (byte)'6', (byte)'7', (byte)'8', (byte)'(', (byte)')', (byte)' ',
        (byte)'=', (byte)'+', (byte)'$', (byte)'%', (byte)'#', 0,         0,         0,         0,         0];
    const int SetsDim1Length = 28;

    /// <summary>
    /// Stores position of letter in usx_sets.
    /// </summary>
    /// <remarks>
    /// First 3 bits - position in usx_hcodes<br />
    /// Next  5 bits - position in usx_vcodes
    /// </remarks>
    static Array94<byte> Code94 = new();

    /// <summary>
    /// Vertical codes starting from the MSB
    /// </summary>
    static ReadOnlySpan<byte> VCodes => [
        0x00, 0x40, 0x60, 0x80, 0x90, 0xA0, 0xB0,
        0xC0, 0xD0, 0xD8, 0xE0, 0xE4, 0xE8, 0xEC,
        0xEE, 0xF0, 0xF2, 0xF4, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF ];

    /// <summary>
    /// Length of each veritical code
    /// </summary>
    static ReadOnlySpan<byte> VCodeLens => [
        2,    3,    3,    4,    4,    4,    4,
        4,    5,    5,    6,    6,    6,    7,
        7,    7,    7,    7,    8,    8,    8,
        8,    8,    8,    8,    8,    8,    8 ];

    /// <summary>
    /// Vertical Codes and Set number for frequent sequences in sets <see cref="SetAndState.Sym" /> and <see cref="SetAndState.Num" />.
    /// First 3 bits indicate set (<see cref="SetAndState.Sym" />/<see cref="SetAndState.Num" />) and rest are vcode positions
    /// </summary>
    static ReadOnlySpan<byte> FreqCodes => [(1 << 5) + 25, (1 << 5) + 26, (1 << 5) + 27, (2 << 5) + 23, (2 << 5) + 24, (2 << 5) + 25];

    /// <summary>
    /// Minimum length to consider as repeating sequence
    /// </summary>
    const int NICE_LEN = 5;

    /// <summary>
    /// Set (<see cref="SetAndState.Num" /> - 2) and vertical code (26) for encoding repeating letters
    /// </summary>
    const int RPT_CODE = (2 << 5) + 26;
    /// <summary>
    /// Set (<see cref="SetAndState.Num" /> - 2) and vertical code (27) for encoding terminator
    /// </summary>
    const int TERM_CODE = (2 << 5) + 27;
    /// <summary>
    /// Set (<see cref="SetAndState.Sym" /> - 1) and vertical code (7) for encoding Line feed \n
    /// </summary>
    const int LF_CODE = (1 << 5) + 7;
    /// <summary>
    /// Set (<see cref="SetAndState.Num" /> - 1) and vertical code (8) for encoding \r\n
    /// </summary>
    const int CRLF_CODE = (1 << 5) + 8;
    /// <summary>
    /// Set (<see cref="SetAndState.Num" /> - 1) and vertical code (22) for encoding \r
    /// </summary>
    const int CR_CODE = (1 << 5) + 22;
    /// <summary>
    /// Set (<see cref="SetAndState.Num" /> - 1) and vertical code (14) for encoding \t
    /// </summary>
    const int TAB_CODE = (1 << 5) + 14;
    /// <summary>
    /// Set (<see cref="SetAndState.Num" /> - 2) and vertical code (17) for space character when it appears in <see cref="SetAndState.Num" /> state \\r
    /// </summary>
    const int NUM_SPC_CODE = (2 << 5) + 17;

    /// <summary>
    /// Code for special code (11111) when state=<see cref="SetAndState.Delta" />
    /// </summary>
    const int UNI_STATE_SPL_CODE = 0xF8;
    /// <summary>
    /// Length of Code for special code when state=<see cref="SetAndState.Delta" />
    /// </summary>
    const int UNI_STATE_SPL_CODE_LEN = 5;
    /// <summary>
    /// Code for switch code when state=<see cref="SetAndState.Delta" />
    /// </summary>
    const int UNI_STATE_SW_CODE = 0x80;
    /// <summary>
    /// Length of Code for Switch code when state=<see cref="SetAndState.Delta" />
    /// </summary>
    const int UNI_STATE_SW_CODE_LEN = 2;

    /// <summary>
    /// Switch code in <see cref="SetAndState.Alpha" /> and <see cref="SetAndState.Num" /> 00
    /// </summary>
    const int SW_CODE = 0;
    /// <summary>
    /// Length of Switch code
    /// </summary>
    const int SW_CODE_LEN = 2;
    /// <summary>
    /// Terminator bit sequence for Preset 1. Length varies depending on state as per following macros
    /// </summary>
    const int TERM_BYTE_PRESET_1 = 0;
    /// <summary>
    /// Length of Terminator bit sequence when state is lower
    /// </summary>
    const int TERM_BYTE_PRESET_1_LEN_LOWER = 6;
    /// <summary>
    /// Length of Terminator bit sequence when state is upper
    /// </summary>
    const int TERM_BYTE_PRESET_1_LEN_UPPER = 4;

    /// <summary>
    /// Offset at which <see cref="Code94" /> starts
    /// </summary>
    const int USX_OFFSET_94 = 33;

    static Unishox()
    {
        Span<byte> usx_code_94 = Code94;
        usx_code_94.Clear();
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < SetsDim1Length; j++)
            {
                byte c = Sets[i * SetsDim1Length + j];
                if (c > 32)
                {
                    usx_code_94[c - USX_OFFSET_94] = (byte)((i << 5) + j);
                    if (c >= 'a' && c <= 'z')
                        usx_code_94[c - USX_OFFSET_94 - ('a' - 'A')] = (byte)((i << 5) + j);
                }
            }
        }
    }
}