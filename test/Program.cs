using System.Text;
using UnishoxSharp.V1;

byte[] data = new byte[4096];
byte[] data2 = new byte[4096];
while (Console.ReadLine() is string line)
{
    byte[] src = Encoding.UTF8.GetBytes(line);
    int len = Unishox.Compress(src, data, null);
    int len2 = Unishox.Decompress(data.AsSpan(0, len), data2, null);
    Console.WriteLine($"L1:{len}, L2:{len2}, EQ:{data2.AsSpan(0, len2).SequenceEqual(src)}");
}