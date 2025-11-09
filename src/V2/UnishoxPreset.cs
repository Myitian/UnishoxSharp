namespace UnishoxSharp.V2;

public struct UnishoxPreset : IUnishoxPreset
{
    public ReadOnlyMemory<byte> HCodes { readonly get; set; }
    public ReadOnlyMemory<byte> HCodeLens { readonly get; set; }
    public ReadOnlyMemory<ReadOnlyMemory<byte>> FreqSeq { readonly get; set; }
    public ReadOnlyMemory<ReadOnlyMemory<byte>> Templates { readonly get; set; }
    public readonly ReadOnlySpan<byte> HCodesSpan => HCodes.Span;
    public readonly ReadOnlySpan<byte> HCodeLensSpan => HCodeLens.Span;
    public readonly int FreqSeqSpanCount => FreqSeq.Length;
    public readonly ReadOnlySpan<byte> GetFreqSeqSpan(int index) => FreqSeq.Span[index].Span;
    public readonly int TemplatesSpanCount => Templates.Length;
    public readonly ReadOnlySpan<byte> GetTemplatesSpan(int index) => Templates.Span[index].Span;
}
