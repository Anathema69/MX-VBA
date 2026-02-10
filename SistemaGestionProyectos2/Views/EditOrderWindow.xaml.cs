using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private bool _isCancellingFromButton = false;

        // Gastos Operativos v2.0 - Manejo local hasta guardar
        private ObservableCollection<OrderGastoOperativoDb> _gastosOperativos;
        private List<OrderGastoOperativoDb> _gastosOriginales; // Copia original para comparar
        private List<int> _gastosEliminados = new List<int>(); // IDs de gastos a eliminar
        private int _tempIdCounter = -1; // IDs temporales negativos para nuevos gastos

        // Gastos Indirectos v2.1 - Manejo local hasta guardar
        private ObservableCollection<OrderGastoIndirectoDb> _gastosIndirectos;
        private List<OrderGastoIndirectoDb> _gastosIndirectosOriginales;
        private List<int> _gastosIndirectosEliminados = new List<int>();
        private int _tempIdCounterIndirecto = -1;
        private Border _currentEditingRowIndirecto = null;


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
                if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
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

                // Variables para capturar resultados paralelos
                List<OrderGastoOperativoDb> gastos = null;
                List<OrderGastoIndirectoDb> gastosIndirectos = null;

                // Cargar todos los datos necesarios en paralelo (incluyendo orden y gastos)
                var loadTasks = new List<Task>
                {
                    Task.Run(async () => _orderStatuses = await _supabaseService.GetOrderStatuses()),
                    Task.Run(async () => _clients = await _supabaseService.GetClients()),
                    Task.Run(async () => _vendors = await _supabaseService.GetVendors()),
                    Task.Run(async () => _originalOrderDb = await _supabaseService.GetOrderById(_order.Id)),
                    Task.Run(async () => {
                        if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
                        {
                            gastos = await _supabaseService.GetGastosOperativos(_order.Id);
                            gastosIndirectos = await _supabaseService.GetGastosIndirectos(_order.Id);
                        }
                    })
                };

                await Task.WhenAll(loadTasks);

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

                // Cargar contactos del cliente actual si existe (depende de _originalOrderDb)
                if (_originalOrderDb.ClientId.HasValue)
                {
                    _contacts = await _supabaseService.GetContactsByClient(_originalOrderDb.ClientId.Value);
                }

                // Asignar gastos operativos y guardar copia original
                if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
                {
                    var listaGastos = gastos ?? new List<OrderGastoOperativoDb>();
                    _gastosOperativos = new ObservableCollection<OrderGastoOperativoDb>(listaGastos);
                    // Guardar copia original para comparar al guardar
                    _gastosOriginales = listaGastos.Select(g => new OrderGastoOperativoDb
                    {
                        Id = g.Id,
                        OrderId = g.OrderId,
                        Monto = g.Monto,
                        Descripcion = g.Descripcion,
                        Categoria = g.Categoria,
                        FechaGasto = g.FechaGasto,
                        CreatedBy = g.CreatedBy
                    }).ToList();
                    _gastosEliminados = new List<int>();

                    // Asignar gastos indirectos y guardar copia original
                    var listaGastosIndirectos = gastosIndirectos ?? new List<OrderGastoIndirectoDb>();
                    _gastosIndirectos = new ObservableCollection<OrderGastoIndirectoDb>(listaGastosIndirectos);
                    _gastosIndirectosOriginales = listaGastosIndirectos.Select(g => new OrderGastoIndirectoDb
                    {
                        Id = g.Id,
                        OrderId = g.OrderId,
                        Monto = g.Monto,
                        Descripcion = g.Descripcion,
                        FechaGasto = g.FechaGasto,
                        CreatedBy = g.CreatedBy
                    }).ToList();
                    _gastosIndirectosEliminados = new List<int>();
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

            // Si es admin (direccion o administracion), configurar controles adicionales
            if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
            {
                // Llenar combo de clientes
                EditableClientComboBox.ItemsSource = _clients;
                EditableClientComboBox.DisplayMemberPath = "Name";
                EditableClientComboBox.SelectedValuePath = "Id";
                EditableClientComboBox.SelectionChanged += EditableClientComboBox_SelectionChanged;

                // Llenar combo de vendedores con opción "Sin vendedor"
                var vendorsList = new List<VendorDb>
                {
                    new VendorDb { Id = 0, VendorName = "— Sin vendedor —" }
                };
                if (_vendors != null)
                {
                    vendorsList.AddRange(_vendors);
                }
                EditableVendorComboBox.ItemsSource = vendorsList;
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

            // Roles v2.0: direccion, administracion, proyectos, coordinacion, ventas
            if (_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos")
            {
                PermissionsText.Text = "Como Coordinador, puede editar: Fecha Promesa, % Avance y Estatus";

                // Ocultar todos los campos de admin
                OrderNumberEditPanel.Visibility = Visibility.Collapsed;
                FechaCompraEditPanel.Visibility = Visibility.Collapsed;

                AdminFieldsPanel1.Visibility = Visibility.Collapsed;
                AdminFieldsPanel2.Visibility = Visibility.Collapsed;
                AdminDescriptionPanel.Visibility = Visibility.Collapsed;
                FinancialSection.Visibility = Visibility.Collapsed;
                FinancialFields.Visibility = Visibility.Collapsed;
                // Gastos v2.0 - ocultos para coordinación
                GastosSection.Visibility = Visibility.Collapsed;
                GastosFields.Visibility = Visibility.Collapsed;

                // Campos de solo lectura mantienen su visibilidad normal
                OrderNumberTextBox.IsReadOnly = true;
                OrderNumberTextBox.Background = System.Windows.Media.Brushes.LightGray;
            }
            else if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
            {
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

                // Gastos v2.0 - visibles para direccion y administracion
                if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
                {
                    GastosSection.Visibility = Visibility.Visible;
                    GastosFields.Visibility = Visibility.Visible;
                }
                else
                {
                    GastosSection.Visibility = Visibility.Collapsed;
                    GastosFields.Visibility = Visibility.Collapsed;
                }

                // Ocultar campos de solo lectura que ahora son editables
                // (excepto fecha que nunca es editable)
            }
            else
            {
                MessageBox.Show("No tiene permisos para editar órdenes", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
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


            if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
            {



                // Admin: cargar en campos editables
                EditableOrderNumberTextBox.Text = _order.OrderNumber;
                EditableQuotationTextBox.Text = _originalOrderDb?.Quote ?? "";
                EditableClientComboBox.SelectedValue = _originalOrderDb?.ClientId;
                EditableContactComboBox.SelectedValue = _originalOrderDb?.ContactId;
                // Si no tiene vendedor (null), seleccionar "Sin vendedor" (Id=0)
                EditableVendorComboBox.SelectedValue = _originalOrderDb?.SalesmanId ?? 0;
                EditableDescriptionTextBox.Text = _order.Description;

                

                // Campos financieros
                _subtotalValue = _order.Subtotal;
                SubtotalTextBox.Text = _subtotalValue.ToString("C", new CultureInfo("es-MX"));
                TotalTextBlock.Text = _order.Total.ToString("C", new CultureInfo("es-MX"));

                // Cargar lista de gastos operativos v2.0
                LoadGastosOperativosUI();
                // Cargar lista de gastos indirectos v2.1
                LoadGastosIndirectosUI();
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

            if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
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
            else
            {
                // ✅ Ejecutar la cancelación
                ExecuteCancelOrder();
            }
        }

        private async void ExecuteCancelOrder()
        {
            try
            {
                // Mostrar indicador de carga
                CancelOrderButton.IsEnabled = false;
                CancelOrderButton.Content = "CANCELANDO...";

                // Ejecutar cancelación en la BD
                var success = await _supabaseService.CancelOrder(_order.Id);

                if (success)
                {
                    MessageBox.Show(
                        $"La orden {_order.OrderNumber} ha sido cancelada exitosamente.",
                        "Orden Cancelada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Cerrar ventana con resultado positivo para refrescar la lista
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        "No se pudo cancelar la orden. Por favor intente nuevamente.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Revertir el ComboBox
                    SelectComboBoxItemByTag(StatusComboBox, _currentStatusId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cancelar la orden:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                SelectComboBoxItemByTag(StatusComboBox, _currentStatusId);
            }
            finally
            {
                CancelOrderButton.IsEnabled = true;
                CancelOrderButton.Content = "CANCELAR ORDEN";
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

        // Solo números enteros (para DiasEstimados)
        private void NumericOnlyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex(@"^[0-9]+$");
            e.Handled = !regex.IsMatch(e.Text);
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
            // Roles v2.0
            switch (role)
            {
                case "direccion": return "Dirección";
                case "administracion": return "Administración";
                case "coordinacion": return "Coordinación";
                case "proyectos": return "Proyectos";
                case "ventas": return "Vendedor";
                default: return role;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar campos obligatorios
                if (!ValidateForm()) return;

                // Auto-commit ediciones inline pendientes antes de guardar
                if (_currentEditingRow != null)
                {
                    var pendingGrid = _currentEditingRow.Child as Grid;
                    if (pendingGrid != null)
                    {
                        var pMontoEdit = pendingGrid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MontoEdit");
                        var pDescEdit = pendingGrid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEdit");
                        var pMontoView = pendingGrid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "MontoView");
                        var pDescView = pendingGrid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "DescView");
                        var pEditBtn = pendingGrid.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtn");
                        var pDeleteBtn = pendingGrid.Children.OfType<Button>().FirstOrDefault(t => t.Name == "DeleteBtn");

                        if (pMontoEdit != null && pDescEdit != null)
                        {
                            SaveInlineEdit(_currentEditingRow, pMontoEdit, pDescEdit, pMontoView, pDescView, pEditBtn, pDeleteBtn);
                        }
                    }
                }
                if (_currentEditingRowIndirecto != null)
                {
                    SaveInlineEditIndirecto(_currentEditingRowIndirecto);
                }

                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // Preparar la orden actualizada
                if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
                {
                    // Admin puede actualizar todos los campos (excepto fecha OC)
                    _originalOrderDb.Po = EditableOrderNumberTextBox.Text.Trim().ToUpper();
                    _originalOrderDb.Quote = EditableQuotationTextBox.Text?.Trim().ToUpper();
                    _originalOrderDb.ClientId = (int?)EditableClientComboBox.SelectedValue;
                    _originalOrderDb.ContactId = (int?)EditableContactComboBox.SelectedValue;
                    // Si se selecciona "Sin vendedor" (Id=0), guardar como null
                    var selectedVendorId = (int?)EditableVendorComboBox.SelectedValue;
                    _originalOrderDb.SalesmanId = (selectedVendorId == 0) ? null : selectedVendorId;
                    _originalOrderDb.Description = EditableDescriptionTextBox.Text?.Trim();
                    _originalOrderDb.SaleSubtotal = _subtotalValue;
                    _originalOrderDb.SaleTotal = _subtotalValue * 1.16m;

                    // Campos de gastos v2.0
                    // GastoOperativo: suma de gastos base (el trigger en BD suma + commission_amount)
                    _originalOrderDb.GastoOperativo = _gastosOperativos?.Sum(g => g.Monto) ?? 0;
                    // GastoIndirecto se calcula automáticamente desde order_gastos_indirectos
                    _originalOrderDb.GastoIndirecto = _gastosIndirectos?.Sum(g => g.Monto) ?? 0;
                }

                // Campos que ambos roles pueden editar (según estado)
                _originalOrderDb.EstDelivery = PromiseDatePicker.SelectedDate.Value;
                // ProgressPercentage = Avance del TRABAJO (editable manualmente)
                // NO confundir con OrderPercentage que es el % de facturación (automático)
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
                    // Persistir cambios de gastos operativos (direccion y administracion)
                    if ((_currentUser.Role == "direccion" || _currentUser.Role == "administracion") && _gastosOperativos != null)
                    {
                        await PersistGastosOperativosAsync();
                    }

                    // Persistir cambios de gastos indirectos (direccion y administracion)
                    if ((_currentUser.Role == "direccion" || _currentUser.Role == "administracion") && _gastosIndirectos != null)
                    {
                        await PersistGastosIndirectosAsync();
                    }

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
                    if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
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
            if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
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

        #region Gastos Operativos v2.0

        /// <summary>
        /// Persiste los cambios de gastos operativos a la BD
        /// Solo se llama cuando el usuario hace clic en "GUARDAR CAMBIOS"
        /// </summary>
        private async Task PersistGastosOperativosAsync()
        {
            try
            {
                int insertados = 0, actualizados = 0, eliminados = 0;

                // 1. Eliminar gastos marcados para eliminar
                foreach (var gastoId in _gastosEliminados)
                {
                    System.Diagnostics.Debug.WriteLine($"🗑 Eliminando gasto ID: {gastoId}");
                    await _supabaseService.DeleteGastoOperativo(gastoId, _order.Id, _currentUser.Id);
                    eliminados++;
                }

                // 2. Procesar gastos en la lista actual
                foreach (var gasto in _gastosOperativos)
                {
                    if (gasto.Id < 0)
                    {
                        // Gasto nuevo (ID temporal negativo) - Insertar
                        System.Diagnostics.Debug.WriteLine($"➕ Insertando gasto: {gasto.Monto:C} - {gasto.Descripcion}");
                        var resultado = await _supabaseService.AddGastoOperativo(_order.Id, gasto.Monto, gasto.Descripcion, _currentUser.Id);
                        if (resultado != null)
                        {
                            insertados++;
                            System.Diagnostics.Debug.WriteLine($"✅ Gasto insertado con ID: {resultado.Id}");
                        }
                    }
                    else
                    {
                        // Gasto existente - Verificar si fue modificado
                        var original = _gastosOriginales?.FirstOrDefault(g => g.Id == gasto.Id);
                        if (original != null && (original.Monto != gasto.Monto || original.Descripcion != gasto.Descripcion))
                        {
                            System.Diagnostics.Debug.WriteLine($"✏ Actualizando gasto ID: {gasto.Id}");
                            await _supabaseService.UpdateGastoOperativo(gasto.Id, gasto.Monto, gasto.Descripcion, _order.Id, _currentUser.Id);
                            actualizados++;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Gastos operativos guardados: {insertados} insertados, {actualizados} actualizados, {eliminados} eliminados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error guardando gastos operativos: {ex.Message}");
                MessageBox.Show($"Error al guardar gastos operativos:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void LoadGastosOperativosUI()
        {
            if (_gastosOperativos == null)
            {
                _gastosOperativos = new ObservableCollection<OrderGastoOperativoDb>();
            }

            GastosItemsControl.ItemsSource = _gastosOperativos;

            // Mostrar/ocultar mensaje de "sin gastos"
            NoGastosMessage.Visibility = _gastosOperativos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Actualizar total
            UpdateGastoOperativoTotal();
        }

        private void UpdateGastoOperativoTotal()
        {
            var culture = new CultureInfo("es-MX");
            decimal subtotal = _gastosOperativos?.Sum(g => g.Monto) ?? 0;
            GastoOperativoText.Text = subtotal.ToString("C", culture);

            // Mostrar info de comisión del vendedor si aplica
            decimal rate = _originalOrderDb?.CommissionRate ?? 0;
            if (rate > 0 && _originalOrderDb?.SaleSubtotal > 0)
            {
                decimal comision = (_originalOrderDb.SaleSubtotal ?? 0) * rate / 100;
                decimal totalConComision = subtotal + comision;
                ComisionInfoText.Text = $"+ Comisión vendedor: {comision.ToString("C", culture)}  |  Gasto operativo: {totalConComision.ToString("C", culture)}";
                ComisionInfoText.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                ComisionInfoText.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void AddGastoButton_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar fila para nuevo gasto
            _gastoMontoValue = 0;
            NewGastoRow.Visibility = Visibility.Visible;
            NewGastoMontoTextBox.Text = "";
            NewGastoDescripcionTextBox.Text = "";
            NewGastoMontoTextBox.Focus();
        }

        private void CancelNewGastoButton_Click(object sender, RoutedEventArgs e)
        {
            // Ocultar fila de nuevo gasto
            NewGastoRow.Visibility = Visibility.Collapsed;
            NewGastoMontoTextBox.Text = "";
            NewGastoDescripcionTextBox.Text = "";
            _gastoMontoValue = 0;
        }

        private decimal _gastoMontoValue = 0;

        private void NewGastoMontoTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Mostrar valor numérico sin formato al editar
            if (_gastoMontoValue > 0)
            {
                NewGastoMontoTextBox.Text = _gastoMontoValue.ToString("F2");
            }
            else
            {
                string cleanText = NewGastoMontoTextBox.Text.Replace("$", "").Replace(",", "").Trim();
                if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
                {
                    _gastoMontoValue = value;
                    NewGastoMontoTextBox.Text = value.ToString("F2");
                }
            }
            NewGastoMontoTextBox.SelectAll();
        }

        private void NewGastoMontoTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string cleanText = NewGastoMontoTextBox.Text.Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
            {
                _gastoMontoValue = value;
                NewGastoMontoTextBox.Text = value.ToString("C", new CultureInfo("es-MX"));
            }
            else if (_gastoMontoValue > 0)
            {
                NewGastoMontoTextBox.Text = _gastoMontoValue.ToString("C", new CultureInfo("es-MX"));
            }
            else
            {
                _gastoMontoValue = 0;
                NewGastoMontoTextBox.Text = "";
            }
        }

        private void NewGastoDescripcionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveNewGastoButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelNewGastoButton_Click(sender, e);
            }
        }

        private void NewGastoMontoTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Mover foco a descripción o guardar si descripción tiene texto
                if (!string.IsNullOrWhiteSpace(NewGastoDescripcionTextBox.Text))
                {
                    SaveNewGastoButton_Click(sender, e);
                }
                else
                {
                    NewGastoDescripcionTextBox.Focus();
                }
            }
            else if (e.Key == Key.Escape)
            {
                CancelNewGastoButton_Click(sender, e);
            }
        }

        private void SaveNewGastoButton_Click(object sender, RoutedEventArgs e)
        {
            string montoText = NewGastoMontoTextBox.Text.Replace("$", "").Replace(",", "").Trim();
            string descripcion = NewGastoDescripcionTextBox.Text.Trim();

            // Validar
            if (string.IsNullOrWhiteSpace(montoText) || string.IsNullOrWhiteSpace(descripcion))
            {
                MessageBox.Show("Monto y Descripción son obligatorios", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(montoText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("El monto debe ser un número mayor a 0", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                NewGastoMontoTextBox.Focus();
                return;
            }

            // Llamar al método interno que maneja tanto nuevo como edición
            SaveNewGastoButton_Click_Internal(monto, descripcion);
        }

        private void DeleteGastoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            // Verificar si estamos en modo edición (botón muestra ✕)
            var border = FindParent<Border>(button);
            if (border != null && _currentEditingRow == border)
            {
                // Estamos editando - cancelar edición
                CancelInlineEdit(border);
                return;
            }

            int gastoId = (int)button.Tag;
            var gasto = _gastosOperativos.FirstOrDefault(g => g.Id == gastoId);

            if (gasto == null) return;

            // Solo marcar para eliminar en BD si es un gasto existente (ID positivo)
            if (gastoId > 0)
            {
                _gastosEliminados.Add(gastoId);
            }

            // Remover de la lista local
            _gastosOperativos.Remove(gasto);
            LoadGastosOperativosUI();
            _hasChanges = true;
            UpdateSaveStatus();
        }

        private Border _currentEditingRow = null;

        private void EditGastoInlineButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // Encontrar el Border padre (la fila)
            var border = FindParent<Border>(button);
            if (border == null) return;

            var grid = border.Child as Grid;
            if (grid == null) return;

            // Obtener controles
            var montoView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "MontoView");
            var descView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "DescView");
            var montoEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MontoEdit");
            var descEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEdit");
            var editBtn = grid.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtn");
            var deleteBtn = grid.Children.OfType<Button>().FirstOrDefault(t => t.Name == "DeleteBtn");

            if (montoEdit == null || descEdit == null) return;

            // Verificar si estamos en modo edición o vista
            bool isEditing = montoEdit.Visibility == Visibility.Visible;

            if (isEditing)
            {
                // Modo edición -> Guardar y volver a vista
                SaveInlineEdit(border, montoEdit, descEdit, montoView, descView, editBtn, deleteBtn);
            }
            else
            {
                // Cancelar edición anterior si existe
                if (_currentEditingRow != null && _currentEditingRow != border)
                {
                    CancelInlineEdit(_currentEditingRow);
                }

                // Modo vista -> Cambiar a edición
                montoView.Visibility = Visibility.Collapsed;
                descView.Visibility = Visibility.Collapsed;
                montoEdit.Visibility = Visibility.Visible;
                descEdit.Visibility = Visibility.Visible;
                editBtn.Content = "✓";
                editBtn.Foreground = System.Windows.Media.Brushes.Green;
                editBtn.ToolTip = "Guardar";
                deleteBtn.Content = "✕";
                deleteBtn.Foreground = System.Windows.Media.Brushes.Gray;
                deleteBtn.ToolTip = "Cancelar";

                _currentEditingRow = border;

                montoEdit.Focus();
                montoEdit.SelectAll();
            }
        }

        private void SaveInlineEdit(Border border, TextBox montoEdit, TextBox descEdit,
            TextBlock montoView, TextBlock descView, Button editBtn, Button deleteBtn)
        {
            // Validar y obtener valores
            string montoText = montoEdit.Text.Replace("$", "").Replace(",", "").Trim();
            string descripcion = descEdit.Text.Trim();

            if (!decimal.TryParse(montoText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("El monto debe ser un número mayor a 0", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                montoEdit.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                MessageBox.Show("La descripción es obligatoria", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                descEdit.Focus();
                return;
            }

            // Actualizar el objeto en memoria
            var gasto = border.DataContext as OrderGastoOperativoDb;
            if (gasto != null)
            {
                gasto.Monto = monto;
                gasto.Descripcion = descripcion;
            }

            // Volver a modo vista
            montoView.Text = $"${monto:N2}";
            descView.Text = descripcion;
            montoView.Visibility = Visibility.Visible;
            descView.Visibility = Visibility.Visible;
            montoEdit.Visibility = Visibility.Collapsed;
            descEdit.Visibility = Visibility.Collapsed;
            editBtn.Content = "✏";
            editBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2"));
            editBtn.ToolTip = "Editar";
            deleteBtn.Content = "🗑";
            deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935"));
            deleteBtn.ToolTip = "Eliminar";

            _currentEditingRow = null;
            _hasChanges = true;
            UpdateSaveStatus();
            UpdateGastoOperativoTotal();
        }

        private void CancelInlineEdit(Border border)
        {
            if (border == null) return;

            var grid = border.Child as Grid;
            if (grid == null) return;

            var montoView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "MontoView");
            var descView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "DescView");
            var montoEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MontoEdit");
            var descEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEdit");
            var editBtn = grid.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtn");
            var deleteBtn = grid.Children.OfType<Button>().FirstOrDefault(t => t.Name == "DeleteBtn");

            if (montoEdit == null) return;

            // Restaurar valores originales del objeto
            var gasto = border.DataContext as OrderGastoOperativoDb;
            if (gasto != null)
            {
                montoEdit.Text = gasto.Monto.ToString("N2");
                descEdit.Text = gasto.Descripcion;
            }

            // Volver a modo vista
            if (montoView != null) montoView.Visibility = Visibility.Visible;
            if (descView != null) descView.Visibility = Visibility.Visible;
            montoEdit.Visibility = Visibility.Collapsed;
            if (descEdit != null) descEdit.Visibility = Visibility.Collapsed;
            if (editBtn != null)
            {
                editBtn.Content = "✏";
                editBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2"));
                editBtn.ToolTip = "Editar";
            }
            if (deleteBtn != null)
            {
                deleteBtn.Content = "🗑";
                deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E53935"));
                deleteBtn.ToolTip = "Eliminar";
            }

            _currentEditingRow = null;
        }

        private void GastoDescEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                if (border != null)
                {
                    var grid = border.Child as Grid;
                    var editBtn = grid?.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtn");
                    editBtn?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                CancelInlineEdit(border);
            }
        }

        private void GastoMontoEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                if (border != null)
                {
                    var grid = border.Child as Grid;
                    var descEdit = grid?.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEdit");

                    // Si descripción tiene texto, guardar; si no, mover foco a descripción
                    if (descEdit != null && !string.IsNullOrWhiteSpace(descEdit.Text))
                    {
                        var editBtn = grid?.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtn");
                        editBtn?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    else
                    {
                        descEdit?.Focus();
                    }
                }
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                CancelInlineEdit(border);
            }
        }

        private void GastoMontoEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            // Formatear como moneda al perder el foco
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string cleanText = textBox.Text.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
            {
                textBox.Text = value.ToString("N2");
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private void SaveNewGastoButton_Click_Internal(decimal monto, string descripcion)
        {
            var nuevoGasto = new OrderGastoOperativoDb
            {
                Id = _tempIdCounter--, // ID temporal negativo
                OrderId = _order.Id,
                Monto = monto,
                Descripcion = descripcion,
                FechaGasto = DateTime.Now,
                CreatedBy = _currentUser.Id
            };

            _gastosOperativos.Insert(0, nuevoGasto);
            LoadGastosOperativosUI();
            _hasChanges = true;
            UpdateSaveStatus();

            // Limpiar campos y preparar para otro gasto
            NewGastoMontoTextBox.Text = "";
            NewGastoDescripcionTextBox.Text = "";
            _gastoMontoValue = 0;
            NewGastoMontoTextBox.Focus();
        }

        #endregion

        #region Gastos Indirectos v2.1

        /// <summary>
        /// Persiste los cambios de gastos indirectos a la BD
        /// Solo se llama cuando el usuario hace clic en "GUARDAR CAMBIOS"
        /// </summary>
        private async Task PersistGastosIndirectosAsync()
        {
            try
            {
                int insertados = 0, actualizados = 0, eliminados = 0;

                // 1. Eliminar gastos marcados para eliminar
                foreach (var gastoId in _gastosIndirectosEliminados)
                {
                    System.Diagnostics.Debug.WriteLine($"🗑 Eliminando gasto indirecto ID: {gastoId}");
                    await _supabaseService.DeleteGastoIndirecto(gastoId, _order.Id, _currentUser.Id);
                    eliminados++;
                }

                // 2. Procesar gastos en la lista actual
                foreach (var gasto in _gastosIndirectos)
                {
                    if (gasto.Id < 0)
                    {
                        // Gasto nuevo (ID temporal negativo) - Insertar
                        System.Diagnostics.Debug.WriteLine($"➕ Insertando gasto indirecto: {gasto.Monto:C} - {gasto.Descripcion}");
                        var resultado = await _supabaseService.AddGastoIndirecto(_order.Id, gasto.Monto, gasto.Descripcion, _currentUser.Id);
                        if (resultado != null)
                        {
                            insertados++;
                            System.Diagnostics.Debug.WriteLine($"✅ Gasto indirecto insertado con ID: {resultado.Id}");
                        }
                    }
                    else
                    {
                        // Gasto existente - Verificar si fue modificado
                        var original = _gastosIndirectosOriginales?.FirstOrDefault(g => g.Id == gasto.Id);
                        if (original != null && (original.Monto != gasto.Monto || original.Descripcion != gasto.Descripcion))
                        {
                            System.Diagnostics.Debug.WriteLine($"✏ Actualizando gasto indirecto ID: {gasto.Id}");
                            await _supabaseService.UpdateGastoIndirecto(gasto.Id, gasto.Monto, gasto.Descripcion, _order.Id, _currentUser.Id);
                            actualizados++;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Gastos indirectos guardados: {insertados} insertados, {actualizados} actualizados, {eliminados} eliminados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error guardando gastos indirectos: {ex.Message}");
                MessageBox.Show($"Error al guardar gastos indirectos:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void LoadGastosIndirectosUI()
        {
            if (_gastosIndirectos == null)
            {
                _gastosIndirectos = new ObservableCollection<OrderGastoIndirectoDb>();
            }

            GastosIndirectosItemsControl.ItemsSource = _gastosIndirectos;

            // Mostrar/ocultar mensaje de "sin gastos"
            NoGastosIndirectosMessage.Visibility = _gastosIndirectos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Actualizar total
            UpdateGastoIndirectoTotal();
        }

        private void UpdateGastoIndirectoTotal()
        {
            decimal total = _gastosIndirectos?.Sum(g => g.Monto) ?? 0;
            GastoIndirectoText.Text = $"${total:N2}";
        }

        private decimal _gastoIndirectoMontoValue = 0;

        private void AddGastoIndirectoButton_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar fila para nuevo gasto
            _gastoIndirectoMontoValue = 0;
            NewGastoIndirectoRow.Visibility = Visibility.Visible;
            NewGastoIndirectoMontoTextBox.Text = "";
            NewGastoIndirectoDescripcionTextBox.Text = "";
            NewGastoIndirectoMontoTextBox.Focus();
        }

        private void CancelNewGastoIndirectoButton_Click(object sender, RoutedEventArgs e)
        {
            // Ocultar fila de nuevo gasto
            NewGastoIndirectoRow.Visibility = Visibility.Collapsed;
            NewGastoIndirectoMontoTextBox.Text = "";
            NewGastoIndirectoDescripcionTextBox.Text = "";
            _gastoIndirectoMontoValue = 0;
        }

        private void NewGastoIndirectoMontoTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Mostrar valor numérico sin formato al editar
            if (_gastoIndirectoMontoValue > 0)
            {
                NewGastoIndirectoMontoTextBox.Text = _gastoIndirectoMontoValue.ToString("F2");
            }
            else
            {
                string cleanText = NewGastoIndirectoMontoTextBox.Text.Replace("$", "").Replace(",", "").Trim();
                if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
                {
                    _gastoIndirectoMontoValue = value;
                    NewGastoIndirectoMontoTextBox.Text = value.ToString("F2");
                }
            }
            NewGastoIndirectoMontoTextBox.SelectAll();
        }

        private void NewGastoIndirectoMontoTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Capturar valor y formatear
            string cleanText = NewGastoIndirectoMontoTextBox.Text.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
            {
                _gastoIndirectoMontoValue = value;
                NewGastoIndirectoMontoTextBox.Text = $"${value:N2}";
            }
        }

        private void NewGastoIndirectoMontoTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Mover al campo descripción
                NewGastoIndirectoDescripcionTextBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelNewGastoIndirectoButton_Click(sender, e);
            }
        }

        private void NewGastoIndirectoDescripcionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveNewGastoIndirectoButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelNewGastoIndirectoButton_Click(sender, e);
            }
        }

        private void SaveNewGastoIndirectoButton_Click(object sender, RoutedEventArgs e)
        {
            // Capturar valor del monto
            string cleanText = NewGastoIndirectoMontoTextBox.Text.Replace("$", "").Replace(",", "").Trim();
            if (!decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Ingrese un monto válido", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                NewGastoIndirectoMontoTextBox.Focus();
                return;
            }

            string descripcion = NewGastoIndirectoDescripcionTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                MessageBox.Show("Ingrese una descripción", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                NewGastoIndirectoDescripcionTextBox.Focus();
                return;
            }

            // Agregar nuevo gasto solo en memoria
            var nuevoGasto = new OrderGastoIndirectoDb
            {
                Id = _tempIdCounterIndirecto--,
                OrderId = _order.Id,
                Monto = monto,
                Descripcion = descripcion,
                FechaGasto = DateTime.Now,
                CreatedBy = _currentUser.Id
            };

            _gastosIndirectos.Insert(0, nuevoGasto);
            LoadGastosIndirectosUI();
            _hasChanges = true;
            UpdateSaveStatus();

            // Limpiar campos y preparar para otro gasto
            NewGastoIndirectoMontoTextBox.Text = "";
            NewGastoIndirectoDescripcionTextBox.Text = "";
            _gastoIndirectoMontoValue = 0;
            NewGastoIndirectoMontoTextBox.Focus();
        }

        private void DeleteGastoIndirectoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            // Verificar si estamos en modo edición (botón muestra ✕)
            var border = FindParent<Border>(button);
            if (border != null && _currentEditingRowIndirecto == border)
            {
                // Estamos editando - cancelar edición
                CancelInlineEditIndirecto(border);
                return;
            }

            int gastoId = Convert.ToInt32(button.Tag);
            var gasto = _gastosIndirectos.FirstOrDefault(g => g.Id == gastoId);

            if (gasto == null) return;

            // Si es un gasto existente (ID positivo), marcarlo para eliminar
            if (gastoId > 0)
            {
                _gastosIndirectosEliminados.Add(gastoId);
            }

            // Remover de la colección local
            _gastosIndirectos.Remove(gasto);
            LoadGastosIndirectosUI();
            _hasChanges = true;
            UpdateSaveStatus();
        }

        private void EditGastoIndirectoInlineButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            if (border == null) return;

            var grid = border.Child as Grid;
            if (grid == null) return;

            // Encontrar elementos
            var montoView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "MontoViewInd");
            var descView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "DescViewInd");
            var montoEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MontoEditInd");
            var descEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEditInd");
            var editBtn = grid.Children.OfType<Button>().FirstOrDefault(b => b.Name == "EditBtnInd");
            var deleteBtn = grid.Children.OfType<Button>().FirstOrDefault(b => b.Name == "DeleteBtnInd");

            // Verificar si estamos en modo edición
            bool isEditing = montoEdit?.Visibility == Visibility.Visible;

            if (isEditing)
            {
                // Guardar cambios
                SaveInlineEditIndirecto(border);
            }
            else
            {
                // Si hay otra fila en edición, cancelarla primero
                if (_currentEditingRowIndirecto != null && _currentEditingRowIndirecto != border)
                {
                    CancelInlineEditIndirecto(_currentEditingRowIndirecto);
                }

                // Entrar a modo edición
                if (montoView != null) montoView.Visibility = Visibility.Collapsed;
                if (descView != null) descView.Visibility = Visibility.Collapsed;
                if (montoEdit != null)
                {
                    montoEdit.Visibility = Visibility.Visible;
                    // Limpiar formato de moneda para edición
                    var gasto = border.DataContext as OrderGastoIndirectoDb;
                    if (gasto != null)
                    {
                        montoEdit.Text = gasto.Monto.ToString("N2");
                    }
                }
                if (descEdit != null) descEdit.Visibility = Visibility.Visible;
                if (editBtn != null) editBtn.Content = "✓";
                if (deleteBtn != null)
                {
                    deleteBtn.Content = "✕";
                    deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                }

                _currentEditingRowIndirecto = border;
                montoEdit?.Focus();
            }
        }

        private void SaveInlineEditIndirecto(Border border)
        {
            var grid = border?.Child as Grid;
            if (grid == null) return;

            var montoView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "MontoViewInd");
            var descView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "DescViewInd");
            var montoEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MontoEditInd");
            var descEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEditInd");
            var editBtn = grid.Children.OfType<Button>().FirstOrDefault(b => b.Name == "EditBtnInd");
            var deleteBtn = grid.Children.OfType<Button>().FirstOrDefault(b => b.Name == "DeleteBtnInd");

            // Obtener valores
            string cleanMonto = montoEdit?.Text.Replace("$", "").Replace(",", "").Trim() ?? "0";
            if (!decimal.TryParse(cleanMonto, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Ingrese un monto válido", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                montoEdit?.Focus();
                return;
            }

            string descripcion = descEdit?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                MessageBox.Show("Ingrese una descripción", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                descEdit?.Focus();
                return;
            }

            // Actualizar el objeto
            var gasto = border.DataContext as OrderGastoIndirectoDb;
            if (gasto != null)
            {
                gasto.Monto = monto;
                gasto.Descripcion = descripcion;
            }

            // Volver a modo vista
            if (montoView != null)
            {
                montoView.Text = $"${monto:N2}";
                montoView.Visibility = Visibility.Visible;
            }
            if (descView != null)
            {
                descView.Text = descripcion;
                descView.Visibility = Visibility.Visible;
            }
            if (montoEdit != null) montoEdit.Visibility = Visibility.Collapsed;
            if (descEdit != null) descEdit.Visibility = Visibility.Collapsed;
            if (editBtn != null) editBtn.Content = "✏";
            if (deleteBtn != null)
            {
                deleteBtn.Content = "🗑";
                deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x39, 0x35));
            }

            _currentEditingRowIndirecto = null;
            UpdateGastoIndirectoTotal();
            _hasChanges = true;
            UpdateSaveStatus();
        }

        private void CancelInlineEditIndirecto(Border border)
        {
            var grid = border?.Child as Grid;
            if (grid == null) return;

            var montoView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "MontoViewInd");
            var descView = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "DescViewInd");
            var montoEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "MontoEditInd");
            var descEdit = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEditInd");
            var editBtn = grid.Children.OfType<Button>().FirstOrDefault(b => b.Name == "EditBtnInd");
            var deleteBtn = grid.Children.OfType<Button>().FirstOrDefault(b => b.Name == "DeleteBtnInd");

            // Restaurar valores originales
            var gasto = border.DataContext as OrderGastoIndirectoDb;
            if (gasto != null && montoEdit != null && descEdit != null)
            {
                montoEdit.Text = gasto.Monto.ToString("N2");
                descEdit.Text = gasto.Descripcion;
            }

            // Volver a modo vista
            if (montoView != null) montoView.Visibility = Visibility.Visible;
            if (descView != null) descView.Visibility = Visibility.Visible;
            if (montoEdit != null) montoEdit.Visibility = Visibility.Collapsed;
            if (descEdit != null) descEdit.Visibility = Visibility.Collapsed;
            if (editBtn != null) editBtn.Content = "✏";
            if (deleteBtn != null)
            {
                deleteBtn.Content = "🗑";
                deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x39, 0x35));
            }

            _currentEditingRowIndirecto = null;
        }

        private void GastoIndirectoDescEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                if (border != null)
                {
                    var grid = border.Child as Grid;
                    var editBtn = grid?.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtnInd");
                    editBtn?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                CancelInlineEditIndirecto(border);
            }
        }

        private void GastoIndirectoMontoEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                if (border != null)
                {
                    var grid = border.Child as Grid;
                    var descEdit = grid?.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "DescEditInd");

                    if (descEdit != null && !string.IsNullOrWhiteSpace(descEdit.Text))
                    {
                        var editBtn = grid?.Children.OfType<Button>().FirstOrDefault(t => t.Name == "EditBtnInd");
                        editBtn?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    else
                    {
                        descEdit?.Focus();
                    }
                }
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                var border = FindParent<Border>(textBox);
                CancelInlineEditIndirecto(border);
            }
        }

        private void GastoIndirectoMontoEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string cleanText = textBox.Text.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
            {
                textBox.Text = value.ToString("N2");
            }
        }

        #endregion

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

        private async void DeleteOrderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DeleteOrderButton.IsEnabled = false;
                DeleteOrderButton.Content = "VERIFICANDO...";

                // Primero verificar si se puede eliminar (no tiene facturas, gastos o comisiones)
                var (canDelete, reason) = await _supabaseService.CanDeleteOrder(_order.Id);

                if (!canDelete)
                {
                    MessageBox.Show(
                        $"No se puede eliminar esta orden:\n\n{reason}\n\n" +
                        "Use CANCELAR ORDEN en su lugar para cambiar el estado a CANCELADA.",
                        "No se puede eliminar",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Primera confirmación
                var result1 = MessageBox.Show(
                    $"¿Está seguro que desea ELIMINAR PERMANENTEMENTE la orden {_order.OrderNumber}?\n\n" +
                    "Esta acción:\n" +
                    "• Eliminará la orden de la base de datos\n" +
                    "• Se guardará un registro en la tabla de auditoría\n" +
                    "• NO se puede deshacer\n\n" +
                    "¿Desea continuar?",
                    "Confirmar Eliminación Permanente",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result1 != MessageBoxResult.Yes) return;

                // Segunda confirmación con código
                var confirmCode = $"ELIMINAR-{_order.OrderNumber}";
                var inputWindow = new Window
                {
                    Title = "Confirmación de Seguridad - ELIMINAR",
                    Width = 450,
                    Height = 280,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var warningLabel = new TextBlock
                {
                    Text = "ADVERTENCIA: Esta eliminación es PERMANENTE",
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.Red,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var label1 = new TextBlock
                {
                    Text = "Para confirmar la eliminación, escriba el siguiente código:",
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
                    Foreground = System.Windows.Media.Brushes.DarkRed
                };

                var textBox = new TextBox
                {
                    Height = 30,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                // Prevenir pegar
                DataObject.AddPastingHandler(textBox, (s, ev) => { ev.CancelCommand(); });

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var confirmButton = new Button
                {
                    Content = "ELIMINAR PERMANENTEMENTE",
                    Width = 220,
                    Height = 40,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = System.Windows.Media.Brushes.DarkRed,
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

                confirmButton.Click += (s, ev) =>
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

                cancelButton.Click += (s, ev) => inputWindow.Close();

                buttonPanel.Children.Add(confirmButton);
                buttonPanel.Children.Add(cancelButton);

                Grid.SetRow(warningLabel, 0);
                Grid.SetRow(label1, 1);
                Grid.SetRow(label2, 2);
                Grid.SetRow(textBox, 3);
                Grid.SetRow(buttonPanel, 4);

                grid.Children.Add(warningLabel);
                grid.Children.Add(label1);
                grid.Children.Add(label2);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);

                inputWindow.Content = grid;
                textBox.Focus();

                inputWindow.ShowDialog();

                if (!confirmed) return;

                // Ejecutar la eliminación
                DeleteOrderButton.Content = "ELIMINANDO...";
                var (success, message) = await _supabaseService.DeleteOrderWithAudit(
                    _order.Id,
                    _currentUser.Id,
                    "Orden eliminada manualmente - creada por error"
                );

                if (success)
                {
                    MessageBox.Show(
                        $"La orden {_order.OrderNumber} ha sido eliminada permanentemente.\n\n{message}",
                        "Orden Eliminada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        $"No se pudo eliminar la orden:\n\n{message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al eliminar la orden:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                DeleteOrderButton.IsEnabled = true;
                DeleteOrderButton.Content = "ELIMINAR ORDEN";
            }
        }
    }
}