using SistemaGestionProyectos2.Controls;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class SupplierPendingDetailView : Window, INotifyPropertyChanged
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private int _supplierId;
        private string _supplierName;
        private ObservableCollection<ExpenseDetailViewModel> _expenses;
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");
        private bool _isCreatingNewExpense = false;
        private int _supplierCreditDays = 0;
        private bool _isSupplierSelectionMode = false;
        private string _currentStatusFilter = "PENDIENTE"; // Filtro actual: PENDIENTE, PAGADO, TODOS
        private bool _supplierSelectionConfirmed = false; // Para evitar selección automática al escribir

        // Lista de órdenes disponibles para el ComboBox
        private ObservableCollection<OrderDisplayItem> _availableOrders;
        private List<OrderDisplayItem> _allOrders = new List<OrderDisplayItem>(); // Lista completa para filtrado
        public ObservableCollection<OrderDisplayItem> AvailableOrders
        {
            get => _availableOrders;
            set { _availableOrders = value; OnPropertyChanged(); }
        }

        // Lista de proveedores para el selector
        private ObservableCollection<SupplierDisplayItem> _availableSuppliers;
        public ObservableCollection<SupplierDisplayItem> AvailableSuppliers
        {
            get => _availableSuppliers;
            set { _availableSuppliers = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _startInCreateMode = false;

        /// <summary>
        /// Constructor para modo CON proveedor preseleccionado
        /// </summary>
        public SupplierPendingDetailView(UserSession currentUser, int supplierId, string supplierName, bool startInCreateMode = false)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supplierId = supplierId;
            _supplierName = supplierName;
            _startInCreateMode = startInCreateMode;
            _isSupplierSelectionMode = false;
            _supabaseService = SupabaseService.Instance;
            _expenses = new ObservableCollection<ExpenseDetailViewModel>();
            _availableOrders = new ObservableCollection<OrderDisplayItem>();
            _availableSuppliers = new ObservableCollection<SupplierDisplayItem>();

            // Establecer DataContext para bindings
            this.DataContext = this;

            ExpensesDataGrid.ItemsSource = _expenses;

            // Modo con proveedor: mostrar info del proveedor, ocultar selector
            SupplierInfoPanel.Visibility = Visibility.Visible;
            SupplierSelectorPanel.Visibility = Visibility.Collapsed;

            // Configurar información del proveedor en el header
            SupplierNameText.Text = supplierName;
            SetSupplierInitials(supplierName);

            // Maximizar ventana respetando la barra de tareas
            MaximizeWithTaskbar();

            // Cargar datos del proveedor, órdenes y gastos
            _ = LoadSupplierDataAsync();
            _ = LoadAvailableOrdersAsync();
            _ = InitializeWithFilterAsync();

            // Manejar teclas Enter/Escape
            this.PreviewKeyDown += SupplierPendingDetailView_KeyDown;
        }

        /// <summary>
        /// Constructor para modo SIN proveedor (selector visible)
        /// </summary>
        public SupplierPendingDetailView(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supplierId = 0;
            _supplierName = "";
            _startInCreateMode = true; // Siempre iniciar en modo creación
            _isSupplierSelectionMode = true;
            _supabaseService = SupabaseService.Instance;
            _expenses = new ObservableCollection<ExpenseDetailViewModel>();
            _availableOrders = new ObservableCollection<OrderDisplayItem>();
            _availableSuppliers = new ObservableCollection<SupplierDisplayItem>();

            // Establecer DataContext para bindings
            this.DataContext = this;

            ExpensesDataGrid.ItemsSource = _expenses;

            // Modo selector: ocultar info del proveedor, mostrar selector
            SupplierInfoPanel.Visibility = Visibility.Collapsed;
            SupplierSelectorPanel.Visibility = Visibility.Visible;

            // Deshabilitar botón nuevo gasto hasta seleccionar proveedor
            NewExpenseButton.IsEnabled = false;

            // Mostrar mensaje de selección de proveedor
            NoSupplierSelectedMessage.Visibility = Visibility.Visible;
            ExpensesDataGrid.Visibility = Visibility.Collapsed;

            // Inicializar cards con valores vacíos
            UpdateSummaryCards(0, 0, 0, 0);
            CreditDaysText.Text = "--";

            // Maximizar ventana respetando la barra de tareas
            MaximizeWithTaskbar();

            // Cargar proveedores y órdenes disponibles
            _ = LoadAvailableSuppliersAsync();
            _ = LoadAvailableOrdersAsync();

            // Manejar teclas Enter/Escape
            this.PreviewKeyDown += SupplierPendingDetailView_KeyDown;
        }

        private async Task InitializeWithFilterAsync()
        {
            // Determinar filtro inicial según los datos del proveedor
            await DetermineInitialFilterAsync();

            await LoadExpensesAsync();

            // Si se abrió en modo creación, iniciar automáticamente
            if (_startInCreateMode)
            {
                await Task.Delay(100); // Pequeño delay para que la UI termine de renderizar
                Dispatcher.Invoke(() => NewExpenseButton_Click(null, null));
            }
        }

        private async Task LoadAvailableOrdersAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Limit(100)
                    .Get();

                var orders = ordersResponse?.Models ?? new List<OrderDb>();

                // Guardar lista completa para filtrado
                _allOrders.Clear();
                _allOrders.Add(new OrderDisplayItem { Id = 0, DisplayText = "-- Sin orden --" });

                foreach (var order in orders)
                {
                    _allOrders.Add(new OrderDisplayItem
                    {
                        Id = order.Id,
                        DisplayText = $"{order.Po ?? $"ORD-{order.Id}"}"
                    });
                }

                // Copiar a la lista observable
                AvailableOrders.Clear();
                foreach (var item in _allOrders)
                {
                    AvailableOrders.Add(item);
                }
            }
            catch { }
        }

        private async Task LoadAvailableSuppliersAsync()
        {
            try
            {
                DetailStatusText.Text = "Cargando proveedores...";

                var supabaseClient = _supabaseService.GetClient();

                // Cargar proveedores activos
                var suppliersResponse = await supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.IsActive == true)
                    .Order("f_suppliername", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var suppliers = suppliersResponse?.Models ?? new List<SupplierDb>();

                // Cargar gastos pendientes para mostrar indicador
                var pendingExpenses = await supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Status == "PENDIENTE")
                    .Get();

                var expensesBySupplier = (pendingExpenses?.Models ?? new List<ExpenseDb>())
                    .GroupBy(e => e.SupplierId)
                    .ToDictionary(g => g.Key, g => g.Count());

                AvailableSuppliers.Clear();

                foreach (var supplier in suppliers)
                {
                    var pendingCount = expensesBySupplier.ContainsKey(supplier.Id)
                        ? expensesBySupplier[supplier.Id]
                        : 0;

                    var displayText = pendingCount > 0
                        ? $"{supplier.SupplierName} ({pendingCount} pend.)"
                        : supplier.SupplierName;

                    AvailableSuppliers.Add(new SupplierDisplayItem
                    {
                        Id = supplier.Id,
                        SupplierName = supplier.SupplierName,
                        DisplayText = displayText,
                        CreditDays = supplier.CreditDays,
                        PendingCount = pendingCount
                    });
                }

                // Asignar al ComboBox
                SupplierComboBox.ItemsSource = AvailableSuppliers;

                DetailStatusText.Text = "Seleccione un proveedor para continuar";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar proveedores: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DetailStatusText.Text = "Error al cargar proveedores";
            }
        }

        #region Selector de Proveedor - Confirmación con Enter o Clic

        /// <summary>
        /// Se ejecuta cuando el ComboBox de proveedor se carga
        /// </summary>
        private void SupplierComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Nada especial por ahora, pero reservado para futuras mejoras
        }

        /// <summary>
        /// Detecta cuando el usuario presiona Enter para confirmar selección
        /// </summary>
        private void SupplierComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var comboBox = sender as ComboBox;
                if (comboBox == null) return;

                // Si hay un item seleccionado, confirmar
                if (comboBox.SelectedItem != null)
                {
                    _supplierSelectionConfirmed = true;
                    ConfirmSupplierSelection();
                    e.Handled = true;
                }
                // Si no hay item seleccionado pero hay texto, buscar coincidencia exacta
                else if (!string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    var match = AvailableSuppliers?.FirstOrDefault(s =>
                        s.DisplayText.Contains(comboBox.Text, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        comboBox.SelectedItem = match;
                        _supplierSelectionConfirmed = true;
                        ConfirmSupplierSelection();
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Detecta cuando el dropdown se cierra (usuario hizo clic en un item)
        /// </summary>
        private void SupplierComboBox_DropDownClosed(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // Si hay un item seleccionado y el dropdown se cerró por clic
            if (comboBox.SelectedItem != null)
            {
                _supplierSelectionConfirmed = true;
                ConfirmSupplierSelection();
            }
        }

        /// <summary>
        /// Procesa la selección del proveedor solo cuando el usuario confirma
        /// </summary>
        private async void ConfirmSupplierSelection()
        {
            if (!_supplierSelectionConfirmed) return;

            var selectedSupplier = SupplierComboBox.SelectedItem as SupplierDisplayItem;

            // Cancelar cualquier edición en progreso al cambiar de proveedor
            CancelPendingEdit();

            if (selectedSupplier == null || selectedSupplier.Id == 0)
            {
                // No hay proveedor seleccionado
                _supplierId = 0;
                _supplierName = "";
                _supplierCreditDays = 0;
                NewExpenseButton.IsEnabled = false;
                _expenses.Clear();
                UpdateSummaryCards(0, 0, 0, 0);
                CreditDaysText.Text = "--";

                // Ocultar info del proveedor seleccionado
                if (SelectedSupplierInfoPanel != null)
                    SelectedSupplierInfoPanel.Visibility = Visibility.Collapsed;

                // Mostrar mensaje, ocultar DataGrid
                NoSupplierSelectedMessage.Visibility = Visibility.Visible;
                ExpensesDataGrid.Visibility = Visibility.Collapsed;

                _supplierSelectionConfirmed = false;
                return;
            }

            // Proveedor seleccionado - ocultar mensaje, mostrar DataGrid
            NoSupplierSelectedMessage.Visibility = Visibility.Collapsed;
            ExpensesDataGrid.Visibility = Visibility.Visible;

            // Proveedor seleccionado
            _supplierId = selectedSupplier.Id;
            _supplierName = selectedSupplier.SupplierName;
            _supplierCreditDays = selectedSupplier.CreditDays;
            CreditDaysText.Text = _supplierCreditDays.ToString();

            // Mostrar info del proveedor seleccionado
            if (SelectedSupplierInfoPanel != null)
            {
                SelectedSupplierInfoPanel.Visibility = Visibility.Visible;
                SelectedCreditDaysText.Text = _supplierCreditDays.ToString();
            }

            // Habilitar botón de nuevo gasto
            NewExpenseButton.IsEnabled = true;

            // Cargar gastos del proveedor (si los tiene)
            await LoadExpensesAsync();

            // Si el proveedor no tiene gastos pendientes, iniciar automáticamente el modo creación
            if (_expenses.Count == 0 && !_isCreatingNewExpense)
            {
                await Task.Delay(150);
                Dispatcher.Invoke(() => NewExpenseButton_Click(null, null));
            }

            // Resetear bandera
            _supplierSelectionConfirmed = false;
        }

        #endregion

        /// <summary>
        /// Cancela cualquier edición pendiente sin mostrar mensajes
        /// </summary>
        private void CancelPendingEdit()
        {
            if (_isCreatingNewExpense)
            {
                var newExpense = _expenses.FirstOrDefault(x => x.IsNew);
                if (newExpense != null)
                {
                    _expenses.Remove(newExpense);
                }
                _isCreatingNewExpense = false;
                NewExpenseButton.IsEnabled = true;
                DetailStatusText.Text = "Listo";
            }
        }

        private void UpdateSummaryCards(decimal totalPending, decimal totalOverdue, decimal totalDueSoon, int expenseCount)
        {
            DetailTotalPendingText.Text = totalPending.ToString("C", _cultureMX);
            DetailTotalOverdueText.Text = totalOverdue.ToString("C", _cultureMX);
            DetailTotalDueSoonText.Text = totalDueSoon.ToString("C", _cultureMX);
            DetailExpenseCountText.Text = expenseCount.ToString();
        }

        private void FilterPendiente_Click(object sender, RoutedEventArgs e)
        {
            SetStatusFilter("PENDIENTE");
        }

        private void FilterPagado_Click(object sender, RoutedEventArgs e)
        {
            SetStatusFilter("PAGADO");
        }

        private void FilterTodos_Click(object sender, RoutedEventArgs e)
        {
            SetStatusFilter("TODOS");
        }

        private void SetStatusFilter(string status)
        {
            if (_currentStatusFilter == status) return;

            _currentStatusFilter = status;
            UpdateFilterButtonStyles();
            _ = LoadExpensesAsync();
        }

        private void UpdateFilterButtonStyles()
        {
            // Actualizar estilos de los botones de filtro
            FilterPendienteBtn.Tag = _currentStatusFilter == "PENDIENTE" ? "Active" : null;
            FilterPagadoBtn.Tag = _currentStatusFilter == "PAGADO" ? "Active" : null;
            FilterTodosBtn.Tag = _currentStatusFilter == "TODOS" ? "Active" : null;
        }

        private async Task DetermineInitialFilterAsync()
        {
            // Verificar si el proveedor solo tiene gastos pagados (ninguno pendiente)
            // En ese caso, mostrar TODOS para que no se vea vacío
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                var pendingCount = await supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.SupplierId == _supplierId && e.Status == "PENDIENTE")
                    .Count(Postgrest.Constants.CountType.Exact);

                if (pendingCount == 0)
                {
                    // No hay pendientes, verificar si hay algún gasto
                    var totalCount = await supabaseClient
                        .From<ExpenseDb>()
                        .Where(e => e.SupplierId == _supplierId)
                        .Count(Postgrest.Constants.CountType.Exact);

                    if (totalCount > 0)
                    {
                        // Hay gastos pero ninguno pendiente, mostrar TODOS
                        _currentStatusFilter = "TODOS";
                    }
                }

                UpdateFilterButtonStyles();
            }
            catch { }
        }

        private async Task LoadSupplierDataAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                var supplier = await supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == _supplierId)
                    .Single();

                if (supplier != null)
                {
                    _supplierCreditDays = supplier.CreditDays;
                }
            }
            catch { }
        }

        private void SupplierPendingDetailView_KeyDown(object sender, KeyEventArgs e)
        {
            // F2 para editar la fila seleccionada (solo si no está pagado)
            if (e.Key == Key.F2 && !_isCreatingNewExpense)
            {
                var selectedExpense = ExpensesDataGrid.SelectedItem as ExpenseDetailViewModel;
                if (selectedExpense != null && !selectedExpense.IsNew && !selectedExpense.IsEditing && !selectedExpense.IsPaid)
                {
                    StartInlineEdit(selectedExpense);
                    e.Handled = true;
                }
                return;
            }

            // Solo procesar Enter/Escape si hay edición en progreso
            if (!_isCreatingNewExpense) return;

            if (e.Key == Key.Enter)
            {
                SaveNewExpense_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelNewExpense_Click(null, null);
                e.Handled = true;
            }
        }

        private void ExpensesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Doble clic para editar (solo si no está pagado)
            if (_isCreatingNewExpense) return;

            var selectedExpense = ExpensesDataGrid.SelectedItem as ExpenseDetailViewModel;
            if (selectedExpense != null && !selectedExpense.IsNew && !selectedExpense.IsEditing && !selectedExpense.IsPaid)
            {
                StartInlineEdit(selectedExpense);
            }
        }

        private void StartInlineEdit(ExpenseDetailViewModel expense)
        {
            // Activar modo edición inline
            expense.CreditDays = _supplierCreditDays;
            expense.TotalInput = expense.Total.ToString();
            expense.IsEditing = true;
            _isCreatingNewExpense = true;

            // Buscar el SelectedOrderId basado en OrderPO
            var matchingOrder = AvailableOrders.FirstOrDefault(o => o.DisplayText == expense.OrderPO);
            expense.SelectedOrderId = matchingOrder?.Id ?? 0;

            NewExpenseButton.IsEnabled = false;
            DetailStatusText.Text = "Editando gasto... (Enter para guardar, Escape para cancelar)";
        }

        private void MaximizeWithTaskbar()
        {
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
        }

        private void SetSupplierInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                SupplierInitialsText.Text = "??";
                return;
            }

            var words = name.Split(' ');
            if (words.Length >= 2)
                SupplierInitialsText.Text = $"{words[0][0]}{words[1][0]}".ToUpper();
            else
                SupplierInitialsText.Text = name.Substring(0, Math.Min(2, name.Length)).ToUpper();
        }

        private async Task LoadExpensesAsync()
        {
            try
            {
                DetailStatusText.Text = "Cargando gastos...";

                var supabaseClient = _supabaseService.GetClient();

                // Obtener información del proveedor para días de crédito
                var supplierResponse = await supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == _supplierId)
                    .Single();

                var supplier = supplierResponse;
                var creditDays = supplier?.CreditDays ?? 0;
                CreditDaysText.Text = creditDays.ToString();

                // Obtener gastos del proveedor según filtro
                Postgrest.Responses.ModeledResponse<ExpenseDb> expensesResponse;

                if (_currentStatusFilter == "TODOS")
                {
                    expensesResponse = await supabaseClient
                        .From<ExpenseDb>()
                        .Where(e => e.SupplierId == _supplierId)
                        .Order("f_expensedate", Postgrest.Constants.Ordering.Descending)
                        .Get();
                }
                else
                {
                    expensesResponse = await supabaseClient
                        .From<ExpenseDb>()
                        .Where(e => e.SupplierId == _supplierId && e.Status == _currentStatusFilter)
                        .Order("f_expensedate", Postgrest.Constants.Ordering.Descending)
                        .Get();
                }

                var expenses = expensesResponse?.Models ?? new List<ExpenseDb>();

                // Obtener órdenes para mostrar el PO
                var orderIds = expenses.Where(e => e.OrderId.HasValue).Select(e => e.OrderId.Value).Distinct().ToList();
                Dictionary<int, string> orderPoDict = new Dictionary<int, string>();

                if (orderIds.Any())
                {
                    var ordersResponse = await supabaseClient
                        .From<OrderDb>()
                        .Filter("f_order", Postgrest.Constants.Operator.In, orderIds)
                        .Get();

                    var orders = ordersResponse?.Models ?? new List<OrderDb>();
                    orderPoDict = orders.ToDictionary(o => o.Id, o => o.Po ?? $"ORD-{o.Id}");
                }

                _expenses.Clear();

                decimal totalPending = 0;
                decimal totalOverdue = 0;
                decimal totalDueSoon = 0;

                DateTime today = DateTime.Today;
                DateTime dueSoonDate = today.AddDays(7);

                foreach (var expense in expenses)
                {
                    var viewModel = new ExpenseDetailViewModel
                    {
                        ExpenseId = expense.Id,
                        Description = expense.Description ?? "Sin descripción",
                        OrderPO = expense.OrderId.HasValue && orderPoDict.ContainsKey(expense.OrderId.Value)
                            ? orderPoDict[expense.OrderId.Value]
                            : "-",
                        SelectedOrderId = expense.OrderId ?? 0,
                        Total = expense.TotalExpense,
                        TotalFormatted = expense.TotalExpense.ToString("C", _cultureMX),
                        ExpenseDate = expense.ExpenseDate,
                        PayMethod = expense.PayMethod ?? "TRANSFERENCIA",
                        PaidDate = expense.PaidDate,
                        CreditDays = creditDays
                    };

                    // Calcular fecha de vencimiento
                    DateTime? dueDate = expense.ScheduledDate;
                    if (!dueDate.HasValue)
                    {
                        dueDate = expense.ExpenseDate.AddDays(creditDays);
                    }

                    viewModel.DueDate = dueDate.Value;

                    // Calcular días restantes/vencidos
                    int daysDiff = (dueDate.Value.Date - today).Days;
                    viewModel.DaysRemaining = daysDiff;

                    if (daysDiff < 0)
                    {
                        viewModel.DaysText = $"{Math.Abs(daysDiff)} días vencido";
                        viewModel.Status = "VENCIDO";
                        totalOverdue += expense.TotalExpense;
                    }
                    else if (daysDiff <= 7)
                    {
                        viewModel.DaysText = daysDiff == 0 ? "Hoy" : $"{daysDiff} días";
                        viewModel.Status = "POR VENCER";
                        totalDueSoon += expense.TotalExpense;
                    }
                    else
                    {
                        viewModel.DaysText = $"{daysDiff} días";
                        viewModel.Status = "AL CORRIENTE";
                    }

                    totalPending += expense.TotalExpense;
                    _expenses.Add(viewModel);
                }

                // Ordenar por estado (vencidos primero) y luego por días
                var sorted = _expenses
                    .OrderBy(e => e.Status == "VENCIDO" ? 0 : e.Status == "POR VENCER" ? 1 : 2)
                    .ThenBy(e => e.DueDate)
                    .ToList();

                _expenses.Clear();
                foreach (var item in sorted)
                {
                    _expenses.Add(item);
                }

                // Actualizar totales
                DetailTotalPendingText.Text = totalPending.ToString("C", _cultureMX);
                DetailTotalOverdueText.Text = totalOverdue.ToString("C", _cultureMX);
                DetailTotalDueSoonText.Text = totalDueSoon.ToString("C", _cultureMX);
                DetailExpenseCountText.Text = _expenses.Count.ToString();

                DetailLastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
                DetailStatusText.Text = "Listo";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar gastos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DetailStatusText.Text = "Error al cargar";
            }
        }

        private async void RefreshDetailButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDetailButton.IsEnabled = false;
            await LoadExpensesAsync();
            RefreshDetailButton.IsEnabled = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NewExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            // Verificar que hay proveedor seleccionado
            if (_supplierId == 0)
            {
                Toast.Show("Selecciona un proveedor primero", null, ToastNotification.ToastType.Warning);
                return;
            }

            // Si ya hay una fila en edición, no permitir crear otra
            if (_isCreatingNewExpense)
            {
                Toast.Show("Ya hay un gasto en edición", "Guarde o cancele antes de crear otro", ToastNotification.ToastType.Warning);
                return;
            }

            // Crear nueva fila inline
            var newExpense = new ExpenseDetailViewModel
            {
                IsNew = true,
                Description = "",
                TotalInput = "",
                Total = 0,
                CreditDays = _supplierCreditDays,
                ExpenseDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(_supplierCreditDays),
                OrderPO = "--",
                SelectedOrderId = 0,
                Status = "NUEVO",
                DaysText = $"{_supplierCreditDays} días"
            };

            // Insertar al inicio de la lista
            _expenses.Insert(0, newExpense);
            _isCreatingNewExpense = true;

            // Deshabilitar botón mientras se edita
            NewExpenseButton.IsEnabled = false;
            DetailStatusText.Text = "Editando nuevo gasto... (Enter para guardar, Escape para cancelar)";

            // Forzar actualización del DataGrid y enfocar
            ExpensesDataGrid.UpdateLayout();
            ExpensesDataGrid.SelectedItem = newExpense;
            ExpensesDataGrid.ScrollIntoView(newExpense);

            // Enfocar el campo descripción después de renderizar con mayor delay
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                // Esperar un poco más para que la fila se genere
                await Task.Delay(50);

                var textBox = FindDescriptionTextBox();
                if (textBox != null)
                {
                    textBox.Focus();
                    Keyboard.Focus(textBox);
                    textBox.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private TextBox FindDescriptionTextBox()
        {
            // Buscar la primera fila del DataGrid (la nueva)
            if (ExpensesDataGrid.Items.Count == 0) return null;

            // Forzar generación de la fila
            ExpensesDataGrid.UpdateLayout();

            var row = ExpensesDataGrid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
            if (row == null)
            {
                // Intentar forzar la generación
                ExpensesDataGrid.ScrollIntoView(ExpensesDataGrid.Items[0]);
                ExpensesDataGrid.UpdateLayout();
                row = ExpensesDataGrid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
            }

            if (row == null) return null;

            // Buscar el TextBox de descripción en la fila
            return FindVisualChild<TextBox>(row, "DescriptionEditBox");
        }

        private T FindVisualChild<T>(DependencyObject parent, string name = null) where T : FrameworkElement
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(name) || typedChild.Name == name)
                        return typedChild;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private async void SaveNewExpense_Click(object sender, RoutedEventArgs e)
        {
            // Buscar fila en edición (nueva o existente)
            var editingExpense = _expenses.FirstOrDefault(x => x.IsNew || x.IsEditing);
            if (editingExpense == null) return;

            bool isNewExpense = editingExpense.IsNew;

            // Validar descripción
            if (string.IsNullOrWhiteSpace(editingExpense.Description))
            {
                Toast.Show("La descripción es obligatoria", null, ToastNotification.ToastType.Warning);
                return;
            }

            // Validar y parsear monto
            decimal totalExpense = 0;
            if (string.IsNullOrWhiteSpace(editingExpense.TotalInput))
            {
                Toast.Show("El monto es obligatorio", null, ToastNotification.ToastType.Warning);
                return;
            }

            if (!decimal.TryParse(editingExpense.TotalInput, NumberStyles.Any, CultureInfo.InvariantCulture, out totalExpense) &&
                !decimal.TryParse(editingExpense.TotalInput.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out totalExpense))
            {
                Toast.Show("El monto debe ser un número válido", null, ToastNotification.ToastType.Warning);
                return;
            }

            if (totalExpense <= 0)
            {
                Toast.Show("El monto debe ser mayor a cero", null, ToastNotification.ToastType.Warning);
                return;
            }

            try
            {
                DetailStatusText.Text = isNewExpense ? "Guardando gasto..." : "Actualizando gasto...";

                var supabaseClient = _supabaseService.GetClient();

                // Usar la fecha seleccionada por el usuario
                DateTime expenseDate = editingExpense.ExpenseDate;
                DateTime scheduledDate = expenseDate.AddDays(_supplierCreditDays);

                // Obtener orden seleccionada
                int? orderId = editingExpense.SelectedOrderId > 0 ? editingExpense.SelectedOrderId : null;

                if (isNewExpense)
                {
                    // CREAR nuevo gasto
                    string status = "PENDIENTE";
                    DateTime? paidDate = null;
                    string payMethod = editingExpense.PayMethod ?? "TRANSFERENCIA";

                    if (_supplierCreditDays == 0)
                    {
                        status = "PAGADO";
                        paidDate = expenseDate;
                    }

                    var expenseDb = new ExpenseDb
                    {
                        SupplierId = _supplierId,
                        Description = editingExpense.Description.Trim(),
                        TotalExpense = totalExpense,
                        ExpenseDate = expenseDate,
                        ScheduledDate = scheduledDate,
                        Status = status,
                        PaidDate = paidDate,
                        PayMethod = payMethod,
                        OrderId = orderId,
                        ExpenseCategory = "GENERAL"
                    };

                    var result = await supabaseClient
                        .From<ExpenseDb>()
                        .Insert(expenseDb);

                    if (result?.Models?.Count > 0)
                    {
                        Toast.Show("Gasto guardado", $"{totalExpense.ToString("C", _cultureMX)}", ToastNotification.ToastType.Success);
                    }
                    else
                    {
                        throw new Exception("No se pudo crear el gasto");
                    }
                }
                else
                {
                    // ACTUALIZAR gasto existente
                    await supabaseClient
                        .From<ExpenseDb>()
                        .Where(ex => ex.Id == editingExpense.ExpenseId)
                        .Set(ex => ex.Description, editingExpense.Description.Trim())
                        .Set(ex => ex.TotalExpense, totalExpense)
                        .Set(ex => ex.ExpenseDate, expenseDate)
                        .Set(ex => ex.ScheduledDate, scheduledDate)
                        .Set(ex => ex.OrderId, orderId)
                        .Set(ex => ex.PayMethod, editingExpense.PayMethod ?? "TRANSFERENCIA")
                        .Update();

                    Toast.Show("Gasto actualizado", $"{totalExpense.ToString("C", _cultureMX)}", ToastNotification.ToastType.Success);
                }

                _isCreatingNewExpense = false;
                NewExpenseButton.IsEnabled = true;

                // Recargar la lista completa
                await LoadExpensesAsync();
            }
            catch (Exception ex)
            {
                Toast.Show("Error al guardar", ex.Message, ToastNotification.ToastType.Error);
                DetailStatusText.Text = "Error al guardar";
            }
        }

        private void CancelNewExpense_Click(object sender, RoutedEventArgs e)
        {
            // Buscar gasto nuevo (para eliminar)
            var newExpense = _expenses.FirstOrDefault(x => x.IsNew);
            if (newExpense != null)
            {
                _expenses.Remove(newExpense);
            }

            // Buscar gasto en edición (para cancelar edición)
            var editingExpense = _expenses.FirstOrDefault(x => x.IsEditing);
            if (editingExpense != null)
            {
                editingExpense.IsEditing = false;
                // Recargar para restaurar los valores originales
                _ = LoadExpensesAsync();
            }

            _isCreatingNewExpense = false;
            NewExpenseButton.IsEnabled = true;
            DetailStatusText.Text = "Listo";
            Toast.Show("Edición cancelada", null, ToastNotification.ToastType.Info, 2000);
        }

        private void PayButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var expenseId = (int)button.Tag;
            var expense = _expenses.FirstOrDefault(ex => ex.ExpenseId == expenseId);

            if (expense == null) return;

            // Crear menú contextual con métodos de pago
            var contextMenu = new ContextMenu();
            contextMenu.Style = (Style)FindResource("PaymentMethodMenu");

            foreach (var method in ExpenseDetailViewModel.PayMethods)
            {
                var menuItem = new MenuItem
                {
                    Header = method,
                    Tag = new Tuple<int, string, ExpenseDetailViewModel>(expenseId, method, expense),
                    FontSize = 13,
                    Padding = new Thickness(10, 6, 10, 6)
                };
                menuItem.Click += PaymentMethodMenuItem_Click;

                // Agregar ícono según método
                string icon = method switch
                {
                    "TRANSFERENCIA" => "🏦",
                    "EFECTIVO" => "💵",
                    "CHEQUE" => "📄",
                    "CRÉDITO" => "💳",
                    "DÉBITO" => "💳",
                    _ => "💰"
                };
                menuItem.Icon = new TextBlock { Text = icon, FontSize = 14 };

                contextMenu.Items.Add(menuItem);
            }

            // Mostrar el menú
            contextMenu.PlacementTarget = button;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        private async void PaymentMethodMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var tagData = menuItem.Tag as Tuple<int, string, ExpenseDetailViewModel>;

            if (tagData == null) return;

            int expenseId = tagData.Item1;
            string payMethod = tagData.Item2;
            var expense = tagData.Item3;

            try
            {
                DetailStatusText.Text = "Registrando pago...";

                var supabaseClient = _supabaseService.GetClient();

                await supabaseClient
                    .From<ExpenseDb>()
                    .Where(ex => ex.Id == expenseId)
                    .Set(ex => ex.Status, "PAGADO")
                    .Set(ex => ex.PaidDate, DateTime.Today)
                    .Set(ex => ex.PayMethod, payMethod)
                    .Update();

                // Remover de la lista (ya no esta pendiente)
                _expenses.Remove(expense);

                // Actualizar totales
                await LoadExpensesAsync();

                Toast.Show("Pago registrado", $"{expense.TotalFormatted} - {payMethod}", ToastNotification.ToastType.Success);
            }
            catch (Exception ex)
            {
                Toast.Show("Error al registrar pago", ex.Message, ToastNotification.ToastType.Error);
                DetailStatusText.Text = "Error al registrar pago";
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var expenseId = (int)button.Tag;
            var expense = _expenses.FirstOrDefault(ex => ex.ExpenseId == expenseId);

            if (expense == null) return;

            // Verificar que no hay otra fila en edición
            if (_isCreatingNewExpense || _expenses.Any(x => x.IsEditing))
            {
                Toast.Show("Ya hay un gasto en edición", "Guarde o cancele antes de editar otro", ToastNotification.ToastType.Warning);
                return;
            }

            StartInlineEdit(expense);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var expenseId = (int)button.Tag;
            var expense = _expenses.FirstOrDefault(ex => ex.ExpenseId == expenseId);

            if (expense == null) return;

            try
            {
                DetailStatusText.Text = "Eliminando gasto...";

                var supabaseClient = _supabaseService.GetClient();

                await supabaseClient
                    .From<ExpenseDb>()
                    .Where(ex => ex.Id == expenseId)
                    .Delete();

                _expenses.Remove(expense);

                // Actualizar totales
                await LoadExpensesAsync();

                Toast.Show("Gasto eliminado", expense.Description, ToastNotification.ToastType.Success);
            }
            catch (Exception ex)
            {
                Toast.Show("Error al eliminar", ex.Message, ToastNotification.ToastType.Error);
                DetailStatusText.Text = "Error al eliminar";
            }
        }

        #region Validación del campo Total (solo números decimales)

        /// <summary>
        /// Valida que solo se ingresen números y un punto decimal
        /// </summary>
        private void TotalTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string currentText = textBox.Text;
            string newChar = e.Text;

            // Solo permitir dígitos y punto decimal
            if (!char.IsDigit(newChar[0]) && newChar != ".")
            {
                e.Handled = true;
                return;
            }

            // Solo permitir un punto decimal
            if (newChar == "." && currentText.Contains("."))
            {
                e.Handled = true;
                return;
            }

            // Limitar a 2 decimales después del punto
            if (currentText.Contains("."))
            {
                int dotIndex = currentText.IndexOf('.');
                int selectionStart = textBox.SelectionStart;

                // Si el cursor está después del punto
                if (selectionStart > dotIndex)
                {
                    string decimals = currentText.Substring(dotIndex + 1);
                    // Si ya hay 2 decimales y no hay selección, bloquear
                    if (decimals.Length >= 2 && textBox.SelectionLength == 0)
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Previene el pegado de texto no válido
        /// </summary>
        private void TotalTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Interceptar Ctrl+V (pegar)
            if (e.Key == System.Windows.Input.Key.V &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (Clipboard.ContainsText())
                {
                    string pastedText = Clipboard.GetText();

                    // Validar que el texto pegado sea un número válido
                    if (!IsValidDecimalInput(pastedText, textBox.Text, textBox.SelectionStart, textBox.SelectionLength))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Permitir teclas especiales: Backspace, Delete, Tab, Enter, flechas
            // No hacer nada especial para estas
        }

        /// <summary>
        /// Selecciona todo el texto cuando el campo recibe el foco
        /// </summary>
        private void TotalTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                // Seleccionar todo el texto para facilitar la edición
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// Valida si el texto pegado resultaría en un número decimal válido
        /// </summary>
        private bool IsValidDecimalInput(string pastedText, string currentText, int selectionStart, int selectionLength)
        {
            // Construir el texto resultante
            string resultText = currentText.Substring(0, selectionStart) +
                               pastedText +
                               currentText.Substring(selectionStart + selectionLength);

            // Permitir cadena vacía
            if (string.IsNullOrEmpty(resultText))
                return true;

            // Intentar parsear como decimal
            if (!decimal.TryParse(resultText, out decimal value))
                return false;

            // Verificar que no tenga más de 2 decimales
            if (resultText.Contains("."))
            {
                string[] parts = resultText.Split('.');
                if (parts.Length > 2)
                    return false;
                if (parts.Length == 2 && parts[1].Length > 2)
                    return false;
            }

            return true;
        }

        #endregion

        #region Filtrado del ComboBox de Órdenes

        /// <summary>
        /// Se ejecuta cuando el ComboBox se carga - conecta el evento TextChanged al TextBox interno
        /// </summary>
        private void OrderComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // Esperar a que el template se aplique completamente
            comboBox.ApplyTemplate();

            // Buscar el TextBox interno del ComboBox editable
            var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
            if (textBox != null)
            {
                // Suscribirse al evento TextChanged
                textBox.TextChanged += (s, args) =>
                {
                    // Solo filtrar si el dropdown está abierto o si hay texto
                    string searchText = textBox.Text ?? "";

                    // Abrir dropdown automáticamente al escribir
                    if (!string.IsNullOrEmpty(searchText) && searchText.Length >= 1)
                    {
                        comboBox.IsDropDownOpen = true;
                    }

                    FilterOrders(searchText);
                };
            }
        }

        /// <summary>
        /// Filtra la lista de órdenes mientras el usuario escribe
        /// </summary>
        private void OrderComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // Obtener el TextBox interno del ComboBox editable
            var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
            string searchText = textBox?.Text ?? comboBox.Text ?? "";

            FilterOrders(searchText);
        }

        /// <summary>
        /// Filtra las órdenes basándose en el texto de búsqueda
        /// </summary>
        private void FilterOrders(string searchText)
        {
            if (_allOrders == null || _allOrders.Count == 0) return;

            AvailableOrders.Clear();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Si no hay texto, mostrar todas las órdenes
                foreach (var order in _allOrders)
                {
                    AvailableOrders.Add(order);
                }
            }
            else
            {
                // Filtrar órdenes que contengan el texto de búsqueda
                var filtered = _allOrders
                    .Where(o => o.DisplayText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filtered.Any())
                {
                    foreach (var order in filtered)
                    {
                        AvailableOrders.Add(order);
                    }
                }
                else
                {
                    // Si no hay coincidencias, mostrar solo "Sin orden"
                    var sinOrden = _allOrders.FirstOrDefault(o => o.Id == 0);
                    if (sinOrden != null)
                    {
                        AvailableOrders.Add(sinOrden);
                    }
                }
            }
        }

        /// <summary>
        /// Cuando el ComboBox pierde el foco, asegurar que tenga un valor válido
        /// </summary>
        private void OrderComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // Si no hay item seleccionado, seleccionar "Sin orden"
            if (comboBox.SelectedItem == null)
            {
                var sinOrden = _allOrders.FirstOrDefault(o => o.Id == 0);
                if (sinOrden != null && AvailableOrders.Contains(sinOrden))
                {
                    comboBox.SelectedItem = sinOrden;
                }
                else if (AvailableOrders.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            }

            // Restaurar la lista completa para la próxima vez
            FilterOrders("");
        }

        /// <summary>
        /// Cuando el dropdown se abre, restaurar la lista completa
        /// </summary>
        private void OrderComboBox_DropDownOpened(object sender, EventArgs e)
        {
            // Restaurar lista completa cuando se abre el dropdown
            FilterOrders("");
        }

        #endregion
    }

    // ViewModel para el detalle de gastos
    public class ExpenseDetailViewModel : INotifyPropertyChanged
    {
        private int _expenseId;
        private string _description;
        private string _orderPO;
        private int _selectedOrderId;
        private decimal _total;
        private string _totalFormatted;
        private string _totalInput;
        private DateTime _expenseDate;
        private DateTime _dueDate;
        private int _daysRemaining;
        private string _daysText;
        private string _status;
        private bool _isNew;
        private bool _isEditing;
        private int _creditDays; // Días de crédito del proveedor para cálculos
        private string _payMethod = "TRANSFERENCIA"; // Método de pago por defecto
        private DateTime? _paidDate; // Fecha de pago

        public int ExpenseId
        {
            get => _expenseId;
            set { _expenseId = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string OrderPO
        {
            get => _orderPO;
            set { _orderPO = value; OnPropertyChanged(); }
        }

        public int SelectedOrderId
        {
            get => _selectedOrderId;
            set { _selectedOrderId = value; OnPropertyChanged(); }
        }

        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged();
                TotalFormatted = value.ToString("C", new System.Globalization.CultureInfo("es-MX"));
            }
        }

        public string TotalFormatted
        {
            get => _totalFormatted;
            set { _totalFormatted = value; OnPropertyChanged(); }
        }

        public string TotalInput
        {
            get => _totalInput;
            set { _totalInput = value; OnPropertyChanged(); }
        }

        public DateTime ExpenseDate
        {
            get => _expenseDate;
            set
            {
                _expenseDate = value;
                OnPropertyChanged();
                // Recalcular fecha de vencimiento automáticamente
                if (_creditDays > 0 || IsNew || IsEditing)
                {
                    DueDate = value.AddDays(_creditDays);
                    RecalculateDaysRemaining();
                }
            }
        }

        public DateTime DueDate
        {
            get => _dueDate;
            set { _dueDate = value; OnPropertyChanged(); }
        }

        public int CreditDays
        {
            get => _creditDays;
            set { _creditDays = value; OnPropertyChanged(); }
        }

        private void RecalculateDaysRemaining()
        {
            if (!IsNew && !IsEditing) return;

            int daysDiff = (DueDate.Date - DateTime.Today).Days;
            DaysRemaining = daysDiff;

            if (daysDiff < 0)
                DaysText = $"{Math.Abs(daysDiff)} días vencido";
            else if (daysDiff == 0)
                DaysText = "Hoy";
            else
                DaysText = $"{daysDiff} días";
        }

        public int DaysRemaining
        {
            get => _daysRemaining;
            set { _daysRemaining = value; OnPropertyChanged(); }
        }

        public string DaysText
        {
            get => _daysText;
            set { _daysText = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public bool IsNew
        {
            get => _isNew;
            set
            {
                _isNew = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotNew));
                OnPropertyChanged(nameof(IsEditable));
                OnPropertyChanged(nameof(IsReadOnly));
            }
        }

        public bool IsNotNew => !_isNew;

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditable));
                OnPropertyChanged(nameof(IsReadOnly));
            }
        }

        /// <summary>
        /// True si la fila está en modo edición (nuevo o editando existente)
        /// </summary>
        public bool IsEditable => IsNew || IsEditing;

        /// <summary>
        /// True si la fila es de solo lectura
        /// </summary>
        public bool IsReadOnly => !IsEditable;

        public string PayMethod
        {
            get => _payMethod;
            set { _payMethod = value; OnPropertyChanged(); }
        }

        public DateTime? PaidDate
        {
            get => _paidDate;
            set { _paidDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPaid)); }
        }

        /// <summary>
        /// True si el gasto está pagado (tiene fecha de pago)
        /// </summary>
        public bool IsPaid => PaidDate.HasValue;

        /// <summary>
        /// True si se puede pagar (no pagado y no en edición)
        /// </summary>
        public bool IsPayable => !IsPaid && IsReadOnly;

        /// <summary>
        /// Lista de métodos de pago disponibles
        /// </summary>
        public static List<string> PayMethods => new List<string>
        {
            "TRANSFERENCIA",
            "EFECTIVO",
            "CHEQUE",
            "CRÉDITO",
            "DÉBITO"
        };

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ViewModel para el selector de proveedores
    public class SupplierDisplayItem
    {
        public int Id { get; set; }
        public string SupplierName { get; set; }
        public string DisplayText { get; set; }
        public int CreditDays { get; set; }
        public int PendingCount { get; set; }
    }

    // ViewModel para el selector de órdenes
    public class OrderDisplayItem
    {
        public int Id { get; set; }
        public string DisplayText { get; set; }
    }
}
