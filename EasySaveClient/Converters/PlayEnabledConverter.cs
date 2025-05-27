using System;
using System.Globalization;
using System.Windows.Data;
using Easy_Save.Model.Enum;

namespace EasySaveClient.Converters
{
    public class PlayEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is BackupJobState state && state != BackupJobState.RUNNING;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
