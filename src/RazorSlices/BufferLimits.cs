namespace RazorSlices;

internal class BufferLimits
{
    public const int SmallWriteByteSize = 256;
    public const int SmallWriteCharSize = SmallWriteByteSize / 2;
    public const int MaxBufferSize = 1024;
}
