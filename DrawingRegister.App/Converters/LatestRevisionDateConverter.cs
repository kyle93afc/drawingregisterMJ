using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Converters
{
    public class LatestRevisionDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Dictionary<DateTime, RevisionInfo> revisionHistory && revisionHistory.Any())
            {
                var latest = revisionHistory
                    .Where(kvp => !kvp.Value.IsSuperseded)
                    .OrderByDescending(kvp => kvp.Key)
                    .FirstOrDefault();
                if (latest.Value == null)
                {
                    return "-";
                }
                return latest.Key.ToString("yyyy-MM-dd");
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 