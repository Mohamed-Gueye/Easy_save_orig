using System;
using System.Globalization;
using System.Windows.Data;
using Easy_Save.Model.Enum;
public class StopEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is BackupJobState state &&
               (state == BackupJobState.RUNNING || state == BackupJobState.PAUSED);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}