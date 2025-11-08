namespace UnishoxSharp.V1;

public class UnishoxLinkList
{
    public ReadOnlyMemory<byte> Data { get; set; }
    public UnishoxLinkList? Previous { get; set; }
};