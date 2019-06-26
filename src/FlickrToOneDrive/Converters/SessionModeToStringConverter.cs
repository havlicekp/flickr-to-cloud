using System;
using Windows.UI.Xaml.Data;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Converters
{
    public class SessionModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var mode = (SessionMode?) value;
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
