// Archivo: Services/RoleToVisibilityConverter.cs

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SistemaGestionProyectos2.Services
{
    public class RoleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role && parameter is string requiredRole)
            {
                return role == requiredRole ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    
    public class IsAdminToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Roles v2.0: direccion y administracion tienen permisos de admin
            if (value is string role)
            {
                return (role == "direccion" || role == "administracion") ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}