using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;
using Supabase.Gotrue;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Data;
using System.Windows.Input;

namespace SistemaGestionProyectos2.Views
{
    public partial class PayrollManagementView : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<PayrollViewModel> _employees;
        private ObservableCollection<FixedExpenseViewModel> _expenses;
        private UserSession _currentUser;
        private string _searchText = ""; // Variable para el texto de búsqueda
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX"); //Para el formato de moneda
        private bool _isAddingNewExpense = false;
        private FixedExpenseViewModel _currentEditingExpense = null;


        public PayrollManagementView(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _employees = new ObservableCollection<PayrollViewModel>();
            _expenses = new ObservableCollection<FixedExpenseViewModel>();

            // Conectar evento de búsqueda
            SearchEmployeeBox.TextChanged += SearchEmployeeBox_TextChanged;

            PayrollDataGrid.ItemsSource = _employees;
            ExpensesDataGrid.ItemsSource = _expenses;

            // Cargar datos de manera asíncrona
            _ = LoadData(); // Usar discard para llamada asíncrona


        }

        private async Task LoadData()
        {
            try
            {
                // Mostrar mes actual
                CurrentMonthText.Text = DateTime.Now.ToString("MMMM yyyy");

                // Cargar empleados
                await LoadEmployees();

                // Cargar gastos fijos
                await LoadFixedExpenses();

                // Actualizar totales
                UpdateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchEmployeeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchEmployeeBox.Text?.ToLower() ?? "";

            // Obtener la vista de colección
            var view = CollectionViewSource.GetDefaultView(PayrollDataGrid.ItemsSource);

            if (view != null)
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    // Quitar filtro
                    view.Filter = null;
                }
                else
                {
                    // Aplicar filtro
                    view.Filter = obj =>
                    {
                        var employee = obj as PayrollViewModel;
                        if (employee == null) return false;

                        return employee.Employee.ToLower().Contains(_searchText) ||
                               employee.Title.ToLower().Contains(_searchText) ||
                               employee.Range.ToLower().Contains(_searchText);
                    };
                }

                view.Refresh();
            }
        }

        private async void DeactivateEmployee_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var employee = button?.DataContext as PayrollViewModel;

            if (employee != null)
            {
                var result = MessageBox.Show(
                    $"¿Está seguro de desactivar a {employee.Employee}?\n\n" +
                    "El empleado no aparecerá en la nómina activa pero se conservará su historial.",
                    "Confirmar desactivación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Usar el método específico de desactivación
                        bool success = await _supabaseService.DeactivateEmployee(employee.Id, _currentUser.Id);

                        if (success)
                        {
                            MessageBox.Show("Empleado desactivado correctamente",
                                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                            await LoadData();
                        }
                        else
                        {
                            MessageBox.Show("No se pudo desactivar el empleado",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al desactivar: {ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }


        private void UpdateTotals()
        {
            // Calcular totales (excluyendo filas nuevas sin datos)
            decimal totalPayroll = _employees.Sum(e => e.MonthlyPayroll);
            decimal totalExpenses = _expenses
                .Where(e => !e.IsNew || !string.IsNullOrWhiteSpace(e.Description))
                .Sum(e => e.MonthlyAmount);

            // Actualizar las cards superiores
            TotalPayrollText.Text = totalPayroll.ToString("C", _mexicanCulture);
            TotalExpensesCardText.Text = totalExpenses.ToString("C", _mexicanCulture);

            // Actualizar el total en el tab de gastos fijos
            if (TotalExpensesText != null)
                TotalExpensesText.Text = totalExpenses.ToString("C", _mexicanCulture);
        }


        // Reemplazar el método LoadEmployees con este:
        private async Task LoadEmployees()
        {
            try
            {
                _employees.Clear();

                var payrollList = await _supabaseService.GetActivePayroll();

                foreach (var payroll in payrollList)
                {
                    _employees.Add(new PayrollViewModel
                    {
                        Id = payroll.Id,
                        Employee = payroll.Employee,
                        Title = payroll.Title,
                        Range = payroll.Range,
                        Condition = payroll.Condition,
                        MonthlyPayroll = payroll.MonthlyPayroll ?? 0
                    });
                }

                ActiveEmployeesText.Text = _employees.Count.ToString();
                LastUpdateText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar empleados: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        
        private async void AddEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new EmployeeEditWindow(null, _currentUser);
            if (editWindow.ShowDialog() == true)
            {
                await LoadData(); // Ahora con await
            }
        }

        private async void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var employee = button?.DataContext as PayrollViewModel;
            if (employee != null)
            {
                var editWindow = new EmployeeEditWindow(employee, _currentUser);
                if (editWindow.ShowDialog() == true)
                {
                    await LoadData();
                }
            }
        }

        private void ViewEmployeeHistory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var employee = button?.DataContext as PayrollViewModel;
            if (employee != null)
            {
                var historyWindow = new PayrollHistoryWindow(employee.Id, employee.Employee);
                historyWindow.ShowDialog();
            }
        }

        private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new PayrollHistoryWindow(null, "TODOS");
            historyWindow.ShowDialog();
        }

        private void PayrollDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var employee = PayrollDataGrid.SelectedItem as PayrollViewModel;
            if (employee != null)
            {
                EditEmployee_Click(sender, null);
            }
        }

        

        private void EditExpense_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.DataContext as FixedExpenseViewModel;
            if (expense != null)
            {
                MessageBox.Show($"Editar gasto: {expense.Description}", "En desarrollo");
            }
        }

        
        private void ExportPayrollButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Exportar a Excel", "En desarrollo");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // METODOS PARA GASTOS FIJOS
        

        private void AddEmptyExpenseRow()
        {
            // Solo agregar si no hay una fila nueva ya
            if (!_expenses.Any(e => e.IsNew))
            {
                _expenses.Add(new FixedExpenseViewModel
                {
                    Id = 0,
                    ExpenseType = "OTROS",
                    Description = "",
                    MonthlyAmount = 0,
                    IsNew = true,
                    HasChanges = false
                });
            }
        }
        private async void ExpensesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var expense = e.Row.Item as FixedExpenseViewModel;
                if (expense == null) return;

                // Marcar como modificado si no es nuevo
                if (!expense.IsNew)
                {
                    expense.HasChanges = true;
                }

                // Programar el guardado
                _currentEditingExpense = expense;

                // Usar dispatcher para guardar después de que termine la edición
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await SaveExpenseIfValid(_currentEditingExpense);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        // Modificar el método AddExpenseButton_Click para mejor UX
        private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            // Verificar si ya existe una fila nueva vacía
            var existingNewRow = _expenses.FirstOrDefault(ex => ex.IsNew && string.IsNullOrWhiteSpace(ex.Description));

            if (existingNewRow != null)
            {
                // Si ya existe una fila vacía, enfocarla en la columna descripción
                ExpensesDataGrid.SelectedItem = existingNewRow;
                ExpensesDataGrid.ScrollIntoView(existingNewRow);

                // Enfocar específicamente en la columna de descripción (índice 1)
                ExpensesDataGrid.CurrentCell = new DataGridCellInfo(existingNewRow, ExpensesDataGrid.Columns[1]);
                ExpensesDataGrid.BeginEdit();

                // Mensaje de ayuda
                if (StatusText != null)
                    StatusText.Text = "Complete los datos del nuevo gasto";
            }
            else
            {
                // Si no existe, agregar una nueva y enfocarla
                var newExpense = new FixedExpenseViewModel
                {
                    Id = 0,
                    ExpenseType = "OTROS",
                    Description = "",
                    MonthlyAmount = 0,
                    IsNew = true,
                    HasChanges = false
                };

                _expenses.Add(newExpense);

                // Enfocar la nueva fila en la columna de descripción
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ExpensesDataGrid.SelectedItem = newExpense;
                    ExpensesDataGrid.ScrollIntoView(newExpense);
                    ExpensesDataGrid.CurrentCell = new DataGridCellInfo(newExpense, ExpensesDataGrid.Columns[1]);
                    ExpensesDataGrid.BeginEdit();
                }), System.Windows.Threading.DispatcherPriority.Background);

                if (StatusText != null)
                    StatusText.Text = "Ingrese los datos del nuevo gasto";
            }
        }

        // Modificar LoadFixedExpenses para NO agregar fila vacía automáticamente
        private async Task LoadFixedExpenses()
        {
            try
            {
                _expenses.Clear();

                var expensesList = await _supabaseService.GetActiveFixedExpenses();

                foreach (var expense in expensesList)
                {
                    _expenses.Add(new FixedExpenseViewModel
                    {
                        Id = expense.Id,
                        ExpenseType = expense.ExpenseType ?? "OTROS",
                        Description = expense.Description ?? "",
                        MonthlyAmount = expense.MonthlyAmount ?? 0,
                        IsNew = false,
                        HasChanges = false
                    });
                }

                // NO agregar fila vacía automáticamente
                // AddEmptyExpenseRow(); // Comentar o eliminar esta línea

                UpdateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar gastos fijos: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Modificar SaveExpenseIfValid para limpiar filas vacías después de guardar
        private async Task SaveExpenseIfValid(FixedExpenseViewModel expense)
        {
            if (expense == null) return;

            // Validar que tenga descripción
            if (string.IsNullOrWhiteSpace(expense.Description))
            {
                // Si es una fila nueva vacía, simplemente removerla
                if (expense.IsNew && expense.MonthlyAmount == 0)
                {
                    _expenses.Remove(expense);
                    UpdateTotals();
                }
                return;
            }

            // Validar monto
            if (expense.MonthlyAmount <= 0)
            {
                MessageBox.Show("El monto debe ser mayor a cero",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var expenseTable = new FixedExpenseTable
                {
                    Id = expense.Id,
                    ExpenseType = expense.ExpenseType,
                    Description = expense.Description.Trim(),
                    MonthlyAmount = expense.MonthlyAmount,
                    CreatedBy = _currentUser.Id
                };

                if (expense.IsNew)
                {
                    // Crear nuevo
                    var created = await _supabaseService.CreateFixedExpense(expenseTable);
                    if (created != null)
                    {
                        expense.Id = created.Id;
                        expense.IsNew = false;

                        // NO agregar nueva fila automáticamente
                        // El usuario debe hacer click en "Nuevo Gasto" si quiere agregar otro

                        if (StatusText != null)
                            StatusText.Text = "✅ Gasto agregado correctamente";
                    }
                }
                else if (expense.HasChanges)
                {
                    // Actualizar existente
                    await _supabaseService.UpdateFixedExpense(expenseTable);
                    expense.HasChanges = false;

                    if (StatusText != null)
                        StatusText.Text = "✅ Gasto actualizado";
                }

                UpdateTotals();

                // Limpiar mensaje después de 3 segundos
                if (StatusText != null)
                {
                    await Task.Delay(3000);
                    StatusText.Text = "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void DeleteExpense_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.DataContext as FixedExpenseViewModel;

            if (expense != null && !expense.IsNew)
            {
                var result = MessageBox.Show(
                    $"¿Está seguro de eliminar el gasto '{expense.Description}'?\n" +
                    $"Tipo: {expense.ExpenseType}\n" +
                    $"Monto: {expense.MonthlyAmount:C}",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool success = await _supabaseService.DeleteFixedExpense(expense.Id);

                        if (success)
                        {
                            _expenses.Remove(expense);
                            UpdateTotals();

                            StatusText.Text = "✅ Gasto eliminado";

                            // Limpiar mensaje
                            await Task.Delay(3000);
                            StatusText.Text = "";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar: {ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ExpensesDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null) return;

            // Si presiona Enter, confirmar edición y mover a siguiente celda
            if (e.Key == Key.Enter)
            {
                var currentCell = grid.CurrentCell;
                if (currentCell.Item != null)
                {
                    grid.CommitEdit(DataGridEditingUnit.Row, true);

                    // Mover a la siguiente fila si es la última columna
                    if (currentCell.Column == grid.Columns[grid.Columns.Count - 2]) // -2 porque la última es el botón eliminar
                    {
                        var currentIndex = grid.Items.IndexOf(currentCell.Item);
                        if (currentIndex < grid.Items.Count - 1)
                        {
                            grid.SelectedIndex = currentIndex + 1;
                            grid.CurrentCell = new DataGridCellInfo(
                                grid.Items[currentIndex + 1],
                                grid.Columns[0]);
                        }
                    }
                }
                e.Handled = true;
            }
            // Si presiona Escape, cancelar edición
            else if (e.Key == Key.Escape)
            {
                grid.CancelEdit();
                e.Handled = true;
            }
        }



        private void ExpensesDataGrid_AddingNewItem(object sender, AddingNewItemEventArgs e)
        {
            var newExpense = new FixedExpenseViewModel
            {
                Id = 0,
                ExpenseType = "OTROS",
                Description = "",
                MonthlyAmount = 0,
                IsNew = true,
                HasChanges = true
            };
            e.NewItem = newExpense;
        }

        private void ExpensesDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var expense = e.Row.Item as FixedExpenseViewModel;
            if (expense != null && !expense.IsNew)
            {
                expense.HasChanges = true;
            }
        }

        private async void ExpensesDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var expense = e.Row.Item as FixedExpenseViewModel;
                if (expense != null && (expense.IsNew || expense.HasChanges))
                {
                    await SaveExpense(expense);
                }
            }
        }

        private async Task SaveExpense(FixedExpenseViewModel expense)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expense.Description))
                {
                    MessageBox.Show("La descripción es obligatoria",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var expenseTable = new FixedExpenseTable
                {
                    Id = expense.Id,
                    ExpenseType = expense.ExpenseType,
                    Description = expense.Description,
                    MonthlyAmount = expense.MonthlyAmount,
                    CreatedBy = _currentUser.Id
                };

                if (expense.IsNew)
                {
                    var created = await _supabaseService.CreateFixedExpense(expenseTable);
                    if (created != null)
                    {
                        expense.Id = created.Id;
                        expense.IsNew = false;
                    }
                }
                else
                {
                    await _supabaseService.UpdateFixedExpense(expenseTable);
                }

                expense.HasChanges = false;
                UpdateTotals();

                MessageBox.Show("Gasto guardado correctamente",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }

    // ViewModels
    public class PayrollViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _employee;
        private string _title;
        private string _range;
        private string _condition;
        private decimal _monthlyPayroll;
        private bool _isVisible = true;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Employee
        {
            get => _employee;
            set { _employee = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Range
        {
            get => _range;
            set { _range = value; OnPropertyChanged(); }
        }

        public string Condition
        {
            get => _condition;
            set { _condition = value; OnPropertyChanged(); }
        }

        public decimal MonthlyPayroll
        {
            get => _monthlyPayroll;
            set { _monthlyPayroll = value; OnPropertyChanged(); }
        }

        // Propiedad para controlar visibilidad en la búsqueda
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FixedExpenseViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _expenseType = "OTROS";
        private string _description = "";
        private decimal _monthlyAmount;
        private bool _isNew;
        private bool _hasChanges;
        

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string ExpenseType
        {
            get => _expenseType;
            set { _expenseType = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public decimal MonthlyAmount
        {
            get => _monthlyAmount;
            set { _monthlyAmount = value; OnPropertyChanged(); }
        }

        public bool IsNew
        {
            get => _isNew;
            set { _isNew = value; OnPropertyChanged(); }
        }

        public bool HasChanges
        {
            get => _hasChanges;
            set { _hasChanges = value; OnPropertyChanged(); }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}