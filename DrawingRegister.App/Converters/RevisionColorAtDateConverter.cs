using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Converters
{
    public class RevisionColorAtDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Dictionary<DateTime, RevisionInfo> revisionHistory &&
                parameter is DateTime date &&
                revisionHistory.TryGetValue(date, out var revInfo))
            {
                return RevisionColorConverter.GetForegroundBrush(revInfo.Revision);
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
