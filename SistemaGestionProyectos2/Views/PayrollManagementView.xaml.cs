using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
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
using System.Text.RegularExpressions;
using System.Windows.Threading;

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
        private bool _isAddingOrEditingExpense = false;

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
                    (emp.Employee ?? "").ToLower().Contains(_searchText) ||
                    (emp.Title ?? "").ToLower().Contains(_searchText) ||
                    (emp.Range ?? "").ToLower().Contains(_searchText) ||
                    (emp.Condition ?? "").ToLower().Contains(_searchText));

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
                        _isAddingOrEditingExpense = true;
                    }
                    UpdateTotals();
                }
            }
        }

        private void UpdateTotals()
        {
            var culture = new CultureInfo("es-MX");

            decimal totalPayroll = _employees.Sum(e => e.MonthlyPayroll);
            decimal totalExpenses = _expenses
                .Where(e => !e.IsNew || !string.IsNullOrWhiteSpace(e.Description))
                .Sum(e => e.MonthlyAmount);

            decimal grandTotal = totalPayroll + totalExpenses;

            // Actualizar las cards superiores
            TotalPayrollText.Text = totalPayroll.ToString("C", culture);
            TotalExpensesCardText.Text = totalExpenses.ToString("C", culture);

            // Actualizar el total general en el footer
            if (TotalExpensesText != null)
                TotalExpensesText.Text = grandTotal.ToString("C", culture);
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
            // Verificar si ya estamos agregando o editando un gasto
            if (_isAddingOrEditingExpense)
            {
                // Buscar si hay un gasto nuevo sin guardar
                var newUnsaved = _expenses.FirstOrDefault(x => x.IsNew);
                if (newUnsaved != null)
                {
                    

                    // Enfocar el gasto pendiente
                    FocusExpenseField(newUnsaved, "DescriptionTextBox");
                    return;
                }

                // Buscar si hay un gasto con cambios
                var withChanges = _expenses.FirstOrDefault(x => x.HasChanges);
                if (withChanges != null)
                {
                   

                    // Enfocar el gasto con cambios
                    FocusExpenseField(withChanges, "DescriptionTextBox");
                    return;
                }
            }

            // Marcar que estamos agregando
            _isAddingOrEditingExpense = true;

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

            // Insertar al principio
            _expenses.Insert(0, newExpense);

            // Hacer scroll al principio
            if (ExpensesItemsControl != null)
            {
                var scrollViewer = FindScrollViewer(ExpensesItemsControl);
                scrollViewer?.ScrollToTop();

                // Enfocar el campo de descripción del nuevo gasto
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FocusExpenseField(newExpense, "DescriptionTextBox");
                }), DispatcherPriority.Render);
            }

            ShowStatus("Complete los datos del nuevo gasto (ESC cancelar, ENTER guardar)", true);
        }



        // Método auxiliar para encontrar el primer TextBox en un contenedor
        private TextBox FindFirstTextBox(DependencyObject parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBox textBox && textBox.IsEnabled)
                {
                    return textBox;
                }

                var result = FindFirstTextBox(child);
                if (result != null)
                    return result;
            }

            return null;
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

            // Validaciones mejoradas
            if (string.IsNullOrWhiteSpace(expense.Description))
            {
                MessageBox.Show("La descripción es obligatoria",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Enfocar el campo de descripción
                var container = GetItemContainer(expense);
                if (container != null)
                {
                    var descriptionBox = FindVisualChild<TextBox>(container, "DescriptionTextBox");
                    descriptionBox?.Focus();
                }
                return;
            }

            if (expense.MonthlyAmount <= 0)
            {
                MessageBox.Show("El monto debe ser mayor a 0",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Enfocar el campo de monto
                var container = GetItemContainer(expense);
                if (container != null)
                {
                    var amountBox = FindVisualChild<TextBox>(container, "AmountTextBox");
                    amountBox?.Focus();
                    amountBox?.SelectAll();
                }
                return;
            }

            // Deshabilitar el botón mientras se guarda
            button.IsEnabled = false;

            try
            {
                ShowStatus("Guardando cambios...", true);

                DateTime effectiveDate;
                bool isCorrection = false;

                var container = GetItemContainer(expense);
                if (container != null)
                {
                    var nextMonthRadio = FindNextMonthRadio(container);

                    if (nextMonthRadio?.IsChecked == true)
                    {
                        effectiveDate = new DateTime(
                            DateTime.Now.AddMonths(1).Year,
                            DateTime.Now.AddMonths(1).Month, 1);
                    }
                    else
                    {
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
                    var created = await _supabaseService.CreateFixedExpense(expenseTable);
                    if (created != null)
                    {
                        expense.Id = created.Id;
                        expense.IsNew = false;
                        expense.OriginalAmount = expense.MonthlyAmount;
                        expense.HasChanges = false;
                        success = true;
                        successMessage = $"Gasto creado: {expense.Description} - {expense.MonthlyAmount:C}/mes";

                        // Quitar el foco después de guardar exitosamente
                        Keyboard.ClearFocus();
                    }
                }
                else if (expense.HasChanges)
                {
                    var difference = Math.Abs(expense.MonthlyAmount - expense.OriginalAmount);

                    if (difference > 0.01m)
                    {
                        if (isCorrection)
                        {
                            await _supabaseService.UpdateFixedExpense(expenseTable);
                            successMessage = $"Monto corregido a {expense.MonthlyAmount:C}";
                        }
                        else
                        {
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
                        await _supabaseService.UpdateFixedExpense(expenseTable);
                        expense.HasChanges = false;
                        success = true;
                        successMessage = "Detalles actualizados";
                    }

                    // Quitar el foco después de guardar
                    Keyboard.ClearFocus();
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
        private T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName) || typedChild.Name == childName)
                    {
                        return typedChild;
                    }
                }

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
        private async void ExpenseField_KeyDown(object sender, KeyEventArgs e)
        {
            var element = sender as FrameworkElement;
            var expense = element?.DataContext as FixedExpenseViewModel;

            if (expense == null) return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // Validaciones antes de guardar
                if (string.IsNullOrWhiteSpace(expense.Description))
                {
                    MessageBox.Show("La descripción es obligatoria",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Enfocar campo de descripción
                    FocusExpenseField(expense, "DescriptionTextBox");
                    return;
                }

                if (expense.MonthlyAmount <= 0)
                {
                    MessageBox.Show("El monto debe ser mayor a 0",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Enfocar campo de monto
                    FocusExpenseField(expense, "AmountTextBox");
                    return;
                }

                // Guardar el gasto
                await SaveExpenseDirectly(expense);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;

                if (expense.IsNew)
                {
                    // Cancelar nuevo gasto sin preguntar
                    _expenses.Remove(expense);
                    UpdateTotals();
                    ShowStatus("Nuevo gasto cancelado", false);
                    _isAddingOrEditingExpense = false;
                }
                else if (expense.HasChanges)
                {
                    // Revertir cambios
                    await RevertExpenseChanges(expense);
                    ShowStatus("Cambios revertidos", false);
                    _isAddingOrEditingExpense = false;
                }

                // Quitar el foco del campo actual
                Keyboard.ClearFocus();
            }
            else if (e.Key == Key.Tab)
            {
                // Manejar navegación con Tab
                var control = sender as Control;
                if (control != null)
                {
                    e.Handled = true;

                    // Determinar dirección (Shift+Tab va hacia atrás)
                    bool goBack = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                    NavigateToNextExpenseField(expense, control, goBack);
                }
            }
        }

        private async Task SaveExpenseDirectly(FixedExpenseViewModel expense)
        {
            if (expense == null) return;

            try
            {
                ShowStatus("Guardando gasto...", true);

                DateTime effectiveDate = DateTime.Now;
                bool isCorrection = false;

                // Buscar las opciones de fecha
                var container = GetItemContainer(expense);
                if (container != null)
                {
                    var nextMonthRadio = FindNextMonthRadio(container);
                    if (nextMonthRadio?.IsChecked == true)
                    {
                        effectiveDate = new DateTime(
                            DateTime.Now.AddMonths(1).Year,
                            DateTime.Now.AddMonths(1).Month, 1);
                    }
                    else
                    {
                        effectiveDate = new DateTime(
                            DateTime.Now.Year,
                            DateTime.Now.Month, 1);
                        isCorrection = true;
                    }
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
                var culture = new CultureInfo("es-MX");

                if (expense.IsNew)
                {
                    var created = await _supabaseService.CreateFixedExpense(expenseTable);
                    if (created != null)
                    {
                        expense.Id = created.Id;
                        expense.IsNew = false;
                        expense.OriginalAmount = expense.MonthlyAmount;
                        expense.HasChanges = false;
                        success = true;
                        successMessage = $"✓ Gasto creado: {expense.Description} - {expense.MonthlyAmount.ToString("C", culture)}";
                        _isAddingOrEditingExpense = false;
                    }
                }
                else if (expense.HasChanges)
                {
                    var difference = Math.Abs(expense.MonthlyAmount - expense.OriginalAmount);

                    if (difference > 0.01m)
                    {
                        if (isCorrection)
                        {
                            await _supabaseService.UpdateFixedExpense(expenseTable);
                            successMessage = $"✓ Monto actualizado: {expense.MonthlyAmount.ToString("C", culture)}";
                        }
                        else
                        {
                            success = await _supabaseService.SaveFixedExpenseWithEffectiveDate(
                                expenseTable, effectiveDate, _currentUser.Id);
                            successMessage = $"✓ Cambio programado: {expense.MonthlyAmount.ToString("C", culture)} desde {effectiveDate.ToString("MMMM yyyy", culture)}";
                        }
                    }
                    else
                    {
                        await _supabaseService.UpdateFixedExpense(expenseTable);
                        successMessage = "✓ Detalles actualizados";
                    }

                    expense.OriginalAmount = expense.MonthlyAmount;
                    expense.HasChanges = false;
                    success = true;
                    _isAddingOrEditingExpense = false;
                }

                if (success)
                {
                    UpdateTotals();
                    ShowStatus(successMessage, true);
                    Keyboard.ClearFocus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error al guardar: {ex.Message}", false);
            }
        }

        // Método para revertir cambios de un gasto
        private async Task RevertExpenseChanges(FixedExpenseViewModel expense)
        {
            if (expense == null || expense.IsNew) return;

            try
            {
                // Recargar valores originales desde la base de datos
                var originalExpense = await _supabaseService.GetFixedExpenseById(expense.Id);
                if (originalExpense != null)
                {
                    expense.ExpenseType = originalExpense.ExpenseType ?? "OTROS";
                    expense.Description = originalExpense.Description ?? "";
                    expense.MonthlyAmount = originalExpense.MonthlyAmount ?? 0;
                    expense.OriginalAmount = originalExpense.MonthlyAmount ?? 0;
                    expense.HasChanges = false;
                }

                UpdateTotals();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error revirtiendo cambios: {ex.Message}");
            }
        }


        // Método para navegar entre campos del gasto
        private void NavigateToNextExpenseField(FixedExpenseViewModel expense, Control currentControl, bool goBack = false)
        {
            var container = GetItemContainer(expense);
            if (container == null) return;

            string currentName = currentControl.Name;
            string nextFieldName = "";

            if (!goBack)
            {
                // Navegación hacia adelante
                switch (currentName)
                {
                    case "ExpenseTypeCombo":
                        nextFieldName = "DescriptionTextBox";
                        break;
                    case "DescriptionTextBox":
                        nextFieldName = "AmountTextBox";
                        break;
                    case "AmountTextBox":
                        // Último campo, mantener foco o guardar
                        return;
                }
            }
            else
            {
                // Navegación hacia atrás
                switch (currentName)
                {
                    case "AmountTextBox":
                        nextFieldName = "DescriptionTextBox";
                        break;
                    case "DescriptionTextBox":
                        nextFieldName = "ExpenseTypeCombo";
                        break;
                    case "ExpenseTypeCombo":
                        // Primer campo, mantener foco
                        return;
                }
            }

            if (!string.IsNullOrEmpty(nextFieldName))
            {
                FocusExpenseField(expense, nextFieldName);
            }
        }

        // Método auxiliar para enfocar un campo específico
        private void FocusExpenseField(FixedExpenseViewModel expense, string fieldName)
        {
            var container = GetItemContainer(expense);
            if (container == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var field = FindVisualChild<FrameworkElement>(container, fieldName);
                if (field != null)
                {
                    if (field is TextBox textBox)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                    else if (field is ComboBox comboBox)
                    {
                        comboBox.Focus();
                    }
                }
            }), DispatcherPriority.Render);
        }

        // Método para manejar el formato decimal del monto - CORREGIDO
        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Permitir solo números y punto decimal
            if (e.Text == ".")
            {
                // Permitir punto si no existe ya uno
                e.Handled = textBox.Text.Contains(".");
            }
            else
            {
                // Solo permitir dígitos
                e.Handled = !char.IsDigit(e.Text[0]);
            }

            // Verificar límite de decimales
            if (!e.Handled && textBox.Text.Contains("."))
            {
                var parts = textBox.Text.Split('.');
                if (parts.Length > 1 && parts[1].Length >= 2)
                {
                    // Si ya hay 2 decimales y el cursor está después del punto
                    var caretIndex = textBox.CaretIndex;
                    var dotIndex = textBox.Text.IndexOf('.');
                    if (caretIndex > dotIndex)
                    {
                        e.Handled = true;
                    }
                }
            }
        }

        // Método para manejar teclas especiales en el campo de monto
        private void AmountTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Permitir el punto del teclado numérico
            if (e.Key == Key.Decimal || e.Key == Key.OemPeriod)
            {
                // Si ya hay un punto, no permitir otro
                if (textBox.Text.Contains("."))
                {
                    e.Handled = true;
                }
            }
            // Permitir teclas de navegación y edición
            else if (e.Key == Key.Back || e.Key == Key.Delete ||
                     e.Key == Key.Left || e.Key == Key.Right ||
                     e.Key == Key.Home || e.Key == Key.End)
            {
                // Permitir estas teclas
                return;
            }
        }

        // Método para cuando el campo de monto obtiene el foco
        private void AmountTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var expense = textBox.DataContext as FixedExpenseViewModel;
            if (expense == null) return;

            // Marcar que estamos editando
            _isAddingOrEditingExpense = true;

            // Si el monto es 0, limpiar el campo
            if (expense.MonthlyAmount == 0)
            {
                textBox.Text = "";
            }
            else
            {
                // Formatear sin símbolo de moneda para edición
                textBox.Text = expense.MonthlyAmount.ToString("0.00");
            }

            // Seleccionar todo el texto para facilitar el reemplazo
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
            }), DispatcherPriority.Render);
        }

        // Método para cuando el campo de monto pierde el foco
        private void AmountTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var expense = textBox.DataContext as FixedExpenseViewModel;
            if (expense == null) return;

            // Parsear el valor ingresado
            if (decimal.TryParse(textBox.Text, out decimal value))
            {
                expense.MonthlyAmount = value;
                // Formatear con 2 decimales
                textBox.Text = value.ToString("0.00");
            }
            else if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                expense.MonthlyAmount = 0;
                textBox.Text = "0.00";
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