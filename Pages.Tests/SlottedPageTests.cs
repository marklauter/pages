namespace Pages.Tests
{
    public class SlottedPageTests
    {
        [Fact]
        public void NewCreatesEmptyPage()
        {
            using var page = SlottedPage.New(1);
            Assert.Equal(1, page.Id);
            Assert.Equal(0, page.RecordCount);
            Assert.Equal(SlottedPage.PageSize - SlottedPage.HeaderSize, page.AvailableBytes);
        }

        [Fact]
        public void WriteIncreasesCount()
        {
            var abc = System.Text.Encoding.UTF8.GetBytes("abc");
            using var page = SlottedPage.New(1);
            Assert.Equal(0, page.RecordCount);

            var rowId = page.Write(abc);
            Assert.Equal(1, rowId.PageId);
            Assert.Equal(0, rowId.SlotId);
            Assert.Equal(1, page.RecordCount);

            rowId = page.Write(abc);
            Assert.Equal(1, rowId.PageId);
            Assert.Equal(1, rowId.SlotId);
            Assert.Equal(2, page.RecordCount);
        }

        [Fact]
        public void WriteDecreasesAvailableBytes()
        {
            var abc = System.Text.Encoding.UTF8.GetBytes("abc");
            using var page = SlottedPage.New(1);
            Assert.Equal(0, page.RecordCount);

            var rowId = page.Write(abc);
            Assert.Equal(SlottedPage.PageSize - (SlottedPage.HeaderSize + abc.Length + SlottedPage.DirectorySlotSize), page.AvailableBytes);

            rowId = page.Write(abc);
            Assert.Equal(SlottedPage.PageSize - (SlottedPage.HeaderSize + 2 * (abc.Length + SlottedPage.DirectorySlotSize)), page.AvailableBytes);
        }

        [Fact]
        public void ReadRetrievesCorrectData()
        {
            var expectedAbc = System.Text.Encoding.UTF8.GetBytes("abc");
            using var page = SlottedPage.New(1);
            Assert.Equal(0, page.RecordCount);

            var rowId = page.Write(expectedAbc);
            var actualAbc = page.Read(rowId.SlotId);
            Assert.Equal<IEnumerable<byte>>(expectedAbc, actualAbc);
        }

        [Fact]
        public void ReadWithWriteMultipleRetrievesCorrectData()
        {
            var expected = new byte[][]
            {
                System.Text.Encoding.UTF8.GetBytes("abc"),
                System.Text.Encoding.UTF8.GetBytes("def"),
                System.Text.Encoding.UTF8.GetBytes("hij"),
            };

            using var page = SlottedPage.New(1);
            Assert.Equal(0, page.RecordCount);

            var rowIds = new List<RowId>();
            foreach (var row in expected)
            {
                rowIds.Add(page.Write(row));
            }

            Assert.Equal(3, page.RecordCount);

            for (var i = 0; i < rowIds.Count; ++i)
            {
                var actual = page.Read(rowIds[i].SlotId);
                Assert.Equal<IEnumerable<byte>>(expected[i], actual);
            }
        }

        [Fact]
        public void WriteMultipleEventuallyFillsPageAndThrowsBufferTooLargeException()
        {
            using var page = SlottedPage.New(1);
            Assert.Equal(0, page.RecordCount);

            var buffer = new byte[1024];
            _ = page.Write(buffer);
            _ = page.Write(buffer);
            _ = page.Write(buffer);
            Assert.Equal(3, page.RecordCount);
            Assert.True(page.AvailableBytes < buffer.Length);

            var exception = Assert.Throws<BufferTooLargeException>(() => _ = page.Write(buffer));
            Assert.Contains($"{page.Id}", exception.Message);
        }
    }
}
