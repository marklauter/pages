namespace Pages
{
    public interface IPage
        : IDisposable
    {
        short AvailableBytes { get; }
        int Id { get; }
        short RecordCount { get; }

        IPage Clone();
        void Delete(int slotIndex);
        byte[] Read(int slotIndex);
        RowId Write(byte[] record);
    }
}
