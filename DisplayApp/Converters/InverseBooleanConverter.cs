namespace DisplayApp.Converters
{
    internal class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolean)
            {
                return !boolean;
            }
            //return value;

            if (targetType == typeof(bool))
            {
                return false; // fallback
            }

            return value ?? false!;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
