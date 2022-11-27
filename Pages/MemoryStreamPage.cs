namespace Pages
{
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
            get => this.id;
            private set
            {
                this.id = value;
                this.data.Position = 0;
                this.writer.Write(this.id);
            }
        }

        private short recordCount;
        public short RecordCount
        {
            get => this.recordCount;
            private set
            {
                this.recordCount = value;
                this.data.Position = sizeof(short);
                this.writer.Write(this.recordCount);
            }
        }

        private short availableBytes;
        public short AvailableBytes
        {
            get => this.availableBytes;
            private set
            {
                this.availableBytes = value;
                this.data.Position = sizeof(short) * 2;
                this.writer.Write(this.availableBytes);
            }
        }

        public static MemoryStreamPage FromStream(Stream stream)
        {
            return new MemoryStreamPage(stream);
        }

        public static MemoryStreamPage New(int id)
        {
            return new MemoryStreamPage(id);
        }

        public IPage Clone()
        {
            return new MemoryStreamPage(this.data);
        }

        private MemoryStreamPage()
        {
            this.data = new(PageSize);
            this.reader = new BinaryReader(this.data, System.Text.Encoding.UTF8, true);
            this.writer = new BinaryWriter(this.data, System.Text.Encoding.UTF8, true);
            this.availableBytes = PageSize - HeaderSize;
            this.recordCount = 0;
        }

        private MemoryStreamPage(Stream stream)
            : this()
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.Length != PageSize)
            {
                throw new ArgumentException("page size mismatch");
            }

            stream.Position = 0;
            stream.CopyTo(this.data);
            this.data.Position = 0;
            this.id = this.reader.ReadInt32();
            this.recordCount = this.reader.ReadInt16();
            this.availableBytes = this.reader.ReadInt16();
        }

        private MemoryStreamPage(int id)
            : this()
        {
            this.Id = id;
        }

        public byte[] Read(int slotIndex)
        {
            this.data.Position = this.CalculateDirectoryOffset(slotIndex);
            var slot = this.ReadDirectoryEntry(slotIndex);
            return this.ReadData(slot);
        }

        private DirectoryEntry ReadDirectoryEntry(int slotIndex)
        {
            this.data.Position = this.CalculateDirectoryOffset(slotIndex);
            var offset = this.reader.ReadInt16();
            var length = this.reader.ReadInt16();
            return new DirectoryEntry(offset, length);
        }

        private byte[] ReadData(DirectoryEntry slot)
        {
            if (slot.Offset == DeletedOffset)
            {
                return Array.Empty<byte>();
            }

            this.data.Position = slot.Offset;
            return this.reader.ReadBytes(slot.Length);
        }

        // see https://www.youtube.com/watch?v=TeWuLyHYsTQ&list=PLC4UZxBVGKtf2MR6IXMU79HMOtHIdnIEF&index=4
        public RowId Write(byte[] record)
        {
            if (record is null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (record.Length + DirectorySlotSize > this.availableBytes)
            {
                throw new BufferTooLargeException($"Page Id {this.id} can't fit {nameof(record)} with length {record.Length}. Space available {this.availableBytes}.");
            }

            var slotIndex = this.recordCount;
            var previousDirectoryEntry = (slotIndex > 0)
                ? this.ReadDirectoryEntry(slotIndex - 1)
                : new DirectoryEntry(HeaderSize, 0);

            var newDirectoryEntry = new DirectoryEntry(
                this.CalculateNextDataOffset(previousDirectoryEntry),
                (short)record.Length);
            this.WriteDirectoryEntry(slotIndex, newDirectoryEntry);
            this.WriteData(record, newDirectoryEntry);

            this.AvailableBytes = (short)(this.availableBytes - (record.Length + DirectorySlotSize));
            this.RecordCount += 1;

            return new RowId(this.Id, slotIndex);
        }

        private void WriteDirectoryEntry(int slotIndex, DirectoryEntry directoryEntry)
        {
            this.data.Position = this.CalculateDirectoryOffset(slotIndex);
            this.writer.Write(directoryEntry.Offset);
            this.writer.Write(directoryEntry.Length);
        }

        private void WriteData(byte[] record, DirectoryEntry directoryEntry)
        {
            this.data.Position = directoryEntry.Offset;
            this.writer.Write(record);
        }

        public void Delete(int slotIndex)
        {
            this.data.Position = this.CalculateDirectoryOffset(slotIndex);
            this.writer.Write(DeletedOffset);
        }

        private int CalculateDirectoryOffset(int slotIndex)
        {
            // offset from end / tail of page
            return PageSize - DirectorySlotSize * (slotIndex + 1);
        }

        private short CalculateNextDataOffset(DirectoryEntry directoryEntry)
        {
            return (short)(directoryEntry.Offset + directoryEntry.Length);
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.reader.Dispose();
                    this.writer.Dispose();
                    this.data.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
        }
    }
}
