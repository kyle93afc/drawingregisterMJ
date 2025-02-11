using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Converters
{
    public class LatestRevisionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Dictionary<DateTime, RevisionInfo> revisionHistory && revisionHistory.Any())
            {
                var latestRevision = revisionHistory.OrderByDescending(x => x.Key).First();
                return string.IsNullOrWhiteSpace(latestRevision.Value.Revision) ? "-" : latestRevision.Value.Revision;
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 