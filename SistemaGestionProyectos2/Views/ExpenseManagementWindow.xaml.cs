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
        private bool _isCreatingNewExpense = false;
        private ExpenseViewModel _newExpenseRow = null;

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

            // Configurar eventos del DataGrid para edición
            ExpensesDataGrid.PreviewKeyDown += DataGrid_PreviewKeyDown;
            ExpensesDataGrid.CellEditEnding += DataGrid_CellEditEnding;
            ExpensesDataGrid.CurrentCellChanged += DataGrid_CurrentCellChanged;
            ExpensesDataGrid.BeginningEdit += DataGrid_BeginningEdit;
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
                        ExpenseCategory = expense.ExpenseCategory,
                        IsNew = false
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

            // No filtrar la fila nueva que se está creando
            if (_newExpenseRow != null && _isCreatingNewExpense)
            {
                _filteredExpenses.Add(_newExpenseRow);
            }

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

            // Ordenar por fecha descendente (excepto la nueva fila)
            filtered = filtered.Where(e => !e.IsNew)
                              .OrderByDescending(e => e.ExpenseDate)
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

                // Excluir filas nuevas no guardadas del cálculo
                var validExpenses = _filteredExpenses.Where(e => !e.IsNew);

                // Total General
                decimal totalGeneral = validExpenses.Sum(e => e.TotalExpense);
                TotalGeneralText.Text = totalGeneral.ToString("C2", culture);

                // Total Pendiente
                decimal totalPendiente = validExpenses
                    .Where(e => e.Status == "PENDIENTE")
                    .Sum(e => e.TotalExpense);
                TotalPendienteText.Text = totalPendiente.ToString("C2", culture);

                // Total Vencido
                decimal totalVencido = validExpenses
                    .Where(e => e.IsOverdue)
                    .Sum(e => e.TotalExpense);
                TotalVencidoText.Text = totalVencido.ToString("C2", culture);

                // Total Pagado
                decimal totalPagado = validExpenses
                    .Where(e => e.Status == "PAGADO")
                    .Sum(e => e.TotalExpense);
                TotalPagadoText.Text = totalPagado.ToString("C2", culture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando estadísticas: {ex.Message}");
            }
        }

        // === NUEVA FUNCIONALIDAD: CREAR GASTO INLINE ===

        private void NewExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreatingNewExpense)
            {
                MessageBox.Show(
                    "Complete o cancele el gasto actual antes de crear uno nuevo.",
                    "Aviso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                // Enfocar en la primera celda editable de la nueva fila
                if (_newExpenseRow != null)
                {
                    ExpensesDataGrid.SelectedItem = _newExpenseRow;
                    ExpensesDataGrid.ScrollIntoView(_newExpenseRow);
                    ExpensesDataGrid.Focus();
                }
                return;
            }

            try
            {
                _isCreatingNewExpense = true;
                NewExpenseButton.IsEnabled = false;

                // Crear nueva fila con valores por defecto
                _newExpenseRow = new ExpenseViewModel
                {
                    ExpenseId = 0, // ID temporal
                    SupplierId = 0,
                    SupplierName = "",
                    Description = "",
                    ExpenseDate = DateTime.Now,
                    TotalExpense = 0,
                    Status = "PENDIENTE",
                    IsNew = true,
                    IsEditing = true
                };

                // Insertar al inicio de la colección
                _filteredExpenses.Insert(0, _newExpenseRow);

                // Seleccionar y enfocar la nueva fila
                ExpensesDataGrid.SelectedItem = _newExpenseRow;
                ExpensesDataGrid.ScrollIntoView(_newExpenseRow);
                
                // Forzar el modo de edición en la primera columna editable
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ExpensesDataGrid.CurrentCell = new DataGridCellInfo(
                        _newExpenseRow, 
                        ExpensesDataGrid.Columns[0]); // Columna de Proveedor
                    ExpensesDataGrid.BeginEdit();
                }), System.Windows.Threading.DispatcherPriority.Background);

                StatusText.Text = "Creando nuevo gasto...";
                StatusText.Foreground = new SolidColorBrush(Colors.Blue);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al crear nuevo gasto: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                _isCreatingNewExpense = false;
                NewExpenseButton.IsEnabled = true;
            }
        }

        private async Task<bool> SaveNewExpense()
        {
            if (_newExpenseRow == null) return false;

            // Validar campos obligatorios
            if (_newExpenseRow.SupplierId <= 0)
            {
                MessageBox.Show("Debe seleccionar un proveedor", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_newExpenseRow.Description))
            {
                MessageBox.Show("La descripción es obligatoria", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (_newExpenseRow.TotalExpense <= 0)
            {
                MessageBox.Show("El monto debe ser mayor a cero", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                StatusText.Text = "Guardando nuevo gasto...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                // Crear el objeto para guardar en la base de datos
                var newExpenseDb = new ExpenseDb
                {
                    SupplierId = _newExpenseRow.SupplierId,
                    Description = _newExpenseRow.Description,
                    ExpenseDate = _newExpenseRow.ExpenseDate,
                    TotalExpense = _newExpenseRow.TotalExpense,
                    Status = "PENDIENTE",
                    ExpenseCategory = _newExpenseRow.ExpenseCategory,
                    OrderId = _newExpenseRow.OrderId
                };

                // Guardar en la base de datos
                var createdExpense = await _supabaseService.CreateExpense(newExpenseDb);
                


                if (createdExpense != null)
                {
                    // Actualizar el ViewModel con los datos guardados
                    _newExpenseRow.ExpenseId = createdExpense.Id;
                    _newExpenseRow.ScheduledDate = createdExpense.ScheduledDate;
                    _newExpenseRow.IsNew = false;
                    _newExpenseRow.IsEditing = false;

                    // Agregar a la colección principal
                    _expenses.Add(_newExpenseRow);

                    // Limpiar la referencia
                    _newExpenseRow = null;
                    _isCreatingNewExpense = false;
                    NewExpenseButton.IsEnabled = true;

                    StatusText.Text = "Gasto guardado exitosamente";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                    // Actualizar estadísticas
                    UpdateStatistics();

                    return true;
                }
                else
                {
                    throw new Exception("No se pudo crear el gasto");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar el gasto: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "Error al guardar";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);

                return false;
            }
        }

        private void CancelNewExpense()
        {
            if (_newExpenseRow != null && _isCreatingNewExpense)
            {
                _filteredExpenses.Remove(_newExpenseRow);
                _newExpenseRow = null;
                _isCreatingNewExpense = false;
                NewExpenseButton.IsEnabled = true;

                StatusText.Text = "Creación cancelada";
                StatusText.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        // === EVENT HANDLERS PARA EDICIÓN ===

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var expense = e.Row.Item as ExpenseViewModel;
            if (expense == null) return;

            // Si no es una fila nueva y la columna es de solo lectura, cancelar edición
            if (!expense.IsNew)
            {
                var column = e.Column;
                if (column.Header.ToString() == "PROVEEDOR" || 
                    column.Header.ToString() == "ORDEN" ||
                    column.Header.ToString() == "F. PROG." ||
                    column.Header.ToString() == "ESTADO" ||
                    column.Header.ToString() == "F. PAGO" ||
                    column.Header.ToString() == "MÉTODO")
                {
                    e.Cancel = true;
                }
            }
        }

        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            // Manejar la navegación entre celdas
            if (_isCreatingNewExpense && _newExpenseRow != null)
            {
                var currentCell = ExpensesDataGrid.CurrentCell;
                if (currentCell.Item == _newExpenseRow)
                {
                    // Asegurar que la celda actual sea editable
                    var column = currentCell.Column;
                    if (column != null && !column.IsReadOnly)
                    {
                        ExpensesDataGrid.BeginEdit();
                    }
                }
            }
        }

        private async void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel)
                return;

            var expense = e.Row.Item as ExpenseViewModel;
            if (expense == null)
                return;

            // Si es una fila existente (no nueva)
            if (!expense.IsNew)
            {
                _hasUnsavedChanges = true;
                await SaveExpenseChanges(expense);
            }
        }

        private async void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var selectedExpense = grid.SelectedItem as ExpenseViewModel;

            // Enter para confirmar
            if (e.Key == Key.Enter)
            {
                if (_isCreatingNewExpense && _newExpenseRow != null && selectedExpense == _newExpenseRow)
                {
                    // Confirmar y guardar la nueva fila
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);

                    var saved = await SaveNewExpense();
                    if (saved)
                    {
                        // Moverse a la siguiente fila después de guardar
                        if (grid.Items.Count > 1)
                        {
                            grid.SelectedIndex = 1;
                        }
                    }
                    e.Handled = true;
                }
                else
                {
                    // Comportamiento normal para filas existentes
                    var currentColumn = grid.CurrentColumn;
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
            }
            // Escape para cancelar
            else if (e.Key == Key.Escape)
            {
                if (_isCreatingNewExpense && _newExpenseRow != null && selectedExpense == _newExpenseRow)
                {
                    // Cancelar la creación de la nueva fila
                    grid.CancelEdit(DataGridEditingUnit.Cell);
                    grid.CancelEdit(DataGridEditingUnit.Row);
                    CancelNewExpense();
                    e.Handled = true;
                }
                else
                {
                    // Cancelar edición normal
                    grid.CancelEdit(DataGridEditingUnit.Cell);
                    grid.CancelEdit(DataGridEditingUnit.Row);
                    e.Handled = true;
                }
            }
            // Tab para navegar entre celdas
            else if (e.Key == Key.Tab)
            {
                // El comportamiento por defecto del Tab funciona bien
                // Solo asegurar que se confirmen los cambios
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
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

        // === OTROS EVENT HANDLERS ===

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isCreatingNewExpense)
            {
                ApplyFilters();
                UpdateStatistics();
            }
        }

        private void SupplierFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_expenses != null && !_isCreatingNewExpense)
            {
                ApplyFilters();
                UpdateStatistics();
            }
        }

        private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_expenses != null && !_isCreatingNewExpense)
            {
                ApplyFilters();
                UpdateStatistics();
            }
        }

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoading && _expenses != null && !_isCreatingNewExpense)
            {
                _ = LoadDataAsync();
            }
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreatingNewExpense)
            {
                MessageBox.Show(
                    "Complete o cancele el gasto actual antes de limpiar los filtros.",
                    "Aviso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SearchBox.Clear();
            SupplierFilterCombo.SelectedIndex = 0;
            StatusFilterCombo.SelectedIndex = 0;
            FromDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
            ToDatePicker.SelectedDate = DateTime.Now;

            _ = LoadDataAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreatingNewExpense)
            {
                MessageBox.Show(
                    "Complete o cancele el gasto actual antes de actualizar.",
                    "Aviso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await LoadDataAsync();
        }

        private void ExpensesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedExpense = ExpensesDataGrid.SelectedItem as ExpenseViewModel;
        }

        private async void PayButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as ExpenseViewModel;

            if (expense == null || expense.IsPaid || expense.IsNew) return;

            try
            {
                var result = MessageBox.Show(
                    $"¿Confirmar pago de ${expense.TotalExpense:N2} a {expense.SupplierName}?\n\n" +
                    $"Descripción: {expense.Description}\n" +
                    $"Se registrará con fecha de hoy.",
                    "Confirmar Pago",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                string payMethod = "TRANSFERENCIA"; // Por defecto

                var success = await _supabaseService.MarkExpenseAsPaid(
                    expense.ExpenseId,
                    DateTime.Now,
                    payMethod);

                if (success)
                {
                    expense.Status = "PAGADO";
                    expense.PaidDate = DateTime.Now;
                    expense.PayMethod = payMethod;

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

            if (expense == null || expense.IsNew) return;

            // Por ahora mantener el diálogo para edición completa
            // En el futuro se puede eliminar si toda la edición es inline
            try
            {
                var dialog = new NewExpenseDialog(expense);
                if (dialog.ShowDialog() == true && dialog.Result != null)
                {
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

            if (expense == null || expense.IsNew) return;

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

                    sb.AppendLine("ID,Proveedor,Descripción,Categoría,Fecha Compra,Total,Fecha Programada,Estado,Fecha Pago,Método Pago");

                    foreach (var expense in _filteredExpenses.Where(e => !e.IsNew))
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

                    sb.AppendLine();
                    sb.AppendLine("RESUMEN");
                    
                    var validExpenses = _filteredExpenses.Where(e => !e.IsNew);
                    sb.AppendLine($"Total General,{validExpenses.Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Total Pendiente,{validExpenses.Where(e => e.Status == "PENDIENTE").Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Total Pagado,{validExpenses.Where(e => e.Status == "PAGADO").Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Total Vencido,{validExpenses.Where(e => e.IsOverdue).Sum(e => e.TotalExpense).ToString("F2", culture)}");
                    sb.AppendLine($"Cantidad de Registros,{validExpenses.Count()}");

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                    MessageBox.Show(
                        "Reporte exportado exitosamente",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

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

        // === EVENTOS ADICIONALES PARA LA EDICIÓN INLINE ===

        private void SupplierCombo_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null && _suppliers != null)
            {
                combo.ItemsSource = _suppliers.OrderBy(s => s.SupplierName);
            }
        }


        // Cargar órdenes recientes en el ComboBox
        private async void OrderCombo_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null)
            {
                try
                {
                    // Cargar las últimas 10 órdenes
                    var orders = await _supabaseService.GetRecentOrders(10);

                    // Agregar opción vacía
                    var orderList = new List<dynamic> { new { Id = (int?)null, Display = "Sin orden" } };
                    orderList.AddRange(orders.Select(o => new {
                        Id = (int?)o.Id,
                        Display = $"{o.Po} - {o.Description?.Substring(0, Math.Min(30, o.Description?.Length ?? 0))}"
                    }));

                    combo.ItemsSource = orderList;
                    combo.DisplayMemberPath = "Display";
                    combo.SelectedValuePath = "Id";
                    combo.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cargando órdenes: {ex.Message}");
                }
            }
        }

        private void SupplierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var expense = combo?.DataContext as ExpenseViewModel;
            
            if (expense != null && combo?.SelectedItem != null)
            {
                var selectedSupplier = combo.SelectedItem as SupplierDb;
                if (selectedSupplier != null)
                {
                    expense.SupplierId = selectedSupplier.Id;
                    expense.SupplierName = selectedSupplier.SupplierName;
                }
            }
        }

        private async void SaveNewButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.Tag as ExpenseViewModel;

            if (expense != null && expense.IsNew)
            {
                // Confirmar la edición actual
                ExpensesDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                ExpensesDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                // Guardar el gasto
                await SaveNewExpense();
            }
        }

        // Validación para campos numéricos
        private void TotalAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            decimal value;
            e.Handled = !decimal.TryParse(fullText, out value);
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
            if (_hasUnsavedChanges || _isCreatingNewExpense)
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
    }
}