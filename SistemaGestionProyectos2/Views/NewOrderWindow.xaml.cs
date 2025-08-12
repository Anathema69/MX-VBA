using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewOrderWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private List<ClientData> _clients;
        private List<ContactData> _contacts;
        private List<UserSession> _vendors;

        public NewOrderWindow()
        {
            InitializeComponent();
            // _supabaseService = SupabaseService.Instance;
            LoadSampleData();
        }

        

        private void LoadSampleData()
        {
            try
            {
                // Datos de ejemplo para clientes
                _clients = new List<ClientData>
        {
            new ClientData { Id = 1, Name = "Ventas Industriales" },
            new ClientData { Id = 2, Name = "BorgWarner" },
            new ClientData { Id = 3, Name = "Lennox" },
            new ClientData { Id = 4, Name = "La Casa del Caballo" },
            new ClientData { Id = 5, Name = "Gerber" },
            new ClientData { Id = 6, Name = "Engicom" },
            new ClientData { Id = 7, Name = "Purem" },
            new ClientData { Id = 8, Name = "Android" }
        };
                ClientComboBox.ItemsSource = _clients;

                // Datos de ejemplo para vendedores
                _vendors = new List<UserSession>
        {
            new UserSession { Id = 1, FullName = "MARIO GARZA", Role = "salesperson" },
            new UserSession { Id = 2, FullName = "CYNTHIA GARCÍA", Role = "salesperson" },
            new UserSession { Id = 3, FullName = "JEHU ARREDONDO", Role = "salesperson" },
            new UserSession { Id = 4, FullName = "LEONARDO DAVID", Role = "salesperson" },
            new UserSession { Id = 5, FullName = "EDSON", Role = "salesperson" },
            new UserSession { Id = 6, FullName = "JUAN MORENO", Role = "salesperson" }
        };
                VendorComboBox.ItemsSource = _vendors;

                // Establecer fecha por defecto
                OrderDatePicker.SelectedDate = DateTime.Now;
                DeliveryDatePicker.SelectedDate = DateTime.Now.AddDays(30);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cargar datos:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ClientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientComboBox.SelectedItem is ClientData selectedClient)
            {
                ContactComboBox.IsEnabled = true;

                // Datos de ejemplo para contactos
                _contacts = new List<ContactData>
        {
            new ContactData
            {
                Id = 1,
                Name = $"Contacto Principal - {selectedClient.Name}",
                Email = $"contacto@{selectedClient.Name.ToLower().Replace(" ", "")}.com"
            },
            new ContactData
            {
                Id = 2,
                Name = $"Contacto Secundario - {selectedClient.Name}",
                Email = $"ventas@{selectedClient.Name.ToLower().Replace(" ", "")}.com"
            }
        };

                ContactComboBox.ItemsSource = _contacts;

                // Si solo hay un contacto, seleccionarlo automáticamente
                if (_contacts.Count == 1)
                {
                    ContactComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                ContactComboBox.IsEnabled = false;
                ContactComboBox.ItemsSource = null;
            }
        }

        private void SubtotalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateTotal();
        }

        private void CalculateTotal()
        {
            if (decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal))
            {
                decimal total = subtotal * 1.16m; // Agregar 16% de IVA
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));
            }
            else
            {
                TotalTextBlock.Text = "$ 0.00";
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números y punto decimal
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            var newText = (sender as TextBox).Text + e.Text;
            e.Handled = !regex.IsMatch(newText);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validar campos obligatorios
            if (!ValidateForm())
            {
                return;
            }

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // En modo offline, solo mostrar mensaje de éxito
                MessageBox.Show(
                    $"Orden {OrderNumberTextBox.Text} guardada exitosamente.\n\n" +
                    $"Cliente: {(ClientComboBox.SelectedItem as ClientData)?.Name}\n" +
                    $"Vendedor: {(VendorComboBox.SelectedItem as UserSession)?.FullName}\n" +
                    $"Total: {TotalTextBlock.Text}\n\n" +
                    "(Modo offline - Los datos se guardan temporalmente)",
                    "Orden Guardada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar la orden:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR";
            }
        }

        private bool ValidateForm()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(OrderNumberTextBox.Text))
                errors.Add("• Orden de Compra es obligatorio");

            if (!OrderDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha O.C. es obligatoria");

            if (ClientComboBox.SelectedItem == null)
                errors.Add("• Cliente es obligatorio");

            if (ContactComboBox.SelectedItem == null)
                errors.Add("• Contacto es obligatorio");

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
                errors.Add("• Descripción es obligatoria");

            if (VendorComboBox.SelectedItem == null)
                errors.Add("• Vendedor es obligatorio");

            if (string.IsNullOrWhiteSpace(SubtotalTextBox.Text) ||
                !decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal) ||
                subtotal <= 0)
                errors.Add("• Subtotal debe ser mayor a 0");

            if (!DeliveryDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha de Entrega es obligatoria");

            if (errors.Any())
            {
                MessageBox.Show(
                    "Por favor corrija los siguientes errores:\n\n" + string.Join("\n", errors),
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "¿Está seguro que desea cancelar?\nLos datos no guardados se perderán.",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Función para agregar nuevo cliente.\nEn desarrollo...",
                "Nuevo Cliente",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}