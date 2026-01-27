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

    /// <summary>
    /// Converter para determinar si un valor decimal es negativo
    /// Usado para colorear la columna de Utilidad (rojo si negativa, verde si positiva)
    /// </summary>
    public class IsNegativeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue < 0;
            }
            if (value is double doubleValue)
            {
                return doubleValue < 0;
            }
            if (value is int intValue)
            {
                return intValue < 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
