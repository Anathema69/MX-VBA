// Crear archivo: Services/PercentageConverter.cs

using System;
using System.Globalization;
using System.Windows.Data;

namespace SistemaGestionProyectos2.Services
{
    public class PercentageConverter : IValueConverter
    {
        private static PercentageConverter _instance;
        public static PercentageConverter Instance => _instance ?? (_instance = new PercentageConverter());

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                return percentage / 100.0;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}