using System;
using Serilog;

namespace FlickrToCloud.Contracts.Exceptions
{
    public class ReadingSourceException : CloudCopyException
    {
        public ReadingSourceException(string message, Exception innerException, ILogger log) 
            : base(message, innerException, log)
        {
        }
    }
}
