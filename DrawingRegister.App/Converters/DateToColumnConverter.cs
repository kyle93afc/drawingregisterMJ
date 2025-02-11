using System;
using System.Globalization;
using System.Windows.Data;

namespace DrawingRegister.App.Converters
{
    public class DateToColumnConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                // Get all dates from the MainWindow
                var mainWindow = App.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var allDates = mainWindow.GetAllIssueDates();
                    return allDates.IndexOf(date);
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 