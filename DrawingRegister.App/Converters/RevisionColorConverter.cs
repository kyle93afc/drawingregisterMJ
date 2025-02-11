using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DrawingRegister.App.Converters
{
    public class RevisionColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string)?.ToUpper() switch
            {
                "DRAFT" => System.Windows.Media.Brushes.LightBlue,
                "APPROVAL" => System.Windows.Media.Brushes.LightGreen,
                "CONSTRUCTION" => System.Windows.Media.Brushes.Gold,
                "TENDER" => System.Windows.Media.Brushes.Violet,
                _ => System.Windows.Media.Brushes.WhiteSmoke
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 
            throw new NotImplementedException();
    }
} 