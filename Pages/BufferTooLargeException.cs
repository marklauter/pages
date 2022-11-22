using System.Runtime.Serialization;

namespace Pages
{
    public class BufferTooLargeException
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

        protected BufferTooLargeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
