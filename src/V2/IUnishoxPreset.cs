namespace UnishoxSharp.V2;

public interface IUnishoxPreset
{
    ReadOnlySpan<byte> HCodesSpan { get; }
    ReadOnlySpan<byte> HCodeLensSpan { get; }
    bool HasFreqSeq { get; }
    ReadOnlySpan<byte> GetFreqSeqSpan(int index);
    bool HasTemplates { get; }
    ReadOnlySpan<byte> GetTemplatesSpan(int index);

    public static bool Validate<T>(in T preset) where T : IUnishoxPreset
    {
        try
        {
            if (preset.HasFreqSeq)
                preset.GetFreqSeqSpan(4);
            if (preset.HasTemplates)
                preset.GetFreqSeqSpan(5);
            return preset.HCodesSpan.Length >= 5 && preset.HCodeLensSpan.Length >= 5;
        }
        catch
        {
            return false;
        }
    }
}
