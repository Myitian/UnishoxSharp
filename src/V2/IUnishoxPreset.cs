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
            _ = preset.HCodesSpan[4];
            _ = preset.HCodeLensSpan[4];
            if (preset.HasFreqSeq)
            {
                preset.GetFreqSeqSpan(4);

            }
            if (preset.HasTemplates)
            {
                preset.GetFreqSeqSpan(5);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
