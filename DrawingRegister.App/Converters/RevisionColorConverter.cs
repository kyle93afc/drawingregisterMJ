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
            var revision = value as string;
            var mode = (parameter as string)?.ToLower() ?? "bg";

            var prefix = !string.IsNullOrEmpty(revision)
                ? char.ToUpper(revision[0])
                : '\0';

            return (prefix, mode) switch
            {
                ('S', "bg") => BrushFromHex("#DBEAFE"),
                ('S', "fg") => BrushFromHex("#1D4ED8"),
                ('P', "bg") => BrushFromHex("#FEE2E2"),
                ('P', "fg") => BrushFromHex("#DC2626"),
                ('T', "bg") => BrushFromHex("#F3E8FF"),
                ('T', "fg") => BrushFromHex("#7C3AED"),
                ('C', "bg") => BrushFromHex("#FEF3C7"),
                ('C', "fg") => BrushFromHex("#D97706"),
                (_, "bg")   => BrushFromHex("#FEF2F4"),
                _           => BrushFromHex("#eb1845"),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();

        public static SolidColorBrush BrushFromHex(string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static SolidColorBrush GetForegroundBrush(string revision)
        {
            if (string.IsNullOrEmpty(revision))
                return BrushFromHex("#eb1845");

            return char.ToUpper(revision[0]) switch
            {
                'S' => BrushFromHex("#1D4ED8"),
                'P' => BrushFromHex("#DC2626"),
                'T' => BrushFromHex("#7C3AED"),
                'C' => BrushFromHex("#D97706"),
                _   => BrushFromHex("#eb1845"),
            };
        }
    }
}
