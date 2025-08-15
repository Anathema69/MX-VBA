// Archivo: Views/InvoiceManagementWindow.xaml.cs - VERSIÓN MEJORADA

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
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

        public InvoiceManagementWindow(int orderId, UserSession currentUser)
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _invoices = new ObservableCollection<InvoiceViewModel>();
            _currentUser = currentUser;

            // VALIDACIÓN DE SEGURIDAD - Solo admin puede acceder
            if (_currentUser.Role != "admin")
            {
                MessageBox.Show(
                    "No tiene permisos para gestionar facturas.\nSolo el administrador puede acceder a este módulo.",
                    "Acceso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // Cerrar la ventana inmediatamente
                this.Loaded += (s, e) => this.Close();
                return;
            }

            InvoicesDataGrid.ItemsSource = _invoices;

            _ = LoadOrderAndInvoices(orderId);
        }

        private async Task LoadOrderAndInvoices(int orderId)
        {
            try
            {
                StatusMessage.Text = "Cargando información...";

                // Cargar información de la orden
                _currentOrder = await _supabaseService.GetOrderById(orderId);

                if (_currentOrder == null)
                {
                    MessageBox.Show("No se pudo cargar la información de la orden.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                // Cargar información del cliente
                if (_currentOrder.ClientId.HasValue)
                {
                    var clients = await _supabaseService.GetClients();
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

                    // IMPORTANTE: Usar SetTotalDirectly para no recalcular
                    viewModel.SetTotalDirectly(invoice.Total ?? 0);

                    // Manejar fechas especiales (1899-12-30 se considera como null)
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

                    System.Diagnostics.Debug.WriteLine($"   ✅ Factura {invoice.Folio}: Subtotal={invoice.Subtotal}, Total={invoice.Total}, Estado={viewModel.Status}");
                }

                UpdateSummary();

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

            // Actualizar UI
            InvoicedAmountText.Text = totalInvoiced.ToString("C", new CultureInfo("es-MX"));
            BalanceText.Text = balance.ToString("C", new CultureInfo("es-MX"));

            // Actualizar barra de progreso
            double percentage = _orderTotal > 0 ? (double)(totalInvoiced / _orderTotal * 100) : 0;
            InvoiceProgressBar.Value = Math.Min(percentage, 100);
            ProgressPercentText.Text = $"{percentage:F0}%";

            // Actualizar advertencia con formato de moneda mexicana
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
        }

        private void AddInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // DOBLE VALIDACIÓN DE SEGURIDAD
            if (_currentUser.Role != "admin")
            {
                MessageBox.Show(
                    "Solo el administrador puede crear facturas.",
                    "Permiso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            var newInvoice = new InvoiceViewModel
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

            // Establecer Total en 0
            newInvoice.SetTotalDirectly(0);

            _invoices.Add(newInvoice);
            _hasUnsavedChanges = true;

            // Mensaje más claro con instrucciones
            StatusMessage.Text = "📝 Nueva factura agregada - Haga doble clic en las celdas para editar";
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210));

            // Seleccionar la nueva fila y enfocar en el primer campo editable
            InvoicesDataGrid.SelectedItem = newInvoice;
            InvoicesDataGrid.ScrollIntoView(newInvoice);

            // Dar un pequeño delay para que la UI se actualice, luego enfocar
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InvoicesDataGrid.CurrentCell = new DataGridCellInfo(newInvoice, InvoicesDataGrid.Columns[1]); // Columna Folio
                InvoicesDataGrid.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasUnsavedChanges && !_invoices.Any(i => i.HasChanges || i.IsNew))
            {
                MessageBox.Show("No hay cambios para guardar.",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Validar antes de guardar
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
                        // Recalcular el total basado en el subtotal actual
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
                            // Crear nueva factura
                            var created = await _supabaseService.CreateInvoice(invoiceDb, _currentUser.Id);
                            if (created != null)
                            {
                                invoice.Id = created.Id;
                                invoice.IsNew = false;
                                invoice.HasChanges = false;
                                savedCount++;

                                System.Diagnostics.Debug.WriteLine($"✅ Factura creada: {invoice.Folio} con ID {created.Id}");
                            }
                        }
                        else if (invoice.HasChanges)
                        {
                            // Actualizar factura existente
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

                if (errorCount > 0)
                {
                    MessageBox.Show($"Se guardaron {savedCount} facturas.\n{errorCount} facturas tuvieron errores.",
                        "Guardado parcial", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"✅ Se guardaron {savedCount} facturas correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }

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

            foreach (var invoice in _invoices)
            {
                // Validar folio obligatorio
                if (string.IsNullOrWhiteSpace(invoice.Folio))
                {
                    MessageBox.Show($"El folio es obligatorio para todas las facturas.",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Validar subtotal positivo
                if (invoice.Subtotal <= 0)
                {
                    MessageBox.Show($"El subtotal debe ser mayor a 0 para la factura {invoice.Folio}.",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                totalSum += invoice.Total;
            }

            // Validar que no se exceda el monto de la orden
            if (totalSum > _orderTotal)
            {
                MessageBox.Show($"La suma de las facturas ({totalSum:C}) excede el monto de la orden ({_orderTotal:C}).",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("Hay cambios sin guardar. ¿Desea continuar y perder los cambios?",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            await LoadInvoices();
            StatusMessage.Text = "Datos actualizados";
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var invoice = button?.Tag as InvoiceViewModel;

            if (invoice == null) return;

            // Si es nueva, solo remover de la lista
            if (invoice.IsNew)
            {
                _invoices.Remove(invoice);
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

                        MessageBox.Show("Factura eliminada correctamente.",
                            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

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
                    invoice.HasChanges = true;
                    _hasUnsavedChanges = true;

                    // Si se editó el subtotal, recalcular el total
                    if (e.Column.Header.ToString() == "SUBTOTAL")
                    {
                        var textBox = e.EditingElement as TextBox;
                        if (textBox != null)
                        {
                            string cleanText = textBox.Text.Replace("$", "").Replace(",", "").Trim();
                            if (decimal.TryParse(cleanText, out decimal subtotal))
                            {
                                invoice.Subtotal = subtotal;
                                System.Diagnostics.Debug.WriteLine($"💰 Subtotal actualizado: {subtotal:C} -> Total: {invoice.Total:C}");
                            }
                        }
                    }

                    // Si se editó la fecha de recepción, calcular fecha programada
                    if (e.Column.Header.ToString() == "RECIBO FACT." && invoice.ReceptionDate.HasValue)
                    {
                        invoice.ClientCreditDays = _currentClient?.Credit ?? 0;
                        System.Diagnostics.Debug.WriteLine($"📅 Fecha recepción: {invoice.ReceptionDate:d} -> Pago prog: {invoice.DueDate:d}");
                    }

                    UpdateSummary();
                }
            }
        }

        private void InvoicesDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            // Marcar que se está editando
            var invoice = e.Row.Item as InvoiceViewModel;
            if (invoice != null)
            {
                invoice.IsEditing = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("Hay cambios sin guardar. ¿Desea salir sin guardar?",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("Hay cambios sin guardar. ¿Está seguro de salir?",
                    "Cambios sin guardar", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }

            base.OnClosing(e);
        }
    }
}