namespace UnishoxSharp.Common;

public interface IUnishoxTextOutput
{
    void RepeatLast(int count);
    void CopyFrom(int offset, int count);
    void WriteByte(byte value);
    void Write(ReadOnlySpan<byte> buffer);
}
