using System;
using Windows.UI.Xaml.Data;
using FlickrToCloud.Contracts.Exceptions;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Converters
{
    public class UploadStatusToImageFileConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            FileState status = (FileState) value;
            switch (status)
            {
                case FileState.Finished: 
                    return "/Assets/UploadStatusFinished.png";
                case FileState.Failed:
                    return "/Assets/UploadStatusFailed.png";
                case FileState.InProgress:
                    return "/Assets/UploadStatusInProgress.png";
                default:
                    throw new CloudCopyException("Unknown upload status");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
