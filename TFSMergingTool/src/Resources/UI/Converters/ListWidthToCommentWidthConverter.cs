using System;
using System.Globalization;
using System.Windows.Data;

namespace TFSMergingTool.Resources.UI.Converters
{
    public class ListWidthToCommentWidthConverter : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //return ((double)value) * 0.6;
            return 100.0;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
