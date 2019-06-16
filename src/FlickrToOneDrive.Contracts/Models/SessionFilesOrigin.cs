using System;

namespace FlickrToOneDrive.Contracts.Models
{
    [Flags]
    public enum SessionFilesOrigin
    {
        Flat = 1,
        Structured = 2
    }
}
