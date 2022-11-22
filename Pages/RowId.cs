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
}
