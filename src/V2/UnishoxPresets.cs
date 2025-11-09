namespace UnishoxSharp.V2;

public class UnishoxPresets
{
    /// <summary>
    /// Default Horizontal codes. When composition of text is know beforehand, the other hcodes in this section can be used to achieve more compression.
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesDefault { get; } = new byte[] { 0x00, 0x40, 0x80, 0xC0, 0xE0 };
    /// <summary>
    /// Length of each default hcode
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensDefault { get; } = new byte[] { 2, 2, 2, 3, 3 };

    /// <summary>
    /// Horizontal codes preset for English Alphabet content only
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesAlphaOnly { get; } = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
    /// <summary>
    /// Length of each Alpha only hcode
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensAlphaOnly { get; } = new byte[] { 0, 0, 0, 0, 0 };

    /// <summary>
    /// Horizontal codes preset for Alpha Numeric content only
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesAlphaNumOnly { get; } = new byte[] { 0x00, 0x00, 0x80, 0x00, 0x00 };
    /// <summary>
    /// Length of each Alpha numeric hcode
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensAlphaNumOnly { get; } = new byte[] { 1, 0, 1, 0, 0 };

    /// <summary>
    /// Horizontal codes preset for Alpha Numeric and Symbol content only
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesAlphaNumSymOnly { get; } = new byte[] { 0x00, 0x80, 0xC0, 0x00, 0x00 };
    /// <summary>
    /// Length of each Alpha numeric and symbol hcodes
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensAlphaNumSymOnly { get; } = new byte[] { 1, 2, 2, 0, 0 };

    /// <summary>
    /// Horizontal codes preset favouring Alphabet content
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesFavorAlpha { get; } = new byte[] { 0x00, 0x80, 0xA0, 0xC0, 0xE0 };
    /// <summary>
    /// Length of each hcode favouring Alpha content
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensFavorAlpha { get; } = new byte[] { 1, 3, 3, 3, 3 };

    /// <summary>
    /// Horizontal codes preset favouring repeating sequences
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesFavorDict { get; } = new byte[] { 0x00, 0x40, 0xC0, 0x80, 0xE0 };
    /// <summary>
    /// Length of each hcode favouring repeating sequences
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensFavorDict { get; } = new byte[] { 2, 2, 3, 2, 3 };

    /// <summary>
    /// Horizontal codes preset favouring symbols
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesFavorSym { get; } = new byte[] { 0x80, 0x00, 0xA0, 0xC0, 0xE0 };
    /// <summary>
    /// Length of each hcode favouring symbols
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensFavorSym { get; } = new byte[] { 3, 1, 3, 3, 3 };

    /// <summary>
    /// Horizontal codes preset favouring umlaut letters
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesFavorUmlaut { get; } = new byte[] { 0x80, 0xA0, 0xC0, 0xE0, 0x00 };
    /// <summary>
    /// Length of each hcode favouring umlaut letters
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensFavorUmlaut { get; } = new byte[] { 3, 3, 3, 3, 1 };

    /// <summary>
    /// Horizontal codes preset for no repeating sequences
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesNoDict { get; } = new byte[] { 0x00, 0x40, 0x80, 0x00, 0xC0 };
    /// <summary>
    /// Length of each hcode for no repeating sequences
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensNoDict { get; } = new byte[] { 2, 2, 2, 0, 2 };

    /// <summary>
    /// Horizontal codes preset for no Unicode characters
    /// </summary>
    public static ReadOnlyMemory<byte> HCodesNoUni { get; } = new byte[] { 0x00, 0x40, 0x80, 0xC0, 0x00 };
    /// <summary>
    /// Length of each hcode for no Unicode characters
    /// </summary>
    public static ReadOnlyMemory<byte> HCodeLensNoUni { get; } = new byte[] { 2, 2, 2, 2, 0 };

    /// <summary>
    /// Default frequently occuring sequences. When composition of text is know beforehand, the other sequences in this section can be used to achieve more compression.
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeqDefault { get; } = new ReadOnlyMemory<byte>[] { "\": \""u8.ToArray(), "\": "u8.ToArray(), "</"u8.ToArray(), "=\""u8.ToArray(), "\":\""u8.ToArray(), "://"u8.ToArray() };
    /// <summary>
    /// Frequently occuring sequences in text content
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeqText { get; } = new ReadOnlyMemory<byte>[] { " the "u8.ToArray(), " and "u8.ToArray(), "tion"u8.ToArray(), " with"u8.ToArray(), "ing"u8.ToArray(), "ment"u8.ToArray() };
    /// <summary>
    /// Frequently occuring sequences in URL content
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeqUrl { get; } = new ReadOnlyMemory<byte>[] { "https://"u8.ToArray(), "www."u8.ToArray(), ".com"u8.ToArray(), "http://"u8.ToArray(), ".org"u8.ToArray(), ".net"u8.ToArray() };
    /// <summary>
    /// Frequently occuring sequences in JSON content
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeqJson { get; } = new ReadOnlyMemory<byte>[] { "\": \""u8.ToArray(), "\": "u8.ToArray(), "\","u8.ToArray(), "}}}"u8.ToArray(), "\":\""u8.ToArray(), "}}"u8.ToArray() };
    /// <summary>
    /// Frequently occuring sequences in HTML content
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeqHtml { get; } = new ReadOnlyMemory<byte>[] { "</"u8.ToArray(), "=\""u8.ToArray(), "div"u8.ToArray(), "href"u8.ToArray(), "class"u8.ToArray(), "<p>"u8.ToArray() };
    /// <summary>
    /// Frequently occuring sequences in XML content
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeqXml { get; } = new ReadOnlyMemory<byte>[] { "</"u8.ToArray(), "=\""u8.ToArray(), "\">"u8.ToArray(), "<?xml version=\"1.0\""u8.ToArray(), "xmlns:"u8.ToArray(), "://"u8.ToArray() };
    /// <summary>
    /// Commonly occuring templates (ISO Date/Time, ISO Date, US Phone number, ISO Time)
    /// </summary>
    static ReadOnlyMemory<ReadOnlyMemory<byte>> Templates { get; } = new ReadOnlyMemory<byte>[] { "tfff-of-tfTtf:rf:rf.fffZ"u8.ToArray(), "tfff-of-tf"u8.ToArray(), "(fff) fff-ffff"u8.ToArray(), "tf:rf:rf"u8.ToArray() };

