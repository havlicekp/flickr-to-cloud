using FlickrToOneDrive.Contracts.Models;
using System;
using Windows.UI.Xaml.Data;

namespace FlickrToOneDrive.Converters
{
    public class SessionModeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            var actualMode = (SessionMode)value;
            var requestedMode = Enum.Parse<SessionMode>((string)parameter);
            return actualMode == requestedMode;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
