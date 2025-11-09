namespace UnishoxSharp.Common;

public interface IUnishoxDataInput
{
    bool CanRead(int count = 1);
    int ReadBit(bool autoForward);
    int Read8Bit(bool autoForward, int offset);
    void Skip(int count);
}
