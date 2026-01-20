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
            // Roles v2.0: direccion y administracion tienen permisos de admin
            if (value is string role && (role == "direccion" || role == "administracion"))
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
