using System;
using System.Runtime.Serialization;

namespace FlickrToOneDrive.Contracts.Exceptions
{
    public class CloudCopyException : ApplicationException
    {
        public CloudCopyException()
        {
        }

        protected CloudCopyException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public CloudCopyException(string message) : base(message)
        {
        }

        public CloudCopyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
