using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
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
        private List<ClientDb> _clients;
        private List<ContactDb> _contacts;
        private List<VendorDb> _vendors;
        private bool _hasChanges = false;
        private OrderDb _originalOrderDb;
        private decimal _subtotalValue = 0;
        private int _currentStatusId = 0;
        private bool _isLoadingData = false;
        private bool _isCancellingFromButton = false; // Agregar esta variable de instancia al inicio de la clase


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
                if (_currentUser.Role == "admin")
                {
                    // SOLUCIÓN: Colapsar todo el Grid de campos de solo lectura
                    ReadOnlyFieldsGrid.Visibility = Visibility.Collapsed;
                    // Buscar el elemento por nombre (útil si el nombre viene de una variable)
                    var border = this.FindName("FieldNameBasic") as Border;
                    if (border != null)
                    {
                        border.Visibility = Visibility.Collapsed;
                    }

                }

                _isLoadingData = true;
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Cargando...";

                // Cargar todos los datos necesarios
                var loadTasks = new List<Task>
                {
                    Task.Run(async () => _orderStatuses = await _supabaseService.GetOrderStatuses()),
                    Task.Run(async () => _clients = await _supabaseService.GetClients()),
                    Task.Run(async () => _vendors = await _supabaseService.GetVendors())
                };

                await Task.WhenAll(loadTasks);

                // Cargar la orden original
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

                _currentStatusId = _originalOrderDb.OrderStatus ?? 0;

                // Cargar contactos del cliente actual si existe
                if (_originalOrderDb.ClientId.HasValue)
                {
                    _contacts = await _supabaseService.GetContactsByClient(_originalOrderDb.ClientId.Value);
                }

                // Configurar controles
                await Dispatcher.InvokeAsync(() =>
                {
                    SetupControls();
                    LoadOrderData();
                    ApplyStatusRestrictions();
                });
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
                _isLoadingData = false;
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR CAMBIOS";
            }
        }

        private void SetupControls()
        {
            // Llenar ComboBox de estados según el estado actual
            StatusComboBox.Items.Clear();

            List<OrderStatusDb> availableStatuses = new List<OrderStatusDb>();

            // Lógica de estados disponibles según el estado actual (SIN CANCELADA)
            switch (_currentStatusId)
            {
                case 0: // CREADA
                        // Puede ir a: CREADA (mismo), EN PROCESO
                    availableStatuses = _orderStatuses
                        .Where(s => s.Id == 0 || s.Id == 1)
                        .OrderBy(s => s.DisplayOrder)
                        .ToList();
                    break;

                case 1: // EN PROCESO
                        // Puede ir a: EN PROCESO (mismo), LIBERADA
                    availableStatuses = _orderStatuses
                        .Where(s => s.Id == 1 || s.Id == 2)
                        .OrderBy(s => s.DisplayOrder)
                        .ToList();
                    break;

                case 2: // LIBERADA
                        // Solo puede quedarse en LIBERADA
                    availableStatuses = _orderStatuses
                        .Where(s => s.Id == 2)
                        .ToList();
                    break;

                default: // CERRADA, COMPLETADA, CANCELADA
                         // Estados finales - no se pueden cambiar
                    availableStatuses = _orderStatuses
                        .Where(s => s.Id == _currentStatusId)
                        .ToList();
                    break;
            }

            foreach (var status in availableStatuses)
            {
                StatusComboBox.Items.Add(new ComboBoxItem
                {
                    Content = status.Name,
                    Tag = status.Id
                });
            }

            // Si es admin, configurar controles adicionales
            if (_currentUser.Role == "admin")
            {
                // Llenar combo de clientes
                EditableClientComboBox.ItemsSource = _clients;
                EditableClientComboBox.DisplayMemberPath = "Name";
                EditableClientComboBox.SelectedValuePath = "Id";
                EditableClientComboBox.SelectionChanged += EditableClientComboBox_SelectionChanged;

                // Llenar combo de vendedores
                EditableVendorComboBox.ItemsSource = _vendors;
                EditableVendorComboBox.DisplayMemberPath = "VendorName";
                EditableVendorComboBox.SelectedValuePath = "Id";

                // Llenar combo de contactos si hay
                if (_contacts != null && _contacts.Count > 0)
                {
                    EditableContactComboBox.ItemsSource = _contacts;
                    EditableContactComboBox.DisplayMemberPath = "ContactName";
                    EditableContactComboBox.SelectedValuePath = "Id";
                }
            }
        }

        private void ConfigurePermissions()
        {
            // Configurar según el rol
            UserRoleText.Text = GetRoleDisplayName(_currentUser.Role);

            switch (_currentUser.Role)
            {
                case "coordinator":
                    PermissionsText.Text = "Como Coordinador, puede editar: Fecha Promesa, % Avance y Estatus";

                    // Ocultar todos los campos de admin
                    OrderNumberEditPanel.Visibility = Visibility.Collapsed;
                    FechaCompraEditPanel.Visibility = Visibility.Collapsed; 
                    
                    AdminFieldsPanel1.Visibility = Visibility.Collapsed;
                    AdminFieldsPanel2.Visibility = Visibility.Collapsed;
                    AdminDescriptionPanel.Visibility = Visibility.Collapsed;
                    FinancialSection.Visibility = Visibility.Collapsed;
                    FinancialFields.Visibility = Visibility.Collapsed;

                    // Campos de solo lectura mantienen su visibilidad normal
                    OrderNumberTextBox.IsReadOnly = true;
                    OrderNumberTextBox.Background = System.Windows.Media.Brushes.LightGray;
                    break;

                case "admin":
                    PermissionsText.Text = "Como Administrador, puede editar todos los campos disponibles (excepto Fecha O.C.)";
                    PermissionsNotice.Background = System.Windows.Media.Brushes.LightGreen;

                    // Mostrar campos editables de admin
                    OrderNumberEditPanel.Visibility = Visibility.Visible;

                    // Aparece fecha pero no es editable
                    FechaCompraEditPanel.Visibility = Visibility.Visible;
                    FechaCompraEditPanel.IsEnabled = false;

                    AdminFieldsPanel1.Visibility = Visibility.Visible;
                    AdminFieldsPanel2.Visibility = Visibility.Visible;
                    AdminDescriptionPanel.Visibility = Visibility.Visible;
                    FinancialSection.Visibility = Visibility.Visible;
                    FinancialFields.Visibility = Visibility.Visible;

                    // Ocultar campos de solo lectura que ahora son editables
                    // (excepto fecha que nunca es editable)
                    break;

                default:
                    MessageBox.Show("No tiene permisos para editar órdenes", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    break;
            }
        }

        private void LoadOrderData()
        {
            // Cargar datos de la orden
            OrderNumberHeader.Text = $" - #{_order.OrderNumber}";

            // Campos de solo lectura (siempre)
            OrderDateTextBox.Text = _order.OrderDate.ToString("dd/MM/yyyy");
            EditableFechaCompra.Text = OrderDateTextBox.Text;


            // Imprimir en consola la fehca de O.C


            if (_currentUser.Role == "admin")
            {

                
                
                // Admin: cargar en campos editables
                EditableOrderNumberTextBox.Text = _order.OrderNumber;
                EditableQuotationTextBox.Text = _originalOrderDb?.Quote ?? "";
                EditableClientComboBox.SelectedValue = _originalOrderDb?.ClientId;
                EditableContactComboBox.SelectedValue = _originalOrderDb?.ContactId;
                EditableVendorComboBox.SelectedValue = _originalOrderDb?.SalesmanId;
                EditableDescriptionTextBox.Text = _order.Description;

                

                // Campos financieros
                _subtotalValue = _order.Subtotal;
                SubtotalTextBox.Text = _subtotalValue.ToString("C", new CultureInfo("es-MX"));
                TotalTextBlock.Text = _order.Total.ToString("C", new CultureInfo("es-MX"));
            }
            else
            {

                // Coordinador: mostrar en solo lectura - mantener visible
                ReadOnlyFieldsGrid.Visibility = Visibility.Visible;

                OrderNumberTextBox.Text = _order.OrderNumber;
                ClientTextBox.Text = _order.ClientName;
                VendorTextBox.Text = _order.VendorName;
                DescriptionTextBox.Text = _order.Description;
            }

            // Campos editables para todos
            PromiseDatePicker.SelectedDate = _order.PromiseDate;
            ProgressSlider.Value = _originalOrderDb?.ProgressPercentage ?? _order.ProgressPercentage;
            ProgressValueText.Text = $"{(int)ProgressSlider.Value}%";

            // Seleccionar el estado actual
            SelectComboBoxItemByTag(StatusComboBox, _originalOrderDb?.OrderStatus ?? 1);

            // Información de auditoría
            LastModifiedText.Text = $"Última modificación: {DateTime.Now:dd/MM/yyyy HH:mm} - Editando como: {_currentUser.FullName}";
        }

        private void ApplyStatusRestrictions()
        {
            // Obtener el nombre del estado actual
            var currentStatus = _orderStatuses?.FirstOrDefault(s => s.Id == _currentStatusId);
            var statusName = currentStatus?.Name ?? "DESCONOCIDO";

            System.Diagnostics.Debug.WriteLine($"📊 Estado actual: {statusName} (ID: {_currentStatusId})");

            // Aplicar restricciones según el estado
            switch (statusName.ToUpper())
            {
                case "LIBERADA":
                    // Bloquear % Avance y Estado
                    ProgressSlider.IsEnabled = false;
                    ProgressSlider.Value = 100; // Forzar a 100%
                    StatusComboBox.IsEnabled = false;

                    // Mostrar advertencia
                    EditableSectionTitle.Text = "CAMPOS EDITABLES (ORDEN LIBERADA - RESTRICCIONES APLICADAS)";
                    EditableSectionTitle.Foreground = System.Windows.Media.Brushes.Orange;
                    break;

                case "CERRADA":
                case "COMPLETADA":
                    // Bloquear todo
                    DisableAllControls();
                    EditableSectionTitle.Text = $"ORDEN {statusName} - SOLO LECTURA";
                    EditableSectionTitle.Foreground = System.Windows.Media.Brushes.Red;
                    SaveButton.IsEnabled = false;
                    break;

                case "CANCELADA":
                    // Bloquear todo
                    DisableAllControls();
                    EditableSectionTitle.Text = "ORDEN CANCELADA - NO EDITABLE";
                    EditableSectionTitle.Foreground = System.Windows.Media.Brushes.Red;
                    SaveButton.IsEnabled = false;
                    break;
            }
        }

        private void DisableAllControls()
        {
            // Deshabilitar todos los controles editables
            PromiseDatePicker.IsEnabled = false;
            ProgressSlider.IsEnabled = false;
            StatusComboBox.IsEnabled = false;

            if (_currentUser.Role == "admin")
            {
                EditableOrderNumberTextBox.IsEnabled = false;
                EditableQuotationTextBox.IsEnabled = false;
                EditableClientComboBox.IsEnabled = false;
                EditableContactComboBox.IsEnabled = false;
                EditableVendorComboBox.IsEnabled = false;
                EditableDescriptionTextBox.IsEnabled = false;
                SubtotalTextBox.IsEnabled = false;
            }
        }

        private async void EditableClientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingData) return;

            var selectedClient = EditableClientComboBox.SelectedItem as ClientDb;
            if (selectedClient != null)
            {
                try
                {
                    _contacts = await _supabaseService.GetContactsByClient(selectedClient.Id);

                    EditableContactComboBox.ItemsSource = _contacts;
                    EditableContactComboBox.DisplayMemberPath = "ContactName";
                    EditableContactComboBox.SelectedValuePath = "Id";

                    if (_contacts.Count == 1)
                    {
                        EditableContactComboBox.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cargando contactos: {ex.Message}");
                }
            }
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingData) return;

            var selectedItem = StatusComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is int newStatusId)
            {
                var newStatus = _orderStatuses?.FirstOrDefault(s => s.Id == newStatusId);
                var newStatusName = newStatus?.Name ?? "";

                // Validar si el cambio es permitido
                if (!ValidateStatusChange(_currentStatusId, newStatusId))
                {
                    // Revertir el cambio
                    SelectComboBoxItemByTag(StatusComboBox, _currentStatusId);
                    return;
                }

                // Solo llamar a HandleCancelOrder si NO viene del botón
                if (newStatusName.ToUpper() == "CANCELADA" && !_isCancellingFromButton)
                {
                    HandleCancelOrder();
                }
            }
        }

        private void HandleCancelOrder()
        {
            // Primera confirmación
            var result1 = MessageBox.Show(
                $"¿Está seguro que desea CANCELAR la orden {_order.OrderNumber}?\n\n" +
                "ADVERTENCIA: Esta acción NO se puede deshacer.\n" +
                "La orden quedará permanentemente cancelada.",
                "Confirmar Cancelación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result1 != MessageBoxResult.Yes)
            {
                // Revertir el cambio
                SelectComboBoxItemByTag(StatusComboBox, _currentStatusId);
                return;
            }

            // Segunda confirmación con código
            var confirmCode = $"CANCELAR-{_order.OrderNumber}";
            var inputWindow = new Window
            {
                Title = "Confirmación de Seguridad",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label1 = new TextBlock
            {
                Text = "Para confirmar la cancelación, escriba el siguiente código:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var label2 = new TextBlock
            {
                Text = confirmCode,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = System.Windows.Media.Brushes.Red
            };

            var textBox = new TextBox
            {
                Height = 30,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Prevenir pegar
            DataObject.AddPastingHandler(textBox, (s, e) => { e.CancelCommand(); });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var confirmButton = new Button
            {
                Content = "CONFIRMAR CANCELACIÓN",
                Width = 200,
                Height = 40,
                Margin = new Thickness(0, 0, 10, 0),
                Background = System.Windows.Media.Brushes.Red,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold
            };

            var cancelButton = new Button
            {
                Content = "Cancelar",
                Width = 100,
                Height = 35
            };

            bool confirmed = false;

            confirmButton.Click += (s, e) =>
            {
                if (textBox.Text == confirmCode)
                {
                    confirmed = true;
                    inputWindow.Close();
                }
                else
                {
                    MessageBox.Show("El código no coincide. Intente nuevamente.",
                        "Código Incorrecto", MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Clear();
                    textBox.Focus();
                }
            };

            cancelButton.Click += (s, e) => inputWindow.Close();

            buttonPanel.Children.Add(confirmButton);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(label1, 0);
            Grid.SetRow(label2, 1);
            Grid.SetRow(textBox, 2);
            Grid.SetRow(buttonPanel, 3);

            grid.Children.Add(label1);
            grid.Children.Add(label2);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            inputWindow.Content = grid;
            textBox.Focus();

            inputWindow.ShowDialog();

            if (!confirmed)
            {
                // Revertir el cambio si no se confirmó
                SelectComboBoxItemByTag(StatusComboBox, _currentStatusId);
            }
        }

        private bool ValidateStatusChange(int fromStatusId, int toStatusId)
        {
            var fromStatus = _orderStatuses?.FirstOrDefault(s => s.Id == fromStatusId);
            var toStatus = _orderStatuses?.FirstOrDefault(s => s.Id == toStatusId);

            var fromName = fromStatus?.Name?.ToUpper() ?? "";
            var toName = toStatus?.Name?.ToUpper() ?? "";

            System.Diagnostics.Debug.WriteLine($"🔄 Validando cambio de estado: {fromName} → {toName}");

            // No permitir cambios desde estados finales
            if (fromName == "LIBERADA" || fromName == "CERRADA" ||
                fromName == "COMPLETADA" || fromName == "CANCELADA")
            {
                if (fromStatusId != toStatusId)
                {
                    MessageBox.Show(
                        $"No se puede cambiar el estado desde {fromName}.\n" +
                        "Este es un estado final o automático.",
                        "Cambio No Permitido",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            /* No permitir cambiar manualmente a LIBERADA, CERRADA o COMPLETADA
            if (( toName == "CERRADA" || toName == "COMPLETADA") &&
                fromStatusId != toStatusId)
            {
                MessageBox.Show(
                    $"El estado {toName} es automático.\n" +
                    "Se activará cuando se cumplan las condiciones necesarias.",
                    "Estado Automático",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }*/

            return true;
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

        private void SubtotalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!SubtotalTextBox.IsFocused) return;

            string text = SubtotalTextBox.Text.Replace("$", "").Replace(",", "").Replace(" ", "").Trim();

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
            {
                _subtotalValue = subtotal;
                decimal total = subtotal * 1.16m;
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));
            }
        }

        private void SubtotalTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_subtotalValue > 0)
            {
                SubtotalTextBox.Text = _subtotalValue.ToString("F2");
            }
            SubtotalTextBox.SelectAll();
        }

        private void SubtotalTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string cleanText = SubtotalTextBox.Text
                .Replace("$", "")
                .Replace(",", "")
                .Replace(" ", "")
                .Trim();

            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
            {
                _subtotalValue = subtotal;
                SubtotalTextBox.Text = subtotal.ToString("C", new CultureInfo("es-MX"));

                decimal total = _subtotalValue * 1.16m;
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));
            }
            else if (_subtotalValue > 0)
            {
                SubtotalTextBox.Text = _subtotalValue.ToString("C", new CultureInfo("es-MX"));
            }
            else
            {
                _subtotalValue = 0;
                SubtotalTextBox.Text = "$0.00";
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

        private void UpdateSaveStatus()
        {
            if (_hasChanges)
            {
                SaveStatusText.Text = "⚠ Hay cambios sin guardar";
                SaveStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar campos obligatorios
                if (!ValidateForm()) return;

                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // Preparar la orden actualizada
                if (_currentUser.Role == "admin")
                {
                    // Admin puede actualizar todos los campos (excepto fecha OC)
                    _originalOrderDb.Po = EditableOrderNumberTextBox.Text.Trim().ToUpper();
                    _originalOrderDb.Quote = EditableQuotationTextBox.Text?.Trim().ToUpper();
                    _originalOrderDb.ClientId = (int?)EditableClientComboBox.SelectedValue;
                    _originalOrderDb.ContactId = (int?)EditableContactComboBox.SelectedValue;
                    _originalOrderDb.SalesmanId = (int?)EditableVendorComboBox.SelectedValue;
                    _originalOrderDb.Description = EditableDescriptionTextBox.Text?.Trim();
                    _originalOrderDb.SaleSubtotal = _subtotalValue;
                    _originalOrderDb.SaleTotal = _subtotalValue * 1.16m;
                }

                // Campos que ambos roles pueden editar (según estado)
                _originalOrderDb.EstDelivery = PromiseDatePicker.SelectedDate.Value;
                _originalOrderDb.ProgressPercentage = (int)ProgressSlider.Value;

                var selectedStatus = StatusComboBox.SelectedItem as ComboBoxItem;
                if (selectedStatus?.Tag is int statusId)
                {
                    _originalOrderDb.OrderStatus = statusId;
                }

                // Guardar en BD
                bool success = await _supabaseService.UpdateOrder(_originalOrderDb, _currentUser.Id);

                if (success)
                {
                    // Registrar cambio de estado en historial si cambió
                    if (selectedStatus?.Tag is int newStatusId && newStatusId != _currentStatusId)
                    {
                        var oldStatusName = await _supabaseService.GetStatusName(_currentStatusId);
                        var newStatusName = await _supabaseService.GetStatusName(newStatusId);

                        await _supabaseService.LogOrderHistory(
                            _order.Id,
                            _currentUser.Id,
                            "STATUS_CHANGE",
                            "f_orderstat",
                            oldStatusName,
                            newStatusName,
                            $"Cambio manual de estado por {_currentUser.FullName}"
                        );
                    }

                    // Verificar si se debe actualizar automáticamente el estado
                    await _supabaseService.CheckAndUpdateOrderStatus(_order.Id, _currentUser.Id);

                    // Actualizar el objeto local
                    if (_currentUser.Role == "admin")
                    {
                        _order.OrderNumber = _originalOrderDb.Po;
                        _order.Subtotal = _originalOrderDb.SaleSubtotal ?? 0;
                        _order.Total = _originalOrderDb.SaleTotal ?? 0;
                    }

                    _order.PromiseDate = _originalOrderDb.EstDelivery.Value;
                    _order.ProgressPercentage = _originalOrderDb.ProgressPercentage;

                    var statusName = _orderStatuses.FirstOrDefault(s => s.Id == _originalOrderDb.OrderStatus)?.Name;
                    _order.Status = statusName ?? "PENDIENTE";

                    

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

        private bool ValidateForm()
        {
            if (_currentUser.Role == "admin")
            {
                if (string.IsNullOrWhiteSpace(EditableOrderNumberTextBox.Text))
                {
                    MessageBox.Show("La Orden de Compra es obligatoria", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (EditableClientComboBox.SelectedValue == null)
                {
                    MessageBox.Show("El Cliente es obligatorio", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(EditableDescriptionTextBox.Text))
                {
                    MessageBox.Show("La Descripción es obligatoria", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (_subtotalValue <= 0)
                {
                    MessageBox.Show("El Subtotal debe ser mayor a 0", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            if (!PromiseDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("La Fecha Promesa es obligatoria", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (StatusComboBox.SelectedItem == null)
            {
                MessageBox.Show("El Estatus es obligatorio", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            

            this.DialogResult = false;
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            

            base.OnClosing(e);
        }

        private void CancelOrderButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevenir múltiples ejecuciones
            if (_isCancellingFromButton) return;

            // Primero verificar si la orden ya está cancelada
            var currentStatus = _orderStatuses?.FirstOrDefault(s => s.Id == _currentStatusId);
            if (currentStatus?.Name?.ToUpper() == "CANCELADA")
            {
                MessageBox.Show(
                    "Esta orden ya está cancelada.",
                    "Información",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Verificar si el estado actual permite cancelación
            if (_currentStatusId > 1)
            {
                MessageBox.Show(
                    "Solo se pueden cancelar órdenes en estado CREADA o EN PROCESO.\n" +
                    "Esta orden ya está en un estado avanzado.",
                    "No se puede cancelar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Marcar que estamos cancelando desde el botón
            _isCancellingFromButton = true;

            // Llamar directamente a HandleCancelOrder sin cambiar el ComboBox
            HandleCancelOrder();

            // Resetear la bandera
            _isCancellingFromButton = false;
        }
    }
}