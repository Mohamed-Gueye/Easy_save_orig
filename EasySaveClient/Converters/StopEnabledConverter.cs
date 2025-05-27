using System;
using System.Globalization;
using System.Windows.Data;
using Easy_Save.Model.Enum;

namespace EasySaveClient.Converters
{
    public class StopEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is BackupJobState state && state != BackupJobState.STOPPED;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
