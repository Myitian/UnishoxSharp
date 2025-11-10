# UnishoxSharp

C# port of Unishox, work in progress.

Currently, both Unishox 1 and Unishox 2 are available.

Original Unishox: https://github.com/siara-cc/Unishox2

> In general compression utilities such as `zip`, `gzip` do not compress short strings well and often expand them. They also use lots of memory which makes them unusable in constrained environments like Arduino. So Unishox algorithm was developed for individually compressing (and decompressing) short strings.
>
> Note: Unishox provides the best compression for short text and not to be compared with general purpose compression algorithm like lz4, snappy, lzma, brottli and zstd.

> [!IMPORTANT]
>
> The current implementation of Unishox 1 in this project may have a defect. In rare cases, compression/decompression may fail or decode incorrect content. Currently, the bug appears to only occur when a non-null `prev_lines` is provided. Although it passes many test cases when `prev_lines` is null, using Unishox 1 in this library is not recommended for production environments. The issue may be fixed in the future.
>
> No issues have been found in the current implementation of Unishox 2 in this project.
