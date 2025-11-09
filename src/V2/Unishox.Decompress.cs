namespace UnishoxSharp.V2;

internal partial class Unishox
{
    /// <summary>
    /// Reads one bit from in
    /// </summary>
    int readBit(ReadOnlySpan<byte> input, long bit_no)
    {
        return input[(int)(bit_no >> 3)] & (0x80 >> (int)(bit_no % 8));
    }

    /// <summary>
    /// Reads next 8 bits, if available
    /// </summary>
    int read8bitCode(ReadOnlySpan<byte> input, long bit_no)
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
    /// The list of veritical codes is split into 5 sections. Used by readVCodeIdx()
    /// </summary>
    const int SECTION_COUNT = 5;
    /// <summary>
    /// Used by readVCodeIdx() for finding the section under which the code read using read8bitCode() falls
    /// </summary>
    static ReadOnlySpan<byte> usx_vsections => [0x7F, 0xBF, 0xDF, 0xEF, 0xFF];
    /// <summary>
    /// Used by readVCodeIdx() for finding the section vertical position offset
    /// </summary>
    static ReadOnlySpan<byte> usx_vsection_pos => [0, 4, 8, 12, 20];
    /// <summary>
    /// Used by readVCodeIdx() for masking the code read by read8bitCode()
    /// </summary>
    static ReadOnlySpan<byte> usx_vsection_mask => [0x7F, 0x3F, 0x1F, 0x0F, 0x0F];
    /// <summary>
    /// Used by readVCodeIdx() for shifting the code read by read8bitCode() to obtain the vpos
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
}