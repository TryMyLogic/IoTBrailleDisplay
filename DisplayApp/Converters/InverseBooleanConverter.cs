namespace DisplayApp.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse.
    /// </summary>
    /// <remarks>
    /// This converter is useful for XAML bindings where you need to bind a property
    /// to a control's behavior or visibility in an inverted manner (e.g., !IsEnabled).
    /// It implements <see cref="IValueConverter"/> for two-way binding support,
    /// but the ConvertBack method is not implemented as reverse conversion is not required.
    /// </remarks>
    internal class InverseBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to its inverse.
        /// </summary>
        /// <param name="value">The value produced by the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter to influence conversion (not used).</param>
        /// <param name="culture">The culture to use in the converter (not used).</param>
        /// <returns>The inverted boolean value if <paramref name="value"/> is a boolean; otherwise a safe fallback.</returns>
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolean)
            {
                return !boolean;
            }

            // If the value is not a boolean but the target expects a boolean, return false as fallback
            if (targetType == typeof(bool))
            {
                return false; // fallback
            }

            // Return the original value or false if null
            return value ?? false!;
        }

        /// <summary>
        /// ConvertBack is not implemented because this converter is intended for one-way bindings.
        /// </summary>
        /// <param name="value">The value produced by the binding target (ignored).</param>
        /// <param name="targetType">The type to convert to (ignored).</param>
        /// <param name="parameter">Optional parameter (ignored).</param>
        /// <param name="culture">The culture to use (ignored).</param>
        /// <returns>Throws <see cref="NotImplementedException"/>.</returns>
        /// <exception cref="NotImplementedException">Always thrown because reverse conversion is not supported.</exception>
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
