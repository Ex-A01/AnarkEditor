using System;
using System.Globalization;
using System.Windows.Data;
using EvershadeEditor.LM2;

namespace AnarkBrowser
{
    public class ChunkTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ushort typeId)
            {
                // On cast l'ushort vers l'Enum pour obtenir le nom lisible
                if (Enum.IsDefined(typeof(ChunkType), typeId))
                    return ((ChunkType)typeId).ToString();

                return $"Unknown (0x{typeId:X4})";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}