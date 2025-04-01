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
                // Get the latest calendar date (ignoring time component)
                var latestDate = revisionHistory.Keys.Max(dt => dt.Date);
                
                // Get all revisions from that calendar date
                var revisionsOnLatestDate = revisionHistory
                    .Where(kvp => kvp.Key.Date == latestDate)
                    .Select(kvp => kvp.Value.Revision)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList();

                if (revisionsOnLatestDate.Any())
                {
                    // Sort revisions alphanumerically in descending order
                    // This ensures I02 comes before I01, C03 before C02, etc.
                    return revisionsOnLatestDate
                        .OrderByDescending(r => r)
                        .First();
                }
                return "-";
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 