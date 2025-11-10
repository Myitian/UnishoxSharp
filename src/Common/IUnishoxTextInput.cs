namespace UnishoxSharp.Common;

public interface IUnishoxTextInput
{
    int Position { get; set; }
    int Length { get; }
    int ReadByteAt(int position);
}
