// Archivo: Views/InvoiceManagementWindow.xaml.cs - VERSIÓN MEJORADA CON NUEVAS FUNCIONALIDADES

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class InvoiceManagementWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<InvoiceViewModel> _invoices;
        private OrderDb _currentOrder;
        private ClientDb _currentClient;
        private UserSession _currentUser;
        private decimal _orderTotal;
        private bool _hasUnsavedChanges = false;
        private bool _isCreatingNewInvoice = false; // Nueva bandera para controlar creación

        public InvoiceManagementWindow(int orderId, UserSession currentUser)
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _invoices = new ObservableCollection<InvoiceViewModel>();
            _currentUser = currentUser;

            // VALIDACIÓN DE SEGURIDAD - Solo direccion y administracion pueden acceder
            if (_currentUser.Role != "direccion" && _currentUser.Role != "administracion")
            {
                MessageBox.Show(
                    "No tiene permisos para gestionar facturas.\nSolo Dirección y Administración pueden acceder a este módulo.",
                    "Acceso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                this.Loaded += (s, e) => this.Close();
                return;
            }

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            InvoicesDataGrid.ItemsSource = _invoices;
            _ = LoadOrderAndInvoices(orderId);
        }

        private void MaximizeWithTaskbar()
        {
            // Obtener el área de trabajo (sin incluir la barra de tareas)
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
        }

        private async Task LoadOrderAndInvoices(int orderId)
        {
            try
            {
                StatusMessage.Text = "Cargando información...";

                // OPTIMIZACIÓN: Cargar orden y clientes en paralelo para reducir tiempo de carga
                var orderTask = _supabaseService.GetOrderById(orderId);
                var clientsTask = _supabaseService.GetClients();

                // Esperar ambas tareas en paralelo
                await Task.WhenAll(orderTask, clientsTask);

                _currentOrder = await orderTask;
                var clients = await clientsTask;

                if (_currentOrder == null)
                {
                    MessageBox.Show("No se pudo cargar la información de la orden.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                // Validar estado de la orden (estados permitidos: EN_PROCESO(1), LIBERADA(2), CERRADA(3), COMPLETADA(4))
                var numStatus = _currentOrder.OrderStatus ?? 0;
                bool canInvoice = numStatus >= 1 && numStatus <= 4;

                if (!canInvoice)
                {
                    var statusName = await _supabaseService.GetStatusName(numStatus);

                    MessageBox.Show(
                        $"No se pueden gestionar facturas en este momento.\n\n" +
                        $"Estado actual de la orden: {statusName}\n" +
                        $"Las facturas se pueden crear cuando la orden está EN PROCESO o posterior.\n" +
                        $"(No disponible para órdenes en estado CREADA o CANCELADA)",
                        "Facturación No Disponible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    this.Close();
                    return;
                }

                // Obtener información del cliente desde la lista ya cargada
                if (_currentOrder.ClientId.HasValue)
                {
                    _currentClient = clients?.FirstOrDefault(c => c.Id == _currentOrder.ClientId.Value);
                }

                // Establecer información en la UI
                OrderNumberText.Text = _currentOrder.Po ?? "N/A";
                ClientNameText.Text = _currentClient?.Name ?? "Sin cliente";
                _orderTotal = _currentOrder.SaleTotal ?? 0;
                OrderTotalText.Text = _orderTotal.ToString("C", new CultureInfo("es-MX"));

                // Cargar facturas existentes
                await LoadInvoices();

                StatusMessage.Text = "";

                // Actualizar estado del botón completar factura
                UpdateCompleteInvoiceButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }

        private async Task LoadInvoices()
        {
            try
            {
                _invoices.Clear();

                var invoicesDb = await _supabaseService.GetInvoicesByOrder(_currentOrder.Id);

                System.Diagnostics.Debug.WriteLine($"📋 Cargando {invoicesDb.Count} facturas para orden {_currentOrder.Id}");

                foreach (var invoice in invoicesDb)
                {
                    var viewModel = new InvoiceViewModel
                    {
                        Id = invoice.Id,
                        OrderId = invoice.OrderId ?? _currentOrder.Id,
                        Folio = invoice.Folio,
                        InvoiceDate = invoice.InvoiceDate,
                        ReceptionDate = invoice.ReceptionDate,
                        Subtotal = invoice.Subtotal ?? 0,
                        PaymentDate = invoice.PaymentDate,
                        DueDate = invoice.DueDate,
                        StatusId = invoice.InvoiceStatus ?? 1,
                        ClientCreditDays = _currentClient?.Credit ?? 0,
                        IsNew = false,
                        HasChanges = false
                    };

                    // Usar SetTotalDirectly para no recalcular
                    viewModel.SetTotalDirectly(invoice.Total ?? 0);

                    // Manejar fechas especiales
                    if (viewModel.PaymentDate.HasValue && viewModel.PaymentDate.Value.Year < 1900)
                    {
                        viewModel.PaymentDate = null;
                    }

                    // Establecer el estado basado en el ID
                    switch (viewModel.StatusId)
                    {
                        case 1: viewModel.Status = "CREADA"; break;
                        case 2: viewModel.Status = "PENDIENTE"; break;
                        case 3: viewModel.Status = "VENCIDA"; break;
                        case 4: viewModel.Status = "PAGADA"; break;
                        default: viewModel.Status = "DESCONOCIDO"; break;
                    }

                    // Si no hay DueDate pero hay ReceptionDate, calcularlo
                    if (!viewModel.DueDate.HasValue && viewModel.ReceptionDate.HasValue && _currentClient != null)
                    {
                        viewModel.DueDate = viewModel.ReceptionDate.Value.AddDays(_currentClient.Credit);
                    }

                    _invoices.Add(viewModel);
                }

                UpdateSummary();
                _isCreatingNewInvoice = false; // Reset la bandera después de cargar
                AddInvoiceButton.IsEnabled = true; // Habilitar el botón

                System.Diagnostics.Debug.WriteLine($"📊 Total facturado: {_invoices.Sum(i => i.Total):C}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar facturas: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando facturas: {ex}");
            }
        }

        private void UpdateSummary()
        {
            // Calcular totales (solo de facturas guardadas, no las nuevas)
            decimal totalInvoiced = _invoices.Where(i => !i.IsNew).Sum(i => i.Total);
            decimal totalSubtotal = _invoices.Where(i => !i.IsNew).Sum(i => i.Subtotal);
            decimal balance = _orderTotal - totalInvoiced;

            // NUEVO: Calcular balance sin IVA
            decimal balanceWithoutTax = balance / 1.16m;

            // Actualizar UI
            InvoicedAmountText.Text = totalInvoiced.ToString("C", new CultureInfo("es-MX"));
            BalanceText.Text = balance.ToString("C", new CultureInfo("es-MX"));

            // NUEVO: Mostrar balance sin IVA
            BalanceWithoutTaxText.Text = balanceWithoutTax.ToString("C", new CultureInfo("es-MX"));

            // Actualizar barra de progreso
            double percentage = _orderTotal > 0 ? (double)(totalInvoiced / _orderTotal * 100) : 0;
            InvoiceProgressBar.Value = Math.Min(percentage, 100);
            ProgressPercentText.Text = $"{percentage:F0}%";

            // Actualizar advertencia
            var cultureMX = new CultureInfo("es-MX");
            if (balance > 0)
            {
                WarningText.Text = $"⚠️ Puede facturar hasta {balance.ToString("C", cultureMX)} más";
                WarningText.Foreground = new SolidColorBrush(Color.FromRgb(255, 224, 130));
            }
            else if (balance < 0)
            {
                WarningText.Text = $"❌ Excedido por {Math.Abs(balance):C}";
                WarningText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
            else
            {
                WarningText.Text = "✅ Facturación completa";
                WarningText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }

            // Actualizar footer
            InvoiceCountText.Text = _invoices.Count(i => !i.IsNew).ToString();
            SubtotalSumText.Text = totalSubtotal.ToString("C", new CultureInfo("es-MX"));
            TotalSumText.Text = totalInvoiced.ToString("C", new CultureInfo("es-MX"));
            LastUpdateText.Text = $"Última actualización: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

            // Actualizar estado del botón completar factura
            UpdateCompleteInvoiceButtonState();
        }

        private void UpdateCompleteInvoiceButtonState()
        {
            // Calcular el balance actual
            decimal totalInvoiced = _invoices.Where(i => !i.IsNew).Sum(i => i.Total);
            decimal balance = _orderTotal - totalInvoiced;

            // Habilitar el botón solo si hay balance pendiente y no hay facturas nuevas sin guardar
            bool hasNewInvoices = _invoices.Any(i => i.IsNew);
            CompleteInvoiceButton.IsEnabled = balance > 0 && !hasNewInvoices && !_isCreatingNewInvoice;

            // Actualizar tooltip
            if (balance <= 0)
            {
                CompleteInvoiceButton.ToolTip = "La facturación ya está completa";
            }
            else if (hasNewInvoices)
            {
                CompleteInvoiceButton.ToolTip = "Guarde las facturas pendientes antes de completar";
            }
            else if (_isCreatingNewInvoice)
            {
                CompleteInvoiceButton.ToolTip = "Complete la factura actual antes de crear otra";
            }
            else
            {
                decimal balanceWithoutTax = balance / 1.16m;
                CompleteInvoiceButton.ToolTip = $"Crear factura por {balanceWithoutTax:C} (sin IVA) / {balance:C} (con IVA)";
            }
        }

        private void AddInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // NUEVA VALIDACIÓN: Verificar si ya hay una factura nueva siendo creada
            if (_isCreatingNewInvoice)
            {
                MessageBox.Show(
                    "Ya hay una factura nueva en edición.\n" +
                    "Complete o cancele la factura actual antes de crear otra.",
                    "Factura en Edición",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Enfocar en la factura nueva existente
                var newInvoice = _invoices.FirstOrDefault(i => i.IsNew);
                if (newInvoice != null)
                {
                    InvoicesDataGrid.SelectedItem = newInvoice;
                    InvoicesDataGrid.ScrollIntoView(newInvoice);
                }
                return;
            }

            // Verificar que no se exceda el monto de la orden
            decimal currentTotal = _invoices.Where(i => !i.IsNew).Sum(i => i.Total);
            decimal remaining = _orderTotal - currentTotal;

            if (remaining <= 0)
            {
                MessageBox.Show("No puede agregar más facturas.\nEl monto total de la orden ya ha sido facturado.",
                    "Límite alcanzado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Crear nueva factura vacía
            var newInvoiceItem = new InvoiceViewModel
            {
                Id = 0,
                OrderId = _currentOrder.Id,
                Folio = "",
                InvoiceDate = DateTime.Now,
                Subtotal = 0,
                StatusId = 1,
                Status = "CREADA",
                ClientCreditDays = _currentClient?.Credit ?? 0,
                IsNew = true,
                HasChanges = true
            };

            newInvoiceItem.SetTotalDirectly(0);

            _invoices.Add(newInvoiceItem);
            _hasUnsavedChanges = true;
            _isCreatingNewInvoice = true; // Marcar que hay una factura nueva en creación

            // Deshabilitar el botón mientras hay una factura nueva
            AddInvoiceButton.IsEnabled = false;
            CompleteInvoiceButton.IsEnabled = false;

            // Mensaje más claro
            StatusMessage.Text = "📝 Nueva factura agregada - Presione TAB para navegar, ENTER para guardar";
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210));

            // Seleccionar la nueva fila y enfocar
            InvoicesDataGrid.SelectedItem = newInvoiceItem;
            InvoicesDataGrid.ScrollIntoView(newInvoiceItem);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                InvoicesDataGrid.CurrentCell = new DataGridCellInfo(newInvoiceItem, InvoicesDataGrid.Columns[1]);
                InvoicesDataGrid.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // NUEVO: Método para completar factura al 100%
        private void CompleteInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Calcular el monto pendiente
            decimal totalInvoiced = _invoices.Where(i => !i.IsNew).Sum(i => i.Total);
            decimal balance = _orderTotal - totalInvoiced;
            decimal balanceWithoutTax = balance / 1.16m;

            if (balance <= 0)
            {
                MessageBox.Show("La facturación ya está completa.",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

           

            // Crear la factura con el monto restante
            var completeInvoice = new InvoiceViewModel
            {
                Id = 0,
                OrderId = _currentOrder.Id,
                Folio = "", // El usuario debe llenarlo
                InvoiceDate = DateTime.Now,
                Subtotal = balanceWithoutTax,
                StatusId = 1,
                Status = "CREADA",
                ClientCreditDays = _currentClient?.Credit ?? 0,
                IsNew = true,
                HasChanges = true
            };

            // El total se calcula automáticamente
            completeInvoice.RecalculateTotal();

            _invoices.Add(completeInvoice);
            _hasUnsavedChanges = true;
            _isCreatingNewInvoice = true;

            // Deshabilitar botones
            AddInvoiceButton.IsEnabled = false;
            CompleteInvoiceButton.IsEnabled = false;

            StatusMessage.Text = "⚡ Factura de completación creada - Ingrese el folio y guarde";
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));

            // Seleccionar y enfocar en el campo de folio
            InvoicesDataGrid.SelectedItem = completeInvoice;
            InvoicesDataGrid.ScrollIntoView(completeInvoice);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                InvoicesDataGrid.CurrentCell = new DataGridCellInfo(completeInvoice, InvoicesDataGrid.Columns[1]); // Columna Folio
                InvoicesDataGrid.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // NUEVO: Manejar tecla Enter para guardar

        

        private void InvoicesDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // Siempre manejar Enter para prevenir navegación por defecto

                // IMPORTANTE: Primero commitear cualquier edición en progreso
                // Esto asegura que los valores en edición se guarden en el objeto
                InvoicesDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                InvoicesDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                // Pequeña pausa para asegurar que el commit se complete
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Obtener la factura actual (usar CurrentCell.Item con SelectionUnit="Cell")
                    var currentInvoice = (InvoicesDataGrid.CurrentCell.Item ?? InvoicesDataGrid.SelectedItem) as InvoiceViewModel;

                    // Si hay una factura nueva siendo editada, validar
                    if (currentInvoice != null && currentInvoice.IsNew)
                    {
                        // Validar que tenga al menos folio y subtotal
                        if (string.IsNullOrWhiteSpace(currentInvoice.Folio))
                        {
                            MessageBox.Show(
                                "Debe ingresar el folio de la factura antes de guardar.",
                                "Validación",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                            // Enfocar en la columna de folio
                            InvoicesDataGrid.CurrentCell = new DataGridCellInfo(currentInvoice, InvoicesDataGrid.Columns[1]);
                            InvoicesDataGrid.BeginEdit();
                            return;
                        }

                        if (currentInvoice.Subtotal <= 0)
                        {
                            MessageBox.Show(
                                "Debe ingresar un subtotal mayor a 0 antes de guardar.",
                                "Validación",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                            // Enfocar en la columna de subtotal
                            InvoicesDataGrid.CurrentCell = new DataGridCellInfo(currentInvoice, InvoicesDataGrid.Columns[3]);
                            InvoicesDataGrid.BeginEdit();
                            return;
                        }
                    }

                    // Si hay cambios pendientes o facturas nuevas válidas, guardar
                    if (_hasUnsavedChanges || _invoices.Any(i => i.HasChanges || (i.IsNew && !string.IsNullOrWhiteSpace(i.Folio))))
                    {
                        // Ejecutar guardado
                        SaveAllButton_Click(null, null);
                    }
                    else
                    {
                        // Si no hay cambios, mostrar mensaje
                        StatusMessage.Text = "No hay cambios para guardar";
                        StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    }

                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (e.Key == Key.Tab)
            {
                // Para Tab, commitear la celda actual y navegar inteligentemente
                InvoicesDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                e.Handled = true; // Manejar Tab manualmente para mejor control

                // Obtener la celda actual y la fila
                var currentCell = InvoicesDataGrid.CurrentCell;
                var currentRow = InvoicesDataGrid.SelectedItem as InvoiceViewModel;

                // Si no hay celda o fila actual, intentar obtenerla de otra forma
                if (currentCell.Column == null || currentRow == null)
                {
                    currentRow = currentCell.Item as InvoiceViewModel;
                    if (currentRow == null) return;
                }

                // Columnas editables: 1-Folio, 2-Fecha Factura, 3-Subtotal, 5-Recibo Fact., 7-Fecha Pago
                // Columnas NO editables: 0-No., 4-Total(c/IVA), 6-Pago Prog., 8-Estado, 9-Acciones
                var editableColumnIndices = new List<int> { 1, 2, 3, 5, 7 };

                int currentColIndex = currentCell.Column != null
                    ? InvoicesDataGrid.Columns.IndexOf(currentCell.Column)
                    : editableColumnIndices.First();

                bool shiftPressed = Keyboard.Modifiers == ModifierKeys.Shift;

                int nextColIndex;
                object targetRow = currentRow;

                if (shiftPressed)
                {
                    // Shift+Tab: ir hacia atrás
                    nextColIndex = editableColumnIndices
                        .Where(i => i < currentColIndex)
                        .OrderByDescending(i => i)
                        .FirstOrDefault(-1);

                    // Si no hay columna anterior en esta fila, ir a la fila anterior
                    if (nextColIndex == -1)
                    {
                        var rowIndex = InvoicesDataGrid.Items.IndexOf(currentRow);
                        if (rowIndex > 0)
                        {
                            targetRow = InvoicesDataGrid.Items[rowIndex - 1];
                            nextColIndex = editableColumnIndices.Last();
                        }
                        else
                        {
                            return; // Ya estamos en la primera celda editable
                        }
                    }
                }
                else
                {
                    // Tab normal: ir hacia adelante
                    nextColIndex = editableColumnIndices
                        .Where(i => i > currentColIndex)
                        .OrderBy(i => i)
                        .FirstOrDefault(-1);

                    // Si no hay columna siguiente en esta fila, ir a la siguiente fila
                    if (nextColIndex == -1)
                    {
                        var rowIndex = InvoicesDataGrid.Items.IndexOf(currentRow);
                        if (rowIndex < InvoicesDataGrid.Items.Count - 1)
                        {
                            targetRow = InvoicesDataGrid.Items[rowIndex + 1];
                            nextColIndex = editableColumnIndices.First();
                        }
                        else
                        {
                            return; // Ya estamos en la última celda editable
                        }
                    }
                }

                // Navegar a la siguiente celda editable y comenzar edición
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var targetColumn = InvoicesDataGrid.Columns[nextColIndex];
                    InvoicesDataGrid.SelectedItem = targetRow;
                    InvoicesDataGrid.CurrentCell = new DataGridCellInfo(targetRow, targetColumn);
                    InvoicesDataGrid.BeginEdit();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (e.Key == Key.Escape)
            {
                if (_isCreatingNewInvoice)
                {
                    // Cancelar edición actual
                    InvoicesDataGrid.CancelEdit(DataGridEditingUnit.Cell);
                    InvoicesDataGrid.CancelEdit(DataGridEditingUnit.Row);

                    // Permitir cancelar la creación con ESC
                    var newInvoice = _invoices.FirstOrDefault(i => i.IsNew);
                    if (newInvoice != null)
                    {
                        var result = MessageBox.Show(
                            "¿Desea cancelar la creación de la nueva factura?",
                            "Cancelar",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            _invoices.Remove(newInvoice);
                            _isCreatingNewInvoice = false;
                            AddInvoiceButton.IsEnabled = true;
                            UpdateSummary();
                            StatusMessage.Text = "Creación cancelada";
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private async void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasUnsavedChanges && !_invoices.Any(i => i.HasChanges || i.IsNew))
            {
                return;
            }

            if (!ValidateInvoices())
            {
                return;
            }

            try
            {
                SaveAllButton.IsEnabled = false;
                StatusMessage.Text = "Guardando cambios...";
                int savedCount = 0;
                int errorCount = 0;

                foreach (var invoice in _invoices.Where(i => i.HasChanges || i.IsNew))
                {
                    try
                    {
                        invoice.RecalculateTotal();

                        var invoiceDb = new InvoiceDb
                        {
                            Id = invoice.Id,
                            OrderId = invoice.OrderId,
                            Folio = invoice.Folio,
                            InvoiceDate = invoice.InvoiceDate,
                            ReceptionDate = invoice.ReceptionDate,
                            Subtotal = invoice.Subtotal,
                            Total = invoice.Total,
                            InvoiceStatus = invoice.StatusId,
                            PaymentDate = invoice.PaymentDate,
                            DueDate = invoice.DueDate
                        };

                        if (invoice.IsNew)
                        {
                            var created = await _supabaseService.CreateInvoice(invoiceDb, _currentUser.Id);
                            if (created != null)
                            {
                                invoice.Id = created.Id;
                                invoice.IsNew = false;
                                invoice.HasChanges = false;
                                savedCount++;

                                _isCreatingNewInvoice = false;
                                AddInvoiceButton.IsEnabled = true;

                                System.Diagnostics.Debug.WriteLine($"✅ Factura creada: {invoice.Folio} con ID {created.Id}");
                            }
                        }
                        else if (invoice.HasChanges)
                        {
                            bool updated = await _supabaseService.UpdateInvoice(invoiceDb, _currentUser.Id);
                            if (updated)
                            {
                                invoice.HasChanges = false;
                                savedCount++;
                                System.Diagnostics.Debug.WriteLine($"✅ Factura actualizada: {invoice.Folio}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        System.Diagnostics.Debug.WriteLine($"❌ Error guardando factura {invoice.Folio}: {ex.Message}");
                    }
                }

                // NUEVO: Verificar y actualizar estado de la orden después de guardar facturas
                // VAMOS A QUITAR ESTO PARA QUE NO HAGA NADA AUTOMATICAMENTE, EN LA BD HAREMOS UN TRIGGER
                
                /*
                if (savedCount > 0)
                {
                    StatusMessage.Text = "Verificando estado de la orden...";
                    bool statusUpdated = await _supabaseService.CheckAndUpdateOrderStatus(_currentOrder.Id, _currentUser.Id);

                    if (statusUpdated)
                    {
                        StatusMessage.Text = "✅ Estado de la orden actualizado automáticamente";
                        StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                        
                    }
                }
                */

                

                _hasUnsavedChanges = false;
                StatusMessage.Text = $"Guardado: {DateTime.Now:HH:mm:ss}";
                StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ Error general al guardar: {ex}");
            }
            finally
            {
                SaveAllButton.IsEnabled = true;
            }
        }

        private bool ValidateInvoices()
        {
            decimal totalSum = 0;
            var foliosInThisOrder = new HashSet<string>(); // Para validar duplicados dentro de la misma orden

            foreach (var invoice in _invoices)
            {
                // Validar folio obligatorio
                if (string.IsNullOrWhiteSpace(invoice.Folio))
                {
                    MessageBox.Show(
                        $"El folio es obligatorio para todas las facturas.",
                        "Validación",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Enfocar en la factura con problema
                    InvoicesDataGrid.SelectedItem = invoice;
                    InvoicesDataGrid.ScrollIntoView(invoice);
                    InvoicesDataGrid.CurrentCell = new DataGridCellInfo(invoice, InvoicesDataGrid.Columns[1]);

                    return false;
                }

                // Validar que no haya folios duplicados en la misma orden
                if (!foliosInThisOrder.Add(invoice.Folio.Trim().ToUpper()))
                {
                    MessageBox.Show(
                        $"El folio '{invoice.Folio}' está duplicado en esta orden.\n" +
                        "Cada factura debe tener un folio único dentro de la misma orden.",
                        "Validación",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    InvoicesDataGrid.SelectedItem = invoice;
                    InvoicesDataGrid.ScrollIntoView(invoice);

                    return false;
                }

                // Validar subtotal positivo
                if (invoice.Subtotal <= 0)
                {
                    MessageBox.Show(
                        $"El subtotal debe ser mayor a 0 para la factura {invoice.Folio}.",
                        "Validación",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    InvoicesDataGrid.SelectedItem = invoice;
                    InvoicesDataGrid.ScrollIntoView(invoice);
                    InvoicesDataGrid.CurrentCell = new DataGridCellInfo(invoice, InvoicesDataGrid.Columns[3]);

                    return false;
                }

                totalSum += invoice.Total;
            }

            // Validar que no se exceda el monto de la orden
            if (totalSum > _orderTotal * 1.01m) // Tolerancia del 1% por redondeos
            {
                MessageBox.Show(
                    $"La suma de las facturas ({totalSum:C}) excede el monto de la orden ({_orderTotal:C}).",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            

            await LoadInvoices();
            StatusMessage.Text = "Datos actualizados";
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var invoice = button?.Tag as InvoiceViewModel;

            if (invoice == null) return;

            if (invoice.IsNew)
            {
                _invoices.Remove(invoice);
                _isCreatingNewInvoice = false;
                AddInvoiceButton.IsEnabled = true;
                UpdateSummary();
                return;
            }

            var result = MessageBox.Show($"¿Está seguro de eliminar la factura {invoice.Folio}?\n\nEsta acción no se puede deshacer.",
                "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = await _supabaseService.DeleteInvoice(invoice.Id, _currentUser.Id);

                    if (deleted)
                    {
                        _invoices.Remove(invoice);
                        UpdateSummary();

                        

                        System.Diagnostics.Debug.WriteLine($"✅ Factura {invoice.Folio} eliminada");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"❌ Error eliminando factura: {ex}");
                }
            }
        }

        

        private void InvoicesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var invoice = e.Row.Item as InvoiceViewModel;
                if (invoice != null)
                {
                    // Marcar que hay cambios
                    invoice.HasChanges = true;
                    _hasUnsavedChanges = true;

                    // Manejar específicamente cada columna
                    var columnHeader = e.Column.Header?.ToString();

                    if (columnHeader == "SUBTOTAL")
                    {
                        var textBox = e.EditingElement as TextBox;
                        if (textBox != null)
                        {
                            string cleanText = textBox.Text.Replace("$", "").Replace(",", "").Trim();
                            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
                            {
                                invoice.Subtotal = subtotal;
                                System.Diagnostics.Debug.WriteLine($"💰 Subtotal actualizado: {subtotal:C} -> Total: {invoice.Total:C}");
                            }
                        }
                    }
                    else if (columnHeader == "FOLIO FACTURA")
                    {
                        var textBox = e.EditingElement as TextBox;
                        if (textBox != null)
                        {
                            invoice.Folio = textBox.Text?.Trim();
                            System.Diagnostics.Debug.WriteLine($"📄 Folio actualizado: {invoice.Folio}");
                        }
                    }
                    else if (columnHeader == "RECIBO FACT." && invoice.ReceptionDate.HasValue)
                    {
                        invoice.ClientCreditDays = _currentClient?.Credit ?? 0;
                        System.Diagnostics.Debug.WriteLine($"📅 Fecha recepción: {invoice.ReceptionDate:d} -> Pago prog: {invoice.DueDate:d}");
                    }

                    // Actualizar el resumen después de cualquier cambio
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateSummary();

                        // Si es una factura nueva y ya tiene folio y subtotal, quitar el bloqueo
                        if (invoice.IsNew && !string.IsNullOrWhiteSpace(invoice.Folio) && invoice.Subtotal > 0)
                        {
                            StatusMessage.Text = "Factura lista para guardar - Presione Enter o haga clic en Guardar";
                            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        

        private void InvoicesDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            var invoice = e.Row.Item as InvoiceViewModel;
            if (invoice != null)
            {
                invoice.IsEditing = true;

                // Si es una nueva factura, actualizar el mensaje de estado
                if (invoice.IsNew)
                {
                    var columnHeader = e.Column.Header?.ToString();
                    StatusMessage.Text = $"Editando: {columnHeader} - Use TAB para navegar, ENTER para guardar";
                    StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                }
            }
        }

        /// <summary>
        /// Maneja el clic del mouse para entrar automáticamente en modo edición
        /// en celdas editables (sin necesidad de F2 o doble clic)
        /// </summary>
        private void InvoicesDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Buscar la celda clickeada
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null || cell.IsEditing || cell.IsReadOnly) return;

            // Obtener el índice de la columna
            var column = cell.Column;
            if (column == null) return;

            int colIndex = InvoicesDataGrid.Columns.IndexOf(column);

            // Columnas editables: 1-Folio, 2-Fecha Factura, 3-Subtotal, 5-Recibo Fact., 7-Fecha Pago
            var editableColumnIndices = new HashSet<int> { 1, 2, 3, 5, 7 };

            if (editableColumnIndices.Contains(colIndex))
            {
                // Seleccionar la fila primero
                var row = FindVisualParent<DataGridRow>(cell);
                if (row != null)
                {
                    InvoicesDataGrid.SelectedItem = row.Item;
                    InvoicesDataGrid.CurrentCell = new DataGridCellInfo(row.Item, column);

                    // Iniciar edición después de un pequeño delay para permitir la selección
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        InvoicesDataGrid.BeginEdit();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        /// <summary>
        /// Busca un elemento padre visual del tipo especificado
        /// </summary>
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;

            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            

            this.Close();
        }

       
        // Para volver atrás
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}