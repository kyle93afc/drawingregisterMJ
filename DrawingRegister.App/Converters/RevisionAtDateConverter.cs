using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Converters
{
    public class RevisionAtDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Dictionary<DateTime, RevisionInfo> revisionHistory &&
                parameter is DateTime date &&
                revisionHistory.TryGetValue(date, out var revInfo))
            {
                return revInfo.IsSuperseded ? $"{revInfo.Revision} (S)" : revInfo.Revision;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 