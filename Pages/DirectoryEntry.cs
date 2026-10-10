namespace Pages;

internal readonly struct DirectoryEntry(short offset, short length)
{
    public short Offset { get; } = offset;
    public short Length { get; } = length;
}
