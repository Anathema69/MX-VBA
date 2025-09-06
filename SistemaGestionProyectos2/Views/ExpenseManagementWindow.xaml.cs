using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class ExpenseManagementWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<ExpenseViewModel> _expenses;
        private ObservableCollection<ExpenseViewModel> _filteredExpenses;
        private List<SupplierDb> _suppliers;
        private ExpenseViewModel _selectedExpense;
        private bool _isLoading = false;
        private bool _hasUnsavedChanges = false;

        public ExpenseManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _supabaseService = SupabaseService.Instance;
            _expenses = new ObservableCollection<ExpenseViewModel>();
            _filteredExpenses = new ObservableCollection<ExpenseViewModel>();

            InitializeUI();
            _ = LoadDataAsync();
        }

        private void InitializeUI()
        {
            CurrentUserText.Text = $"Usuario: {_currentUser.FullName} ({_currentUser.Role})";
            ExpensesDataGrid.ItemsSource = _filteredExpenses;

            // Configurar fechas por defecto (último mes)
            FromDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
            ToDatePicker.SelectedDate = DateTime.Now;
        }

        private async Task LoadDataAsync()
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                StatusText.Text = "Cargando datos...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                // Cargar proveedores primero
                await LoadSuppliers();

                // Cargar gastos
                await LoadExpenses();

                // Aplicar filtros
                ApplyFilters();

                // Actualizar estadísticas
                UpdateStatistics();

                StatusText.Text = "Datos cargados";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cargar datos: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "Error al cargar datos";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task LoadSuppliers()
        {
            try
            {
                _suppliers = await _supabaseService.GetActiveSuppliers();

                // Llenar combo de filtro de proveedores
                SupplierFilterCombo.Items.Clear();
                SupplierFilterCombo.Items.Add("Todos los proveedores");

                foreach (var supplier in _suppliers.OrderBy(s => s.SupplierName))
                {
                    SupplierFilterCombo.Items.Add(supplier.SupplierName);
                }

                SupplierFilterCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando proveedores: {ex.Message}");
            }
        }

        private async Task LoadExpenses()
        {
            try
            {
                // Obtener fechas de filtro si están seleccionadas
                DateTime? fromDate = FromDatePicker.SelectedDate;
                DateTime? toDate = ToDatePicker.SelectedDate;

                // Cargar gastos con filtros de fecha
                var expensesDb = await _supabaseService.GetExpenses(
                    fromDate: fromDate,
                    toDate: toDate,
                    limit: 500
                );

                _expenses.Clear();

                foreach (var expense in expensesDb)
                {
                    // Buscar el nombre del proveedor
                    var supplier = _suppliers?.FirstOrDefault(s => s.Id == expense.SupplierId);

                    var expenseVm = new ExpenseViewModel
                    {
                        ExpenseId = expense.Id,
                        SupplierId = expense.SupplierId,
                        SupplierName = supplier?.SupplierName ?? "Proveedor Desconocido",
                        Description = expense.Description,
                        ExpenseDate = expense.ExpenseDate,
                        TotalExpense = expense.TotalExpense,
                        ScheduledDate = expense.ScheduledDate,
                        Status = expense.Status,
                        PaidDate = expense.PaidDate,
                        PayMethod = expense.PayMethod,
                        OrderId = expense.OrderId,
                        ExpenseCategory = expense.ExpenseCategory
                    };

                    _expenses.Add(expenseVm);
                }

                System.Diagnostics.Debug.WriteLine($"Gastos cargados: {_expenses.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando gastos: {ex.Message}");
                throw;
            }
        }

        private void ApplyFilters()
        {
            _filteredExpenses.Clear();

            var filtered = _expenses.AsEnumerable();

            // Filtro por búsqueda de texto
            var searchText = SearchBox?.Text?.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(e =>
                    e.Description?.ToLower().Contains(searchText) == true ||
                    e.SupplierName?.ToLower().Contains(searchText) == true ||
                    e.ExpenseCategory?.ToLower().Contains(searchText) == true);
            }

            // Filtro por proveedor
            if (SupplierFilterCombo?.SelectedIndex > 0)
            {
                var selectedSupplier = SupplierFilterCombo.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selectedSupplier))
                {
                    filtered = filtered.Where(e => e.SupplierName == selectedSupplier);
                }
            }

            // Filtro por estado
            if (StatusFilterCombo?.SelectedIndex > 0)
            {
                var selectedStatus = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (selectedStatus == "VENCIDO")
                {
                    filtered = filtered.Where(e => e.IsOverdue);
                }
                else if (!string.IsNullOrEmpty(selectedStatus))
                {
                    filtered = filtered.Where(e => e.Status == selectedStatus);
                }
            }

            // Ordenar por fecha descendente
            filtered = filtered.OrderByDescending(e => e.ExpenseDate)
                              .ThenByDescending(e => e.ExpenseId);

            foreach (var expense in filtered)
            {
                _filteredExpenses.Add(expense);
            }

            RecordCountText.Text = $"{_filteredExpenses.Count} registros";
        }

        private void UpdateStatistics()
        {
            try
            {
                var culture = new CultureInfo("es-MX");

                // Total General (todos los gastos filtrados)
                decimal totalGeneral = _filteredExpenses.Sum(e => e.TotalExpense);
                TotalGeneralText.Text = totalGeneral.ToString("C2", culture);

                // Total Pendiente
                decimal totalPendiente = _filteredExpenses
                    .Where(e => e.Status == "PENDIENTE")
                    .Sum(e => e.TotalExpense);
                TotalPendienteText.Text = totalPendiente.ToString("C2", culture);

                // Total Vencido
                decimal totalVencido = _filteredExpenses
                    .Where(e => e.IsOverdue)
                    .Sum(e => e.TotalExpense);
                TotalVencidoText.Text = totalVencido.ToString("C2", culture);

                // Total Pagado
                decimal totalPagado = _filteredExpenses
                    .Where(e => e.Status == "PAGADO")
                    .Sum(e => e.TotalExpense);
                TotalPagadoText.Text = totalPagado.ToString("C2", culture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando estadísticas: {ex.Message}");
            }
        }

        // === EVENT HANDLERS ===

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
            UpdateStatistics();
        }

        private void SupplierFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_expenses != null)
            {
                ApplyFilters();
                UpdateStatistics();
            }
        }

        private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_expenses != null)
            {
                ApplyFilters();
                UpdateStatistics();
            }
        }

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Recargar datos con nuevos filtros de fecha
            if (!_isLoading && _expenses != null)
            {
                _ = LoadDataAsync();
            }
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            SupplierFilterCombo.SelectedIndex = 0;
            StatusFilterCombo.SelectedIndex = 0;
            FromDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
            ToDatePicker.SelectedDate = DateTime.Now;

            _ = LoadDataAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void NewExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new NewExpenseDialog();
                if (dialog.ShowDialog() == true && dialog.Result != null)
                {
                    // Agregar el nuevo gasto a la lista
                    _expenses.Add(dialog.Result);
                    ApplyFilters();
                    UpdateStatistics();

                    MessageBox.Show(
                        "Gasto registrado exitosamente",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al crear gasto: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExpensesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedExpense = ExpensesDataGrid.SelectedItem as ExpenseViewModel;
        }

        private async void PayButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as ExpenseViewModel;

            if (expense == null || expense.IsPaid) return;

            try
            {
                // Diálogo simple para confirmar pago
                var result = MessageBox.Show(
                    $"¿Confirmar pago de ${expense.TotalExpense:N2} a {expense.SupplierName}?\n\n" +
                    $"Descripción: {expense.Description}\n" +
                    $"Se registrará con fecha de hoy.",
                    "Confirmar Pago",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                // TODO: Crear un diálogo para seleccionar método de pago
                string payMethod = "TRANSFERENCIA"; // Por defecto

                // Registrar pago
                var success = await _supabaseService.MarkExpenseAsPaid(
                    expense.ExpenseId,
                    DateTime.Now,
                    payMethod);

                if (success)
                {
                    // Actualizar en la UI
                    expense.Status = "PAGADO";
                    expense.PaidDate = DateTime.Now;
                    expense.PayMethod = payMethod;

                    // Refrescar la vista
                    var index = _filteredExpenses.IndexOf(expense);
                    if (index >= 0)
                    {
                        _filteredExpenses[index] = expense;
                    }

                    UpdateStatistics();

                    MessageBox.Show(
                        "Pago registrado exitosamente",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Error al registrar el pago",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al procesar pago: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as ExpenseViewModel;

            if (expense == null) return;

            try
            {
                var dialog = new NewExpenseDialog(expense);
                if (dialog.ShowDialog() == true && dialog.Result != null)
                {
                    // Actualizar en la lista
                    var index = _expenses.IndexOf(expense);
                    if (index >= 0)
                    {
                        _expenses[index] = dialog.Result;
                    }

                    ApplyFilters();
                    UpdateStatistics();

                    MessageBox.Show(
                        "Gasto actualizado exitosamente",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al editar gasto: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as ExpenseViewModel;

            if (expense == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"¿Está seguro de eliminar este gasto?\n\n" +
                    $"Proveedor: {expense.SupplierName}\n" +
                    $"Descripción: {expense.Description}\n" +
                    $"Monto: ${expense.TotalExpense:N2}\n\n" +
                    "Esta acción no se puede deshacer.",
                    "Confirmar Eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                var success = await _supabaseService.DeleteExpense(expense.ExpenseId);

                if (success)
                {
                    _expenses.Remove(expense);
                    _filteredExpenses.Remove(expense);
                    UpdateStatistics();

                    MessageBox.Show(
                        "Gasto eliminado exitosamente",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Error al eliminar el gasto",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al eliminar gasto: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Crear archivo CSV en lugar de Excel
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files|*.csv|Text Files|*.txt",
                    Title = "Guardar reporte de gastos",
                    FileName = $"Gastos_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var culture = new CultureInfo("es-MX");
                    var sb = new StringBuilder();

                    // Headers
                    sb.AppendLine("ID,Proveedor,Descripción,Categoría,Fecha Compra,Total,Fecha Programada,Estado,Fecha Pago,Método Pago");

                    // Datos
                    foreach (var expense in _filteredExpenses)
                    {
                        var row = new[]
                        {
                            expense.ExpenseId.ToString(),
                            $"\"{expense.SupplierName ?? ""}\"",
                            $"\"{expense.Description?.Replace("\"", "\"\"") ?? ""}\"",
                            $"\"{expense.ExpenseCategory ?? ""}\"",
                            expense.ExpenseDate.ToString("dd/MM/yyyy"),
                            expense.TotalExpense.ToString("F2", culture),
                            expense.ScheduledDate?.ToString("dd/MM/yyyy") ?? "",
                            expense.Status ?? "",
                            expense.PaidDate?.ToString("dd/MM/yyyy") ?? "",
                            expense.PayMethod ?? ""
                        };

                        sb.AppendLine(string.Join(",", row));
                    }

                    // Agregar resumen al final
                    sb.AppendLine();
                    sb.AppendLine("RESUMEN");
                    sb.AppendLine($"Total General,{_filteredExpenses.Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Total Pendiente,{_filteredExpenses.Where(e => e.Status == "PENDIENTE").Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Total Pagado,{_filteredExpenses.Where(e => e.Status == "PAGADO").Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Total Vencido,{_filteredExpenses.Where(e => e.IsOverdue).Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Cantidad de Registros,{_filteredExpenses.Count}");

                    // Guardar archivo
                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                    MessageBox.Show(
                        "Reporte exportado exitosamente",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Preguntar si desea abrir el archivo
                    var openResult = MessageBox.Show(
                        "¿Desea abrir el archivo exportado?",
                        "Abrir archivo",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (openResult == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = saveDialog.FileName,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al exportar: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // === WINDOW CONTROLS ===

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Hay cambios sin guardar. ¿Desea salir sin guardar?",
                    "Cambios pendientes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            Close();
        }

        // === EDICIÓN INLINE ===

        private async void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel)
                return;

            var expense = e.Row.Item as ExpenseViewModel;
            if (expense == null)
                return;

            // Marcar que hay cambios pendientes
            _hasUnsavedChanges = true;

            // Guardar automáticamente después de editar
            await SaveExpenseChanges(expense);
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null) return;

            // Enter para confirmar y moverse a la siguiente fila
            if (e.Key == Key.Enter)
            {
                var currentColumn = grid.CurrentColumn;

                // Si hay una celda seleccionada y es editable
                if (currentColumn != null && !currentColumn.IsReadOnly)
                {
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);

                    // Moverse a la siguiente fila
                    if (grid.SelectedIndex < grid.Items.Count - 1)
                    {
                        grid.SelectedIndex++;
                        grid.CurrentCell = new DataGridCellInfo(
                            grid.Items[grid.SelectedIndex],
                            currentColumn);
                    }
                    e.Handled = true;
                }
            }
            // Escape para cancelar edición
            else if (e.Key == Key.Escape)
            {
                grid.CancelEdit(DataGridEditingUnit.Cell);
                grid.CancelEdit(DataGridEditingUnit.Row);
                e.Handled = true;
            }
            // F2 para entrar en modo edición
            else if (e.Key == Key.F2)
            {
                grid.BeginEdit();
                e.Handled = true;
            }
        }

        private async Task SaveExpenseChanges(ExpenseViewModel expense)
        {
            try
            {
                StatusText.Text = "Guardando cambios...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                // Obtener el gasto de la base de datos
                var expenseDb = await _supabaseService.GetExpenseById(expense.ExpenseId);
                if (expenseDb == null)
                {
                    MessageBox.Show("No se pudo encontrar el gasto", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Actualizar solo los campos editables
                expenseDb.Description = expense.Description;
                expenseDb.TotalExpense = expense.TotalExpense;
                expenseDb.ExpenseDate = expense.ExpenseDate;

                // Si cambió la fecha de compra, la fecha programada se recalculará por el trigger
                var success = await _supabaseService.UpdateExpense(expenseDb);

                if (success)
                {
                    _hasUnsavedChanges = false;
                    StatusText.Text = "Cambios guardados";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                    // Recargar para obtener la fecha programada actualizada si cambió
                    if (expense.ExpenseDate != expenseDb.ExpenseDate)
                    {
                        await LoadExpenses();
                        ApplyFilters();
                        UpdateStatistics();
                    }
                }
                else
                {
                    StatusText.Text = "Error al guardar";
                    StatusText.Foreground = new SolidColorBrush(Colors.Red);
                    MessageBox.Show("No se pudieron guardar los cambios", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error al guardar";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"Error al guardar cambios: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Validación para campos numéricos
        private void TotalAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números y punto decimal
            var textBox = sender as System.Windows.Controls.TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            decimal value;
            e.Handled = !decimal.TryParse(fullText, out value);
        }
    }
}