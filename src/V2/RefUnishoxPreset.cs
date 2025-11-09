namespace UnishoxSharp.V2;

public ref struct RefUnishoxPreset : IUnishoxPreset
{
    public ReadOnlySpan<byte> HCodes { readonly get; set; }
    public ReadOnlySpan<byte> HCodeLens { readonly get; set; }
    public ReadOnlySpan<ReadOnlyMemory<byte>> FreqSeq { readonly get; set; }
    public ReadOnlySpan<ReadOnlyMemory<byte>> Templates { readonly get; set; }
    public readonly ReadOnlySpan<byte> HCodesSpan => HCodes;
    public readonly ReadOnlySpan<byte> HCodeLensSpan => HCodeLens;
    public readonly bool HasFreqSeq => !FreqSeq.IsEmpty;
    public readonly ReadOnlySpan<byte> GetFreqSeqSpan(int index) => FreqSeq[index].Span;
    public readonly bool HasTemplates => !Templates.IsEmpty;
    public readonly ReadOnlySpan<byte> GetTemplatesSpan(int index) => Templates[index].Span;
}
