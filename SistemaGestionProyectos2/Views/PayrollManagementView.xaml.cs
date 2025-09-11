using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;
using Supabase.Gotrue;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Data;

namespace SistemaGestionProyectos2.Views
{
    public partial class PayrollManagementView : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<PayrollViewModel> _employees;
        private ObservableCollection<FixedExpenseViewModel> _expenses;
        private UserSession _currentUser;
        private string _searchText = ""; // Variable para el texto de búsqueda

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


        private async Task LoadFixedExpenses()
        {
            _expenses.Clear();

            // Datos de prueba
            _expenses.Add(new FixedExpenseViewModel
            {
                Id = 1,
                ExpenseType = "GASOLINA",
                Description = "RAM",
                MonthlyAmount = 9000,
                EffectiveFrom = DateTime.Now
            });

            _expenses.Add(new FixedExpenseViewModel
            {
                Id = 2,
                ExpenseType = "SEGURO",
                Description = "RAM",
                MonthlyAmount = 750,
                EffectiveFrom = DateTime.Now
            });
        }

        private void UpdateTotals()
        {
            // Calcular totales
            decimal totalPayroll = _employees.Sum(e => e.MonthlyPayroll);
            decimal totalExpenses = _expenses.Sum(e => e.MonthlyAmount);
            decimal grandTotal = totalPayroll + totalExpenses;

            // Actualizar UI
            TotalPayrollText.Text = totalPayroll.ToString("C");
            TotalExpensesText.Text = totalExpenses.ToString("C");

            FooterPayrollText.Text = totalPayroll.ToString("C");
            FooterExpensesText.Text = totalExpenses.ToString("C");
            FooterTotalText.Text = grandTotal.ToString("C");
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

        private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            // Abrir ventana de edición de gastos
            MessageBox.Show("Agregar nuevo gasto fijo", "En desarrollo");
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

        private void DeleteExpense_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var expense = button?.DataContext as FixedExpenseViewModel;
            if (expense != null)
            {
                var result = MessageBox.Show(
                    $"¿Está seguro de eliminar el gasto '{expense.Description}'?",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _expenses.Remove(expense);
                    UpdateTotals();
                }
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
        private string _expenseType;
        private string _description;
        private decimal _monthlyAmount;
        private DateTime _effectiveFrom;

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

        public DateTime EffectiveFrom
        {
            get => _effectiveFrom;
            set { _effectiveFrom = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}