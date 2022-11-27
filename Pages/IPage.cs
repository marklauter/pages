namespace Pages
{
    public interface IPage
        : IDisposable
    {
        short AvailableSpace { get; }
        int Id { get; }
        short RecordCount { get; }

        IPage Clone();
        void WriteTo(Stream stream, long pageOffset);
        void Delete(int slotIndex);
        byte[] Read(int slotIndex);
        RowId Write(byte[] record);
    }
}
