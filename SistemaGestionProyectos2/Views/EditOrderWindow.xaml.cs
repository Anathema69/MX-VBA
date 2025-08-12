using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SistemaGestionProyectos2.Models;

namespace SistemaGestionProyectos2.Views
{
    public partial class EditOrderWindow : Window
    {
        private OrderViewModel _order;
        private UserSession _currentUser;
        private bool _hasChanges = false;

        public EditOrderWindow(OrderViewModel order, UserSession currentUser)
        {
            InitializeComponent();
            _order = order;
            _currentUser = currentUser;

            ConfigurePermissions();
            LoadOrderData();
        }

        private void ConfigurePermissions()
        {
            // Configurar según el rol
            UserRoleText.Text = GetRoleDisplayName(_currentUser.Role);

            switch (_currentUser.Role)
            {
                case "coordinator":
                    // Coordinador: Solo puede editar Fecha Promesa, % Avance y Estatus
                    PermissionsText.Text = "Como Coordinador, puede editar: Fecha Promesa, % Avance y Estatus";

                    // Ocultar sección financiera
                    FinancialSection.Visibility = Visibility.Collapsed;
                    FinancialFields.Visibility = Visibility.Collapsed;
                    break;

                case "admin":
                    // Admin: Puede editar todo
                    PermissionsText.Text = "Como Administrador, puede editar todos los campos disponibles";
                    PermissionsNotice.Background = System.Windows.Media.Brushes.LightGreen;

                    // Mostrar sección financiera
                    FinancialSection.Visibility = Visibility.Visible;
                    FinancialFields.Visibility = Visibility.Visible;
                    break;

                default:
                    // No debería llegar aquí
                    MessageBox.Show("No tiene permisos para editar órdenes", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    break;
            }
        }

        private void LoadOrderData()
        {
            // Cargar datos de la orden
            OrderNumberHeader.Text = $" - #{_order.OrderNumber}";

            // Campos de solo lectura
            OrderNumberTextBox.Text = _order.OrderNumber;
            OrderDateTextBox.Text = _order.OrderDate.ToString("dd/MM/yyyy");
            ClientTextBox.Text = _order.ClientName;
            VendorTextBox.Text = _order.VendorName;
            DescriptionTextBox.Text = _order.Description;

            // Campos editables para todos
            PromiseDatePicker.SelectedDate = _order.PromiseDate;
            ProgressSlider.Value = _order.ProgressPercentage;
            ProgressValueText.Text = $"{_order.ProgressPercentage}%";

            // Seleccionar el estado actual
            SelectComboBoxItem(StatusComboBox, _order.Status);

            // Campos financieros (solo Admin)
            if (_currentUser.Role == "admin")
            {
                SubtotalTextBox.Text = _order.Subtotal.ToString("F2");
                TotalTextBlock.Text = _order.Total.ToString("C", new CultureInfo("es-MX"));
                OrderPercentageSlider.Value = _order.OrderPercentage;
                OrderPercentageText.Text = $"{_order.OrderPercentage}%";
            }

            // Información de auditoría
            LastModifiedText.Text = $"Última modificación: {DateTime.Now:dd/MM/yyyy HH:mm} por {_currentUser.FullName}";
        }

        private void SelectComboBoxItem(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetRoleDisplayName(string role)
        {
            switch (role)
            {
                case "admin": return "Administrador";
                case "coordinator": return "Coordinador";
                case "salesperson": return "Vendedor";
                default: return role;
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProgressValueText != null)
            {
                ProgressValueText.Text = $"{(int)ProgressSlider.Value}%";
                _hasChanges = true;
                UpdateSaveStatus();
            }
        }

        private void OrderPercentageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OrderPercentageText != null)
            {
                OrderPercentageText.Text = $"{(int)OrderPercentageSlider.Value}%";
                _hasChanges = true;
                UpdateSaveStatus();
            }
        }

        private void SubtotalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal))
            {
                decimal total = subtotal * 1.16m;
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));
                _hasChanges = true;
                UpdateSaveStatus();
            }
        }

        private void UpdateSaveStatus()
        {
            if (_hasChanges)
            {
                SaveStatusText.Text = "⚠ Hay cambios sin guardar";
                SaveStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar campos obligatorios
                if (!PromiseDatePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("La fecha promesa es obligatoria", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (StatusComboBox.SelectedItem == null)
                {
                    MessageBox.Show("El estatus es obligatorio", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // Actualizar el objeto orden con los nuevos valores
                _order.PromiseDate = PromiseDatePicker.SelectedDate.Value;
                _order.ProgressPercentage = (int)ProgressSlider.Value;
                _order.Status = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

                // Si es admin, actualizar campos financieros
                if (_currentUser.Role == "admin")
                {
                    if (decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal))
                    {
                        _order.Subtotal = subtotal;
                        _order.Total = subtotal * 1.16m;
                    }
                    _order.OrderPercentage = (int)OrderPercentageSlider.Value;
                }

                // Simular guardado
                System.Threading.Thread.Sleep(500);

                // Crear mensaje de confirmación
                string changedFields = "Campos actualizados:\n";
                changedFields += $"• Fecha Promesa: {_order.PromiseDate:dd/MM/yyyy}\n";
                changedFields += $"• % Avance: {_order.ProgressPercentage}%\n";
                changedFields += $"• Estatus: {_order.Status}";

                if (_currentUser.Role == "admin")
                {
                    changedFields += $"\n• Subtotal: {_order.Subtotal:C}";
                    changedFields += $"\n• Total: {_order.Total:C}";
                    changedFields += $"\n• % Orden: {_order.OrderPercentage}%";
                }

                MessageBox.Show(
                    $"Orden {_order.OrderNumber} actualizada exitosamente.\n\n{changedFields}\n\n" +
                    $"Modificado por: {_currentUser.FullName}\n" +
                    $"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}\n\n" +
                    "(Modo offline - Los cambios se guardan temporalmente)",
                    "Actualización Exitosa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar los cambios:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR CAMBIOS";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasChanges)
            {
                var result = MessageBox.Show(
                    "Hay cambios sin guardar. ¿Está seguro que desea salir sin guardar?",
                    "Confirmar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.DialogResult = false;
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasChanges && DialogResult != true)
            {
                var result = MessageBox.Show(
                    "Hay cambios sin guardar. ¿Está seguro que desea salir?",
                    "Cambios sin guardar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }

            base.OnClosing(e);
        }
    }
}