    /// <summary>
    /// Default preset parameter set. When composition of text is know beforehand, the other parameter sets in this section can be used to achieve more compression.
    /// </summary>
    public static UnishoxPreset Default => new()
    {
        HCodes = HCodesDefault,
        HCodeLens = HCodeLensDefault,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for English Alphabet only content
    /// </summary>
    public static UnishoxPreset AlphaOnly => new()
    {
        HCodes = HCodesAlphaOnly,
        HCodeLens = HCodeLensAlphaOnly,
        FreqSeq = FreqSeqText,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for Alpha numeric content
    /// </summary>
    public static UnishoxPreset AlphaNumOnly => new()
    {
        HCodes = HCodesAlphaNumOnly,
        HCodeLens = HCodeLensAlphaNumOnly,
        FreqSeq = FreqSeqText,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for Alpha numeric and symbol content
    /// </summary>
    public static UnishoxPreset AlphaNumSymOnly => new()
    {
        HCodes = HCodesAlphaNumSymOnly,
        HCodeLens = HCodeLensAlphaNumSymOnly,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for Alpha numeric symbol content having predominantly text
    /// </summary>
    public static UnishoxPreset AlphaNumSymOnlyText => new()
    {
        HCodes = HCodesAlphaNumSymOnly,
        HCodeLens = HCodeLensAlphaNumSymOnly,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring Alphabet content
    /// </summary>
    public static UnishoxPreset FavorAlpha => new()
    {
        HCodes = HCodesFavorAlpha,
        HCodeLens = HCodeLensFavorAlpha,
        FreqSeq = FreqSeqText,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring repeating sequences
    /// </summary>
    public static UnishoxPreset FavorDict => new()
    {
        HCodes = HCodesFavorDict,
        HCodeLens = HCodeLensFavorDict,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring symbols
    /// </summary>
    public static UnishoxPreset FavorSym => new()
    {
        HCodes = HCodesFavorSym,
        HCodeLens = HCodeLensFavorSym,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring umlaut letters
    /// </summary>
    public static UnishoxPreset FavorUmlaut => new()
    {
        HCodes = HCodesFavorUmlaut,
        HCodeLens = HCodeLensFavorUmlaut,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for when there are no repeating sequences
    /// </summary>
    public static UnishoxPreset NoDict => new()
    {
        HCodes = HCodesNoDict,
        HCodeLens = HCodeLensNoDict,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for when there are no unicode symbols
    /// </summary>
    public static UnishoxPreset NoUni => new()
    {
        HCodes = HCodesNoUni,
        HCodeLens = HCodeLensNoUni,
        FreqSeq = FreqSeqDefault,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set for when there are no unicode symbols favouring text
    /// </summary>
    public static UnishoxPreset NoUniFavorText => new()
    {
        HCodes = HCodesNoUni,
        HCodeLens = HCodeLensNoUni,
        FreqSeq = FreqSeqText,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring URL content
    /// </summary>
    public static UnishoxPreset Url => new()
    {
        HCodes = HCodesDefault,
        HCodeLens = HCodeLensDefault,
        FreqSeq = FreqSeqUrl,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring JSON content
    /// </summary>
    public static UnishoxPreset Json => new()
    {
        HCodes = HCodesDefault,
        HCodeLens = HCodeLensDefault,
        FreqSeq = FreqSeqJson,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring JSON content having no Unicode symbols
    /// </summary>
    public static UnishoxPreset JsonNoUni => new()
    {
        HCodes = HCodesNoUni,
        HCodeLens = HCodeLensNoUni,
        FreqSeq = FreqSeqJson,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring XML content
    /// </summary>
    public static UnishoxPreset Xml => new()
    {
        HCodes = HCodesDefault,
        HCodeLens = HCodeLensDefault,
        FreqSeq = FreqSeqXml,
        Templates = Templates
    };
    /// <summary>
    /// Preset parameter set favouring HTML content
    /// </summary>
    public static UnishoxPreset Html => new()
    {
        HCodes = HCodesDefault,
        HCodeLens = HCodeLensDefault,
        FreqSeq = FreqSeqHtml,
        Templates = Templates
    };
}
