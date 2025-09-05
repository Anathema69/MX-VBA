using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Lógica de interacción para VendorCardsDemo.xaml
    /// </summary>
    public partial class VendorCardsDemo : Window
    {
        private readonly UserSession _currentUser;
        public VendorCardsDemo(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            
        }

        private void Commission_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                textBox.IsReadOnly = false;
                textBox.SelectAll();
                textBox.Focus();
            }
        }

        private void Commission_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsReadOnly)
            {
                textBox.SelectAll();
            }
        }

        private void Commission_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                textBox.IsReadOnly = true;

                // Validar el valor
                if (!decimal.TryParse(textBox.Text, out decimal value) || value < 0 || value > 100)
                {
                    MessageBox.Show("La comisión debe ser un número entre 0 y 100", "Valor inválido",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Text = "10"; // Valor por defecto
                }
                else
                {
                    // Aquí guardarías en la BD
                    MessageBox.Show($"Comisión actualizada a {value}%", "Demo",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void Commission_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            // Permitir solo números y un punto decimal
            var regex = new System.Text.RegularExpressions.Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(newText);
        }

        private void ManageVendors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /* tomar las credenciales del usuario actual */



                var vendorManagementWindow = new VendorManagementWindow(_currentUser);
                vendorManagementWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir gestión de vendedores: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

    }
}