using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FSARandomizer.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is Visibility.Visible;
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is not Visibility.Visible;
    }

    /// <summary>Converts a bool IsDirty to a red/transparent brush for tree item backgrounds.</summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class DirtyToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true
               ? new SolidColorBrush(Color.FromArgb(60, 249, 226, 175))
               : Brushes.Transparent;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    /// <summary>Returns a green brush when item has been randomized (IsChanged=true).</summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class ChangedToForegroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush _green = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
        private static readonly SolidColorBrush _normal = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4));

        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? _green : _normal;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    /// <summary>Formats a byte as hex string for display.</summary>
    [ValueConversion(typeof(byte), typeof(string))]
    public class ByteToHexConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is byte b ? $"0x{b:X2}" : "0x00";
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
        {
            if (value is string s)
            {
                s = s.Replace("0x", "").Replace("0X", "").Trim();
                if (byte.TryParse(s, NumberStyles.HexNumber, null, out byte result)) return result;
            }
            return (byte)0;
        }
    }

    /// <summary>Null-check → Visibility.</summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value != null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    /// <summary>Null → Visible, non-null → Collapsed (inverse of NullToVisibilityConverter).</summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value == null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }
}
