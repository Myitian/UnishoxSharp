namespace UnishoxSharp.Common;

public class UnishoxLinkList
{
    public ReadOnlyMemory<byte> Data { get; set; }
    public UnishoxLinkList? Previous { get; set; }
};