namespace Pages
{
    public class RowId
        : IEquatable<RowId?>
    {
        public RowId(int pageId, int slotId)
        {
            this.PageId = pageId;
            this.SlotId = slotId;
        }

        public int PageId { get; }
        public int SlotId { get; }

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as RowId);
        }

        public bool Equals(RowId? other)
        {
            return other is not null &&
                   this.PageId.Equals(other.PageId) &&
                   this.SlotId == other.SlotId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.PageId, this.SlotId);
        }

        public override string? ToString()
        {
            return $"{this.PageId}/{this.SlotId}";
        }

        public static bool operator ==(RowId? left, RowId? right)
        {
            return EqualityComparer<RowId>.Default.Equals(left, right);
        }

        public static bool operator !=(RowId? left, RowId? right)
        {
            return !(left == right);
        }
    }

    // https://www.youtube.com/watch?v=7OG-bb7iBgI&list=PLC4UZxBVGKtf2MR6IXMU79HMOtHIdnIEF&index=3
    public sealed class SlottedPage
        : IDisposable
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

        public static SlottedPage FromStream(Stream stream)
        {
            return new SlottedPage(stream);
        }

        public static SlottedPage New(int id)
        {
            return new SlottedPage(id);
        }

        public SlottedPage Clone()
        {
            return new SlottedPage(this);
        }

        private SlottedPage()
        {
            this.data = new(PageSize);
            this.reader = new BinaryReader(this.data, System.Text.Encoding.UTF8, true);
            this.writer = new BinaryWriter(this.data, System.Text.Encoding.UTF8, true);
            this.availableBytes = PageSize - HeaderSize;
            this.recordCount = 0;
        }

        private SlottedPage(SlottedPage sourcePage)
            : this(sourcePage?.data ?? throw new ArgumentNullException(nameof(sourcePage)))
        {
        }

        private SlottedPage(Stream stream)
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

        private SlottedPage(int id)
            : this()
        {
            this.Id = id;
        }

        public byte[] Read(int slotIndex)
        {
            this.data.Position = this.DirectoryOffset(slotIndex);
            var offset = this.reader.ReadInt16();
            return offset != DeletedOffset
                ? this.ReadInternal(offset)
                : Array.Empty<byte>();
        }

        private byte[] ReadInternal(int offset)
        {
            var dataLength = this.reader.ReadInt16();
            this.data.Position = offset;
            return this.reader.ReadBytes(dataLength);
        }

        // see https://www.youtube.com/watch?v=TeWuLyHYsTQ&list=PLC4UZxBVGKtf2MR6IXMU79HMOtHIdnIEF&index=4
        public RowId Write(byte[] buffer)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.Length + DirectorySlotSize > this.availableBytes)
            {
                throw new BufferTooLargeException($"Page Id {this.id} is out of space.");
            }

            var slotIndex = this.recordCount;
            var previousDataOffset = (short)HeaderSize;
            var previousDataLength = (short)0;
            if (slotIndex > 0)
            {
                this.data.Position = this.DirectoryOffset(slotIndex - 1);
                previousDataOffset = this.reader.ReadInt16();
                previousDataLength = this.reader.ReadInt16();
            }

            var nextOffset = (short)(previousDataOffset + previousDataLength);
            this.data.Position = this.DirectoryOffset(slotIndex);
            this.writer.Write(nextOffset);
            this.writer.Write((short)buffer.Length);

            this.data.Position = nextOffset;
            this.writer.Write(buffer);

            this.AvailableBytes = (short)(this.availableBytes - (buffer.Length + DirectorySlotSize));
            this.RecordCount += 1;

            return new RowId(this.Id, slotIndex);
        }

        public void Delete(int slotIndex)
        {
            this.data.Position = this.DirectoryOffset(slotIndex);
            this.writer.Write(DeletedOffset);
        }

        private int DirectoryOffset(int slotIndex)
        {
            return PageSize - DirectorySlotSize * (slotIndex + 1);
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
