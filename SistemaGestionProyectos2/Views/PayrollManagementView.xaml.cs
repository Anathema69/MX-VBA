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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class PayrollManagementView : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<PayrollViewModel> _employees;
        private ObservableCollection<PayrollViewModel> _filteredEmployees;
        private ObservableCollection<FixedExpenseViewModel> _expenses;
        private UserSession _currentUser;
        private string _searchText = "";
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");

        // Para edición de gastos
        private FixedExpenseViewModel _currentEditingExpense = null;
        private bool _isEditingExpense = false;

        public PayrollManagementView(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _employees = new ObservableCollection<PayrollViewModel>();
            _filteredEmployees = new ObservableCollection<PayrollViewModel>();
            _expenses = new ObservableCollection<FixedExpenseViewModel>();

            // Conectar evento de búsqueda
            SearchEmployeeBox.TextChanged += SearchEmployeeBox_TextChanged;

            // Usar ItemsControl en lugar de DataGrid para el nuevo diseño
            PayrollItemsControl.ItemsSource = _filteredEmployees;
            ExpensesItemsControl.ItemsSource = _expenses;

            // Suscribir a cambios en las colecciones para actualizar automáticamente
            _expenses.CollectionChanged += (s, e) => UpdateTotals();

            // Cargar datos de manera asíncrona
            _ = LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                // Mostrar mes actual
                CurrentMonthText.Text = DateTime.Now.ToString("MMMM yyyy", _mexicanCulture).ToUpper();

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
            ApplyEmployeeFilter();
        }

        private void ApplyEmployeeFilter()
        {
            _filteredEmployees.Clear();

            var filtered = string.IsNullOrWhiteSpace(_searchText)
                ? _employees
                : _employees.Where(emp =>
                    emp.Employee.ToLower().Contains(_searchText) ||
                    emp.Title.ToLower().Contains(_searchText) ||
                    emp.Range.ToLower().Contains(_searchText) ||
                    emp.Condition.ToLower().Contains(_searchText));

            foreach (var employee in filtered)
            {
                _filteredEmployees.Add(employee);
            }
        }

        private async Task LoadEmployees()
        {
            try
            {
                _employees.Clear();
                _filteredEmployees.Clear();

                var payrollList = await _supabaseService.GetActivePayroll();

                foreach (var payroll in payrollList)
                {
                    var employee = new PayrollViewModel
                    {
                        Id = payroll.Id,
                        Employee = payroll.Employee,
                        Title = payroll.Title,
                        Range = payroll.Range,
                        Condition = payroll.Condition,
                        MonthlyPayroll = payroll.MonthlyPayroll ?? 0,
                        // Generar iniciales para el avatar
                        EmployeeInitials = GetInitials(payroll.Employee)
                    };

                    _employees.Add(employee);
                    _filteredEmployees.Add(employee);
                }

                ActiveEmployeesText.Text = _employees.Count.ToString();
                LastUpdateText.Text = $"Última actualización: {DateTime.Now:dd/MM/yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar empleados: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "??";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            else if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

            return "??";
        }

        private async Task LoadFixedExpenses()
        {
            try
            {
                _expenses.Clear();

                var expensesList = await _supabaseService.GetActiveFixedExpenses();

                foreach (var expense in expensesList)
                {
                    var vm = new FixedExpenseViewModel
                    {
                        Id = expense.Id,
                        ExpenseType = expense.ExpenseType ?? "OTROS",
                        Description = expense.Description ?? "",
                        MonthlyAmount = expense.MonthlyAmount ?? 0,
                        OriginalAmount = expense.MonthlyAmount ?? 0,
                        IsNew = false,
                        HasChanges = false
                    };

                    // Suscribir a cambios de propiedades para detectar modificaciones
                    vm.PropertyChanged += OnExpensePropertyChanged;
                    _expenses.Add(vm);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar gastos fijos: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExpensePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is FixedExpenseViewModel expense)
            {
                if (e.PropertyName == nameof(FixedExpenseViewModel.MonthlyAmount) ||
                    e.PropertyName == nameof(FixedExpenseViewModel.Description) ||
                    e.PropertyName == nameof(FixedExpenseViewModel.ExpenseType))
                {
                    if (!expense.IsNew)
                    {
                        expense.HasChanges = true;
                    }
                    UpdateTotals();
                }
            }
        }

        private void UpdateTotals()
        {
            decimal totalPayroll = _employees.Sum(e => e.MonthlyPayroll);
            decimal totalExpenses = _expenses
                .Where(e => !e.IsNew || !string.IsNullOrWhiteSpace(e.Description))
                .Sum(e => e.MonthlyAmount);

            decimal grandTotal = totalPayroll + totalExpenses;

            // Actualizar las cards superiores
            TotalPayrollText.Text = totalPayroll.ToString("C", _mexicanCulture);
            TotalExpensesCardText.Text = totalExpenses.ToString("C", _mexicanCulture);

            // Actualizar el total general en el footer
            if (TotalExpensesText != null)
                TotalExpensesText.Text = grandTotal.ToString("C", _mexicanCulture);
        }

        // Eventos de Empleados
        private async void AddEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new EmployeeEditWindow(null, _currentUser);
            if (editWindow.ShowDialog() == true)
            {
                await LoadData();
                ShowStatus("Empleado agregado exitosamente", true);
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
                    ShowStatus("Empleado actualizado exitosamente", true);
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
                        bool success = await _supabaseService.DeactivateEmployee(employee.Id, _currentUser.Id);

                        if (success)
                        {
                            await LoadData();
                            ShowStatus("Empleado desactivado correctamente", true);
                        }
                        else
                        {
                            ShowStatus("No se pudo desactivar el empleado", false);
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

        // Eventos de Gastos
        private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            var newExpense = new FixedExpenseViewModel
            {
                Id = 0,
                ExpenseType = "OTROS",
                Description = "",
                MonthlyAmount = 0,
                OriginalAmount = 0,
                IsNew = true,
                HasChanges = false
            };

            newExpense.PropertyChanged += OnExpensePropertyChanged;

            // Insertar al principio en lugar de al final
            _expenses.Insert(0, newExpense);

            // Hacer scroll al principio automáticamente
            if (ExpensesItemsControl != null)
            {
                var scrollViewer = FindScrollViewer(ExpensesItemsControl);
                scrollViewer?.ScrollToTop();
            }

            ShowStatus("Complete los datos del nuevo gasto", true);
        }
        // Método auxiliar para encontrar el ScrollViewer
        private ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private async void SaveExpense_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.DataContext as FixedExpenseViewModel;

            if (expense == null) return;

            // Validaciones
            if (string.IsNullOrWhiteSpace(expense.Description))
            {
                MessageBox.Show("La descripción es obligatoria",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (expense.MonthlyAmount <= 0)
            {
                MessageBox.Show("El monto debe ser mayor a 0",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            // Deshabilitar el botón mientras se guarda
            button.IsEnabled = false;
            try
            {
                // Mostrar indicador de carga
                ShowStatus("Guardando cambios...", true);

                DateTime effectiveDate;

                // Determinar si es corrección o cambio futuro
                bool isCorrection = false;

                var container = GetItemContainer(expense);
                if (container != null)
                {
                    var nextMonthRadio = FindNextMonthRadio(container);

                    if (nextMonthRadio?.IsChecked == true)
                    {
                        // Cambio futuro
                        effectiveDate = new DateTime(
                            DateTime.Now.AddMonths(1).Year,
                            DateTime.Now.AddMonths(1).Month, 1);
                    }
                    else
                    {
                        // Corrección o aplicación inmediata
                        effectiveDate = new DateTime(
                            DateTime.Now.Year,
                            DateTime.Now.Month, 1);
                        isCorrection = true;
                    }
                }
                else
                {
                    effectiveDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    isCorrection = true;
                }

                var expenseTable = new FixedExpenseTable
                {
                    Id = expense.Id,
                    ExpenseType = expense.ExpenseType,
                    Description = expense.Description,
                    MonthlyAmount = expense.MonthlyAmount,
                    IsActive = true,
                    CreatedBy = _currentUser.Id,
                    EffectiveDate = effectiveDate
                };

                bool success = false;
                string successMessage = "";

                if (expense.IsNew)
                {
                    // Nuevo gasto
                    var created = await _supabaseService.CreateFixedExpense(expenseTable);
                    if (created != null)
                    {
                        expense.Id = created.Id;
                        expense.IsNew = false;
                        expense.OriginalAmount = expense.MonthlyAmount;
                        expense.HasChanges = false;
                        success = true;
                        successMessage = $"Gasto creado: {expense.MonthlyAmount:C}/mes desde {effectiveDate:MMMM yyyy}";
                    }
                }
                else if (expense.HasChanges)
                {
                    // Verificar si es un cambio significativo
                    var difference = Math.Abs(expense.MonthlyAmount - expense.OriginalAmount);

                    if (difference > 0.01m)
                    {
                        if (isCorrection)
                        {
                            // Corrección inmediata - actualizar el registro principal
                            await _supabaseService.UpdateFixedExpense(expenseTable);
                            successMessage = $"Monto corregido a {expense.MonthlyAmount:C}";
                        }
                        else
                        {
                            // Cambio programado - crear entrada en historial
                            success = await _supabaseService.SaveFixedExpenseWithEffectiveDate(
                                expenseTable,
                                effectiveDate,
                                _currentUser.Id);
                            successMessage = $"Cambio programado: {expense.MonthlyAmount:C} desde {effectiveDate:MMMM yyyy}";
                        }

                        expense.OriginalAmount = expense.MonthlyAmount;
                        expense.HasChanges = false;
                        success = true;
                    }
                    else
                    {
                        // Solo cambió descripción o tipo
                        await _supabaseService.UpdateFixedExpense(expenseTable);
                        expense.HasChanges = false;
                        success = true;
                        successMessage = "Detalles actualizados";
                    }
                }

                if (success)
                {
                    UpdateTotals();
                    ShowStatus(successMessage, true);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", false);
            }
            finally
            {
                // Rehabilitar el botón
                button.IsEnabled = true;
            }
        }

        private RadioButton FindNextMonthRadio(DependencyObject container)
        {
            if (container == null) return null;

            // Buscar todos los RadioButtons en el contenedor
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(container); i++)
            {
                var child = VisualTreeHelper.GetChild(container, i);

                // Si encontramos un RadioButton con Name que contiene "NextMonth"
                if (child is RadioButton radioButton && radioButton.Name == "NextMonthRadio")
                {
                    return radioButton;
                }

                // Buscar recursivamente
                var result = FindNextMonthRadio(child);
                if (result != null)
                    return result;
            }

            return null;
        }


        // Método auxiliar para actualizar balances futuros (opcional, para refrescar UI si tienes una vista de balance)
        private async Task UpdateFutureBalances(DateTime effectiveDate)
        {
            try
            {
                // Este método es opcional - úsalo si tienes una vista de balance que necesita actualizarse
                // Por ahora solo actualizamos los totales locales
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando balances: {ex.Message}");
            }
        }



        // Método para obtener el contenedor de un item en el ItemsControl
        private DependencyObject GetItemContainer(object item)
        {
            if (ExpensesItemsControl == null || item == null)
                return null;

            var container = ExpensesItemsControl.ItemContainerGenerator.ContainerFromItem(item);
            return container;
        }

        // Método genérico para buscar un control hijo por tipo y nombre
        private T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // Si es del tipo correcto
                if (child is T typedChild)
                {
                    // Si no especificamos nombre o coincide el nombre
                    if (string.IsNullOrEmpty(childName))
                    {
                        return typedChild;
                    }
                    else if (child is FrameworkElement fe && fe.Name == childName)
                    {
                        return typedChild;
                    }
                }

                // Buscar recursivamente en los hijos
                var foundChild = FindVisualChild<T>(child, childName);
                if (foundChild != null)
                    return foundChild;
            }

            return null;
        }

        private async void DeleteExpense_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.DataContext as FixedExpenseViewModel;

            if (expense == null || expense.IsNew) return;

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el gasto '{expense.Description}'?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _supabaseService.DeactivateFixedExpense(expense.Id);

                    if (success)
                    {
                        _expenses.Remove(expense);
                        UpdateTotals();

                        ShowStatus("Gasto eliminado correctamente", true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
                StatusText.Foreground = isSuccess
                    ? FindResource("SuccessColor") as System.Windows.Media.Brush
                    : FindResource("DangerColor") as System.Windows.Media.Brush;

                // Limpiar mensaje después de 5 segundos
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(5);
                timer.Tick += (s, e) =>
                {
                    StatusText.Text = "";
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Método para manejar el evento KeyDown en los campos de gastos
        private void ExpenseField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                var expense = textBox?.DataContext as FixedExpenseViewModel;

                if (expense != null && (expense.IsNew || expense.HasChanges))
                {
                    e.Handled = true;
                    SaveExpense_Click(textBox, null);
                }
            }
            else if (e.Key == Key.Escape && _isEditingExpense)
            {
                // Cancelar edición
                CancelExpenseEdit();
            }
        }

        // Método para establecer el modo de edición
        private void BeginExpenseEdit(FixedExpenseViewModel expense)
        {
            if (_isEditingExpense && _currentEditingExpense != expense)
            {
                MessageBox.Show("Complete o guarde el gasto actual antes de editar otro.",
                    "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isEditingExpense = true;
            _currentEditingExpense = expense;
        }

        private void EndExpenseEdit()
        {
            _isEditingExpense = false;
            _currentEditingExpense = null;
        }

        private void CancelExpenseEdit()
        {
            if (_currentEditingExpense != null)
            {
                if (_currentEditingExpense.IsNew)
                {
                    _expenses.Remove(_currentEditingExpense);
                }
                else
                {
                    // Restaurar valores originales
                    _currentEditingExpense.MonthlyAmount = _currentEditingExpense.OriginalAmount;
                    _currentEditingExpense.HasChanges = false;
                }
            }
            EndExpenseEdit();
        }
    }

    // ViewModels actualizados
    public class PayrollViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _employee;
        private string _title;
        private string _range;
        private string _condition;
        private decimal _monthlyPayroll;
        private string _employeeInitials;

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

        public string EmployeeInitials
        {
            get => _employeeInitials;
            set { _employeeInitials = value; OnPropertyChanged(); }
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
        private decimal _originalAmount;
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
            set
            {
                _expenseType = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public decimal MonthlyAmount
        {
            get => _monthlyAmount;
            set
            {
                _monthlyAmount = value;
                OnPropertyChanged();
            }
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

        public decimal OriginalAmount
        {
            get => _originalAmount;
            set
            {
                _originalAmount = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}