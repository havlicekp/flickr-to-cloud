using System;
using System.Runtime.Serialization;
using Serilog;

namespace FlickrToCloud.Contracts.Exceptions
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

        public CloudCopyException(string message, ILogger log) : base(message)
        {
            log.Error(message);
        }

        public CloudCopyException(string message, Exception innerException, ILogger log) : base(message, innerException)
        {
            log.Error(innerException, message);
        }

    }
}
