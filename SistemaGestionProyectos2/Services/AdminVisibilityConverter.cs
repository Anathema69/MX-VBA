using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SistemaGestionProyectos2.Services
{
    public class AdminVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Si el valor es "admin", mostrar el botón
            if (value is string role && role == "admin")
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
