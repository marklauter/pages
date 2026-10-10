namespace Pages;

// todo: thinks like record count and available bytes should be held in tables external to the slotted page - this means slotted page isn't responsible for checking size of records being written either
// see this vid for info on maintaining tables with available space: https://www.youtube.com/watch?v=Pt_-GT_6ESc&list=PLC4UZxBVGKtf2MR6IXMU79HMOtHIdnIEF&index=5&t=7s

// https://www.youtube.com/watch?v=7OG-bb7iBgI&list=PLC4UZxBVGKtf2MR6IXMU79HMOtHIdnIEF&index=3
public sealed class MemoryStreamPage
    : IPage
{
    public const int DirectorySlotSize = sizeof(short) * 2; // two int16 and 1 byte denoting forward reference
    public const int HeaderSize = sizeof(short) * 2 + sizeof(int);
    public const int PageSize = 1024 * 4;

    private readonly MemoryStream data;
    private readonly BinaryReader reader;
    private readonly BinaryWriter writer;
    private bool disposedValue;
    private const short DeletedOffset = -1;

    private int id;
    public int Id
    {
        get => id;
        private set
        {
            id = value;
            _ = data.Seek(0, SeekOrigin.Begin);
            writer.Write(id);
        }
    }

    private short recordCount;
    public short RecordCount
    {
        get => recordCount;
        private set
        {
            recordCount = value;
            _ = data.Seek(sizeof(short), SeekOrigin.Begin);
            writer.Write(recordCount);
        }
    }

    private short bytesAvailable;
    public short AvailableSpace
    {
        get => bytesAvailable;
        private set
        {
            bytesAvailable = value;
            _ = data.Seek(sizeof(short) * 2, SeekOrigin.Begin);
            writer.Write(bytesAvailable);
        }
    }

    /// <summary>
    /// Loads the page from a source stream.
    /// </summary>
    /// <param name="stream">source stream</param>
    /// <param name="pageOffset">location of the page in the source stream</param>
    /// <returns><See ecref="MemoryStreamPage"></returns>
    public static MemoryStreamPage FromStream(Stream stream, long pageOffset) => new(stream, pageOffset);

    public static MemoryStreamPage New(int id) => new(id);

    public IPage Clone() => new MemoryStreamPage(data, 0L);

    private MemoryStreamPage()
    {
        data = new(PageSize);
        reader = new BinaryReader(data, System.Text.Encoding.UTF8, true);
        writer = new BinaryWriter(data, System.Text.Encoding.UTF8, true);
        bytesAvailable = PageSize - HeaderSize;
        recordCount = 0;
    }

    private MemoryStreamPage(Stream stream, long pageOffset)
        : this()
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Length != pageOffset + PageSize)
        {
            throw new ArgumentException("page size mismatch");
        }

        _ = stream.Seek(pageOffset, SeekOrigin.Begin);
        var buffer = new Span<byte>(new byte[PageSize]);
        stream.ReadExactly(buffer);
        _ = data.Seek(0, SeekOrigin.Begin);
        data.Write(buffer);

        id = reader.ReadInt32();
        recordCount = reader.ReadInt16();
        bytesAvailable = reader.ReadInt16();
    }

    private MemoryStreamPage(int id)
        : this() => Id = id;

    public byte[] Read(int slotIndex)
    {
        data.Position = CalculateDirectoryOffset(slotIndex);
        var slot = ReadDirectoryEntry(slotIndex);
        return ReadData(slot);
    }

    private DirectoryEntry ReadDirectoryEntry(int slotIndex)
    {
        data.Position = CalculateDirectoryOffset(slotIndex);
        var offset = reader.ReadInt16();
        var length = reader.ReadInt16();
        return new DirectoryEntry(offset, length);
    }

    private byte[] ReadData(DirectoryEntry slot)
    {
        if (slot.Offset == DeletedOffset)
        {
            return [];
        }

        data.Position = slot.Offset;
        return reader.ReadBytes(slot.Length);
    }

    // see https://www.youtube.com/watch?v=TeWuLyHYsTQ&list=PLC4UZxBVGKtf2MR6IXMU79HMOtHIdnIEF&index=4
    public RowId Write(byte[] record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Length + DirectorySlotSize > bytesAvailable)
        {
            throw new BufferTooLargeException($"Page Id {id} can't fit {nameof(record)} with length {record.Length}. Space available {bytesAvailable}.");
        }

        var slotIndex = recordCount;
        var previousDirectoryEntry = (slotIndex > 0)
            ? ReadDirectoryEntry(slotIndex - 1)
            : new DirectoryEntry(HeaderSize, 0);

        var newDirectoryEntry = new DirectoryEntry(
            CalculateNextDataOffset(previousDirectoryEntry),
            (short)record.Length);
        WriteDirectoryEntry(slotIndex, newDirectoryEntry);
        WriteData(record, newDirectoryEntry);

        AvailableSpace = (short)(bytesAvailable - (record.Length + DirectorySlotSize));
        RecordCount += 1;

        return new RowId(Id, slotIndex);
    }

    private void WriteDirectoryEntry(int slotIndex, DirectoryEntry directoryEntry)
    {
        data.Position = CalculateDirectoryOffset(slotIndex);
        writer.Write(directoryEntry.Offset);
        writer.Write(directoryEntry.Length);
    }

    private void WriteData(byte[] record, DirectoryEntry directoryEntry)
    {
        data.Position = directoryEntry.Offset;
        writer.Write(record);
    }

    public void Delete(int slotIndex)
    {
        data.Position = CalculateDirectoryOffset(slotIndex);
        writer.Write(DeletedOffset);
    }

    private static int CalculateDirectoryOffset(int slotIndex) =>
        // offset from end / tail of page
        PageSize - DirectorySlotSize * (slotIndex + 1);

    private static short CalculateNextDataOffset(DirectoryEntry directoryEntry) => (short)(directoryEntry.Offset + directoryEntry.Length);

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                reader.Dispose();
                writer.Dispose();
                data.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose() => Dispose(disposing: true);

    public void WriteTo(Stream stream, long pageOffset)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var buffer = new Span<byte>(new byte[PageSize]);
        _ = data.Seek(0, SeekOrigin.Begin);
        data.ReadExactly(buffer);

        _ = stream.Seek(pageOffset, SeekOrigin.Begin);
        stream.Write(buffer);

        id = reader.ReadInt32();
        recordCount = reader.ReadInt16();
        bytesAvailable = reader.ReadInt16();
    }
}
