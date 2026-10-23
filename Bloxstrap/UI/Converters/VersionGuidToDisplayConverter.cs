using System;
using System.Globalization;
using System.Windows.Data;

namespace Bloxstrap.UI.Converters
{
    /// <summary>
    /// Displays a pinned Roblox version guid, falling back to "Latest (current)"
    /// when nothing is pinned (empty string).
    /// </summary>
    public class VersionGuidToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string guid = value as string ?? string.Empty;

            if (string.IsNullOrWhiteSpace(guid))
                return "Latest (current)";

            return guid;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
