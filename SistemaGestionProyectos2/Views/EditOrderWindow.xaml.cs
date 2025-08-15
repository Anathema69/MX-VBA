using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGestionProyectos2.Views
{
    public partial class EditOrderWindow : Window
    {
        private OrderViewModel _order;
        private UserSession _currentUser;
        private readonly SupabaseService _supabaseService;
        private List<OrderStatusDb> _orderStatuses;
        private bool _hasChanges = false;
        private OrderDb _originalOrderDb;

        // Valor para el subtotal, se usa para formatear correctamente al perder el foco
        private decimal _subtotalValue = 0;
        public EditOrderWindow(OrderViewModel order, UserSession currentUser)
        {
            InitializeComponent();
            _order = order;
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            ConfigurePermissions();
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Cargando...";

                // Cargar estados desde Supabase
                _orderStatuses = await _supabaseService.GetOrderStatuses();

                // Cargar la orden original desde la BD para tener todos los campos
                _originalOrderDb = await _supabaseService.GetOrderById(_order.Id);

                if (_originalOrderDb == null)
                {
                    MessageBox.Show(
                        "No se pudo cargar la información de la orden.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                // Llenar el ComboBox de estados
                StatusComboBox.Items.Clear();
                foreach (var status in _orderStatuses.OrderBy(s => s.DisplayOrder))
                {
                    StatusComboBox.Items.Add(new ComboBoxItem { Content = status.Name, Tag = status.Id });
                }

                LoadOrderData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cargar datos:\n{ex.Message}",
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
                    
                    // Coordinador NO puede editar PO
                    OrderNumberTextBox.IsReadOnly = true;
                    OrderNumberTextBox.Background = System.Windows.Media.Brushes.LightGray;
                    break;

                case "admin":
                    // Admin: Puede editar todo
                    PermissionsText.Text = "Como Administrador, puede editar todos los campos disponibles incluyendo Orden de Compra";
                    PermissionsNotice.Background = System.Windows.Media.Brushes.LightGreen;

                    // Mostrar sección financiera
                    FinancialSection.Visibility = Visibility.Visible;
                    FinancialFields.Visibility = Visibility.Visible;

                    OrderNumberTextBox.IsReadOnly = false;
                    OrderNumberTextBox.Background = System.Windows.Media.Brushes.White;
                    OrderNumberTextBox.Tag = "admin";
                    break;

                default:
                    // No debería llegar aquí
                    MessageBox.Show("No tiene permisos para editar órdenes", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    break;
            }
        }

        
        

        

        

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            fullText = fullText.Replace("$", "").Replace(",", "").Trim();

            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(fullText);
        }

        private void SelectComboBoxItemByTag(ComboBox comboBox, int statusId)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag is int tagId && tagId == statusId)
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

        

        private void UpdateSaveStatus()
        {
            if (_hasChanges)
            {
                SaveStatusText.Text = "⚠ Hay cambios sin guardar";
                SaveStatusText.Foreground = System.Windows.Media.Brushes.Orange;
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
            ProgressSlider.Value = _originalOrderDb?.ProgressPercentage ?? _order.ProgressPercentage;
            ProgressValueText.Text = $"{(int)ProgressSlider.Value}%";

            // Seleccionar el estado actual
            SelectComboBoxItemByTag(StatusComboBox, _originalOrderDb?.OrderStatus ?? 1);

            // Campos financieros (solo Admin)
            if (_currentUser.Role == "admin")
            {
                // IMPORTANTE: Establecer _subtotalValue con el valor actual
                _subtotalValue = _order.Subtotal;

                // Mostrar con formato de moneda
                SubtotalTextBox.Text = _subtotalValue.ToString("C", new CultureInfo("es-MX"));
                TotalTextBlock.Text = _order.Total.ToString("C", new CultureInfo("es-MX"));
                OrderPercentageSlider.Value = _originalOrderDb?.OrderPercentage ?? _order.OrderPercentage;
                OrderPercentageText.Text = $"{(int)OrderPercentageSlider.Value}%";

                System.Diagnostics.Debug.WriteLine($"💰 Subtotal inicial cargado: {_subtotalValue:C}");
            }

            // Información de auditoría
            LastModifiedText.Text = $"Última modificación: {DateTime.Now:dd/MM/yyyy HH:mm} - Editando como: {_currentUser.FullName}";
        }

        // Actualizar SubtotalTextBox_TextChanged para actualizar _subtotalValue
        private void SubtotalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Solo procesar si el TextBox tiene el foco (está siendo editado activamente)
            if (!SubtotalTextBox.IsFocused)
                return;

            string text = SubtotalTextBox.Text.Replace("$", "").Replace(",", "").Replace(" ", "").Trim();

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
            {
                _subtotalValue = subtotal;
                decimal total = subtotal * 1.16m;
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));

                System.Diagnostics.Debug.WriteLine($"📝 Subtotal actualizado mientras se escribe: {_subtotalValue:C}");
            }
        }

        // Actualizar SubtotalTextBox_GotFocus
        private void SubtotalTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Mostrar el valor sin formato para facilitar la edición
            if (_subtotalValue > 0)
            {
                SubtotalTextBox.Text = _subtotalValue.ToString("F2");
            }
            SubtotalTextBox.SelectAll();

            System.Diagnostics.Debug.WriteLine($"🔍 GotFocus - Mostrando valor para editar: {_subtotalValue}");
        }

        // Actualizar SubtotalTextBox_LostFocus
        private void SubtotalTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Limpiar formato y actualizar _subtotalValue
            string cleanText = SubtotalTextBox.Text
                .Replace("$", "")
                .Replace(",", "")
                .Replace(" ", "")
                .Replace("MXN", "")
                .Replace("MX", "")
                .Trim();

            System.Diagnostics.Debug.WriteLine($"🔍 LostFocus - Texto limpio: '{cleanText}'");

            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
            {
                _subtotalValue = subtotal;
                SubtotalTextBox.Text = subtotal.ToString("C", new CultureInfo("es-MX"));

                // Actualizar el total
                decimal total = _subtotalValue * 1.16m;
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));

                System.Diagnostics.Debug.WriteLine($"✅ Subtotal final establecido: {_subtotalValue:C}");
            }
            else if (_subtotalValue > 0)
            {
                // Si no se pudo parsear pero tenemos un valor previo, restaurarlo
                SubtotalTextBox.Text = _subtotalValue.ToString("C", new CultureInfo("es-MX"));
                System.Diagnostics.Debug.WriteLine($"⚠️ Restaurando valor previo: {_subtotalValue:C}");
            }
            else
            {
                _subtotalValue = 0;
                SubtotalTextBox.Text = "$0.00";
                System.Diagnostics.Debug.WriteLine($"❌ Sin valor válido, estableciendo a 0");
            }
        }

        // IMPORTANTE: Actualizar SaveButton_Click para usar _subtotalValue
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
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

                // Log detallado de lo que se está guardando
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"💾 GUARDANDO CAMBIOS EN ORDEN {_originalOrderDb.Id}");
                System.Diagnostics.Debug.WriteLine($"   Usuario: {_currentUser.FullName} (ID: {_currentUser.Id})");
                System.Diagnostics.Debug.WriteLine($"   Rol: {_currentUser.Role}");
                System.Diagnostics.Debug.WriteLine("========================================");

                // Preparar la orden actualizada
                _originalOrderDb.EstDelivery = PromiseDatePicker.SelectedDate.Value;
                _originalOrderDb.ProgressPercentage = (int)ProgressSlider.Value;

                var selectedStatus = StatusComboBox.SelectedItem as ComboBoxItem;
                if (selectedStatus?.Tag is int statusId)
                {
                    _originalOrderDb.OrderStatus = statusId;
                }

                // Si es admin, actualizar campos financieros
                if (_currentUser.Role == "admin")
                {
                    // Actualizar PO si cambió
                    _originalOrderDb.Po = OrderNumberTextBox.Text.Trim().ToUpper();

                    // USAR _subtotalValue EN LUGAR DE PARSEAR EL TEXTO
                    _originalOrderDb.SaleSubtotal = _subtotalValue;
                    _originalOrderDb.SaleTotal = _subtotalValue * 1.16m;
                    _originalOrderDb.OrderPercentage = (int)OrderPercentageSlider.Value;

                    System.Diagnostics.Debug.WriteLine($"   📊 Campos financieros actualizados:");
                    System.Diagnostics.Debug.WriteLine($"      Subtotal (desde _subtotalValue): ${_subtotalValue:N2}");
                    System.Diagnostics.Debug.WriteLine($"      Total calculado: ${(_subtotalValue * 1.16m):N2}");
                    System.Diagnostics.Debug.WriteLine($"      Order %: {_originalOrderDb.OrderPercentage}%");
                }

                System.Diagnostics.Debug.WriteLine($"   📅 Fecha Promesa: {_originalOrderDb.EstDelivery:yyyy-MM-dd}");
                System.Diagnostics.Debug.WriteLine($"   📈 Progress %: {_originalOrderDb.ProgressPercentage}%");
                System.Diagnostics.Debug.WriteLine($"   🔖 Estado ID: {_originalOrderDb.OrderStatus}");

                // IMPORTANTE: Pasar el ID del usuario actual
                bool success = await _supabaseService.UpdateOrder(_originalOrderDb, _currentUser.Id);

                if (success)
                {
                    // Actualizar el objeto local para reflejar los cambios
                    _order.PromiseDate = _originalOrderDb.EstDelivery.Value;
                    _order.ProgressPercentage = _originalOrderDb.ProgressPercentage;

                    var statusName = _orderStatuses.FirstOrDefault(s => s.Id == _originalOrderDb.OrderStatus)?.Name;
                    _order.Status = statusName ?? "PENDIENTE";

                    if (_currentUser.Role == "admin")
                    {
                        _order.OrderNumber = _originalOrderDb.Po;
                        _order.Subtotal = _originalOrderDb.SaleSubtotal ?? 0;
                        _order.Total = _originalOrderDb.SaleTotal ?? 0;
                        _order.OrderPercentage = _originalOrderDb.OrderPercentage;
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Orden actualizada exitosamente");
                    System.Diagnostics.Debug.WriteLine($"   Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    System.Diagnostics.Debug.WriteLine("========================================");

                    MessageBox.Show(
                        $"✅ Orden {_order.OrderNumber} actualizada correctamente\n\n",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    throw new Exception("No se pudo actualizar la orden en la base de datos");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"❌ ERROR AL GUARDAR CAMBIOS:");
                System.Diagnostics.Debug.WriteLine($"   Mensaje: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("========================================");

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

        // Detectar cambios en los campos
        private void PromiseDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PromiseDatePicker.SelectedDate != _order.PromiseDate)
            {
                _hasChanges = true;
                UpdateSaveStatus();
            }
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _hasChanges = true;
            UpdateSaveStatus();
        }
    }
}