namespace Pages
{
    public sealed class BufferTooLargeException
        : Exception
    {
        public BufferTooLargeException()
        {
        }

        public BufferTooLargeException(string? message) : base(message)
        {
        }

        public BufferTooLargeException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
