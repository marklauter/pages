namespace Pages
{
    internal readonly struct DirectoryEntry
    {
        public DirectoryEntry(short offset, short length)
        {
            this.Offset = offset;
            this.Length = length;
        }

        public readonly short Offset;
        public readonly short Length;
    }
}
