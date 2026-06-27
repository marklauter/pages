namespace Pages
{
    internal readonly struct DirectoryEntry
    {
        public DirectoryEntry(short offset, short length)
        {
            this.Offset = offset;
            this.Length = length;
        }

        public short Offset { get; }
        public short Length { get; }
    }
}
