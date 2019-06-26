using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Data;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Converters
{
    public class SessionFilesOriginToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var names = new List<string>(2);
            var filesOrigin = (SessionFilesOrigin)value;
            if (filesOrigin.HasFlag(SessionFilesOrigin.Structured))
                names.Add("Albums");
            if (filesOrigin.HasFlag(SessionFilesOrigin.Flat))
                names.Add("Stream");
            return String.Join(" and ", names);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
