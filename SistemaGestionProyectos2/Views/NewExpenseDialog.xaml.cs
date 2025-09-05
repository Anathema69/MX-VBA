using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewExpenseDialog : Window
    {
        private readonly SupabaseService _supabaseService;
        private ExpenseViewModel _expenseToEdit;
        private List<SupplierDb> _suppliers;
        private List<OrderDb> _orders;
        private bool _isEditMode;

        public ExpenseViewModel Result { get; private set; }

        // Constructor para nuevo gasto
        public NewExpenseDialog()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _isEditMode = false;
            InitializeDialog();
        }

        // Constructor para editar gasto existente
        public NewExpenseDialog(ExpenseViewModel expenseToEdit) : this()
        {
            _expenseToEdit = expenseToEdit;
            _isEditMode = true;
            TitleText.Text = "EDITAR GASTO";
            LoadExpenseData();
        }

        private async void InitializeDialog()
        {
            try
            {
                // Cargar proveedores
                await LoadSuppliers();

                // Cargar órdenes activas
                await LoadOrders();

                // Establecer fecha por defecto
                ExpenseDatePicker.SelectedDate = DateTime.Now;

                // Configurar eventos
                SupplierCombo.SelectionChanged += SupplierCombo_SelectionChanged;
                ExpenseDatePicker.SelectedDateChanged += ExpenseDatePicker_SelectedDateChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inicializando diálogo: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSuppliers()
        {
            try
            {
                _suppliers = await _supabaseService.GetActiveSuppliers();
                SupplierCombo.ItemsSource = _suppliers;

                // Si no hay proveedores, ofrecer crear uno
                if (!_suppliers.Any())
                {
                    var result = MessageBox.Show(
                        "No hay proveedores registrados. ¿Desea agregar uno ahora?",
                        "Sin Proveedores",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Abrir diálogo para crear proveedor
                        // TODO: Implementar NewSupplierDialog
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando proveedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadOrders()
        {
            try
            {
                // Cargar órdenes activas (no completadas)
                var supabaseClient = _supabaseService.GetClient();
                var response = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Filter("f_orderstat", Postgrest.Constants.Operator.NotEqual, 4) // No completadas
                    .Order("f_po", Postgrest.Constants.Ordering.Descending)
                    .Get();

                _orders = response?.Models ?? new List<OrderDb>();

                // Agregar opción vacía al inicio
                var ordersWithEmpty = new List<OrderDb> { new OrderDb { Id = 0, Po = "-- Sin orden --" } };
                ordersWithEmpty.AddRange(_orders);

                OrderCombo.ItemsSource = ordersWithEmpty;
                OrderCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando órdenes: {ex.Message}");
                // No es crítico si no se pueden cargar las órdenes
            }
        }

        private void LoadExpenseData()
        {
            if (_expenseToEdit == null) return;

            // Cargar datos del gasto a editar
            DescriptionBox.Text = _expenseToEdit.Description;
            ExpenseDatePicker.SelectedDate = _expenseToEdit.ExpenseDate;
            TotalAmountBox.Text = _expenseToEdit.TotalExpense.ToString("F2");

            // Seleccionar proveedor
            if (_suppliers != null)
            {
                var supplier = _suppliers.FirstOrDefault(s => s.Id == _expenseToEdit.SupplierId);
                if (supplier != null)
                {
                    SupplierCombo.SelectedItem = supplier;
                }
            }

            // Seleccionar categoría
            if (!string.IsNullOrEmpty(_expenseToEdit.ExpenseCategory))
            {
                foreach (ComboBoxItem item in CategoryCombo.Items)
                {
                    if (item.Content.ToString() == _expenseToEdit.ExpenseCategory)
                    {
                        CategoryCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            // Seleccionar orden si existe
            if (_expenseToEdit.OrderId.HasValue && _orders != null)
            {
                var order = _orders.FirstOrDefault(o => o.Id == _expenseToEdit.OrderId.Value);
                if (order != null)
                {
                    OrderCombo.SelectedItem = order;
                }
            }

            // Si está pagado, mostrar método de pago
            if (_expenseToEdit.IsPaid)
            {
                PaymentPanel.Visibility = Visibility.Visible;
                foreach (ComboBoxItem item in PayMethodCombo.Items)
                {
                    if (item.Content.ToString() == _expenseToEdit.PayMethod)
                    {
                        PayMethodCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SupplierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateScheduledDate();
        }

        private void ExpenseDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateScheduledDate();
        }

        private void UpdateScheduledDate()
        {
            if (SupplierCombo.SelectedItem is SupplierDb supplier &&
                ExpenseDatePicker.SelectedDate.HasValue)
            {
                var scheduledDate = ExpenseDatePicker.SelectedDate.Value.AddDays(supplier.CreditDays);
                ScheduledDateText.Text = scheduledDate.ToString("dd/MM/yyyy");
                CreditDaysText.Text = $"{supplier.CreditDays} días";

                // Cambiar color si ya está vencido
                if (scheduledDate < DateTime.Now.Date)
                {
                    ScheduledDateText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(244, 67, 54)); // Rojo
                }
                else
                {
                    ScheduledDateText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(51, 51, 51)); // Negro
                }
            }
        }

        private void AmountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números y punto decimal
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            var newText = TotalAmountBox.Text.Insert(TotalAmountBox.SelectionStart, e.Text);
            e.Handled = !regex.IsMatch(newText);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validar campos obligatorios
            if (SupplierCombo.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar un proveedor", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
            {
                MessageBox.Show("Debe ingresar una descripción", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ExpenseDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Debe seleccionar una fecha de compra", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(TotalAmountBox.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Debe ingresar un monto válido mayor a 0", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Guardando...";

                var supplier = SupplierCombo.SelectedItem as SupplierDb;
                var selectedOrder = OrderCombo.SelectedItem as OrderDb;

                if (_isEditMode)
                {
                    // Actualizar gasto existente
                    var expense = await _supabaseService.GetExpenseById(_expenseToEdit.ExpenseId);

                    expense.SupplierId = supplier.Id;
                    expense.Description = DescriptionBox.Text.Trim();
                    expense.ExpenseDate = ExpenseDatePicker.SelectedDate.Value;
                    expense.TotalExpense = amount;
                    expense.ScheduledDate = ExpenseDatePicker.SelectedDate.Value.AddDays(supplier.CreditDays);
                    expense.OrderId = selectedOrder?.Id > 0 ? selectedOrder.Id : null;

                    if (CategoryCombo.SelectedItem is ComboBoxItem categoryItem)
                    {
                        expense.ExpenseCategory = categoryItem.Content.ToString();
                    }

                    if (PaymentPanel.Visibility == Visibility.Visible &&
                        PayMethodCombo.SelectedItem is ComboBoxItem methodItem)
                    {
                        expense.PayMethod = methodItem.Content.ToString();
                    }

                    var success = await _supabaseService.UpdateExpense(expense);

                    if (success)
                    {
                        Result = ConvertToViewModel(expense, supplier);
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("Error al actualizar el gasto", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Crear nuevo gasto
                    var newExpense = new ExpenseDb
                    {
                        SupplierId = supplier.Id,
                        Description = DescriptionBox.Text.Trim(),
                        ExpenseDate = ExpenseDatePicker.SelectedDate.Value,
                        TotalExpense = amount,
                        OrderId = selectedOrder?.Id > 0 ? selectedOrder.Id : null
                    };

                    if (CategoryCombo.SelectedItem is ComboBoxItem categoryItem)
                    {
                        newExpense.ExpenseCategory = categoryItem.Content.ToString();
                    }

                    var createdExpense = await _supabaseService.CreateExpense(newExpense, supplier.CreditDays);

                    if (createdExpense != null)
                    {
                        Result = ConvertToViewModel(createdExpense, supplier);
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("Error al crear el gasto", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "Guardar";
            }
        }

        private ExpenseViewModel ConvertToViewModel(ExpenseDb expense, SupplierDb supplier)
        {
            return new ExpenseViewModel
            {
                ExpenseId = expense.Id,
                SupplierId = expense.SupplierId,
                SupplierName = supplier.SupplierName,
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
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}