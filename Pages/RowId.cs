namespace Pages;

public sealed class RowId(int pageId, int slotId)
            : IEquatable<RowId?>
{
    public int PageId { get; } = pageId;
    public int SlotId { get; } = slotId;

    public override bool Equals(object? obj) => Equals(obj as RowId);

    public bool Equals(RowId? other) => other is not null &&
               PageId.Equals(other.PageId) &&
               SlotId == other.SlotId;

    public override int GetHashCode() => HashCode.Combine(PageId, SlotId);

    public override string? ToString() => $"{PageId}/{SlotId}";

    public static bool operator ==(RowId? left, RowId? right) => EqualityComparer<RowId>.Default.Equals(left, right);

    public static bool operator !=(RowId? left, RowId? right) => !(left == right);
}
