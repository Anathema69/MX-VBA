using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class OrdersManagementWindow : Window
    {
        private UserSession _currentUser;
        private ObservableCollection<OrderViewModel> _orders;
        private CollectionViewSource _ordersViewSource;
        private readonly SupabaseService _supabaseService;
        private List<ClientDb> _clients;
        private List<VendorDb> _vendors;
        private List<OrderStatusDb> _orderStatuses;
        public bool IsAdmin => _currentUser?.Role == "direccion" || _currentUser?.Role == "administracion";

        // Caché completo de órdenes cargadas desde la BD
        private List<OrderViewModel> _allOrdersCache;
        private DateTime _lastFullLoadTime = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public OrdersManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _orders = new ObservableCollection<OrderViewModel>();
            _allOrdersCache = new List<OrderViewModel>();
            _supabaseService = SupabaseService.Instance;

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            // IMPORTANTE: Establecer el DataContext para los bindings
            this.DataContext = this;

            InitializeUI();
            ConfigurePermissions();

            // Cargar datos iniciales
            _ = LoadInitialDataAsync();
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

        

        private void InitializeUI()
        {
            // Configurar información del usuario
            UserStatusText.Text = $"Usuario: {_currentUser.FullName} ({GetRoleDisplayName(_currentUser.Role)})";

            // ⭐ IMPORTANTE: AGREGAR ESTA LÍNEA PARA QUE FUNCIONE EL BINDING
            this.Tag = _currentUser.Role;

            // Configurar el DataGrid
            _ordersViewSource = new CollectionViewSource { Source = _orders };

            // Ordenar por fecha ascendente (más antiguo primero)
            _ordersViewSource.SortDescriptions.Add(
                new System.ComponentModel.SortDescription("OrderDate", System.ComponentModel.ListSortDirection.Ascending)
            );

            OrdersDataGrid.ItemsSource = _ordersViewSource.View;

            // Nuevo método para configurar el filtro de estado
            ConfigureStatusFilterComboBox();

            // Los filtros de fecha (Año/Mes) se configuran dinámicamente después de cargar los datos
            // Ver UpdateDateFiltersFromData()

            // Título de la ventana
            this.Title = $"IMA Mecatrónica - Manejo de Órdenes - {_currentUser.FullName}";
        }

        /// <summary>
        /// Actualiza los filtros de Año y Mes basándose en los datos disponibles en el caché
        /// </summary>
        private void UpdateDateFiltersFromData()
        {
            if (_allOrdersCache == null || _allOrdersCache.Count == 0) return;

            // Obtener años únicos disponibles (ordenados descendente)
            var availableYears = _allOrdersCache
                .Select(o => o.OrderDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            // Guardar selección actual
            var currentYearItem = YearFilter.SelectedItem as ComboBoxItem;
            var currentYear = currentYearItem?.Tag as int?;

            var currentMonthItem = MonthFilter.SelectedItem as ComboBoxItem;
            var currentMonth = currentMonthItem?.Tag as int?;

            // Actualizar YearFilter
            YearFilter.SelectionChanged -= YearFilter_SelectionChanged;
            YearFilter.Items.Clear();
            YearFilter.Items.Add(new ComboBoxItem { Content = "Todos", IsSelected = !currentYear.HasValue });

            foreach (var year in availableYears)
            {
                var item = new ComboBoxItem { Content = year.ToString(), Tag = year };
                if (currentYear.HasValue && currentYear.Value == year)
                {
                    item.IsSelected = true;
                }
                YearFilter.Items.Add(item);
            }
            YearFilter.SelectionChanged += YearFilter_SelectionChanged;

            // Actualizar MonthFilter basándose en el año seleccionado
            UpdateMonthFilterForYear(currentYear, currentMonth);
        }

        /// <summary>
        /// Actualiza el filtro de meses según el año seleccionado
        /// </summary>
        private void UpdateMonthFilterForYear(int? selectedYear, int? currentMonth = null)
        {
            if (_allOrdersCache == null) return;

            // Nombres de meses en español
            var monthNames = new[] { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                                    "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            // Obtener meses únicos disponibles para el año seleccionado (o todos si no hay año)
            var ordersToFilter = selectedYear.HasValue
                ? _allOrdersCache.Where(o => o.OrderDate.Year == selectedYear.Value)
                : _allOrdersCache;

            var availableMonths = ordersToFilter
                .Select(o => o.OrderDate.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            // Actualizar MonthFilter
            MonthFilter.SelectionChanged -= MonthFilter_SelectionChanged;
            MonthFilter.Items.Clear();
            MonthFilter.Items.Add(new ComboBoxItem { Content = "Todos", IsSelected = !currentMonth.HasValue });

            foreach (var month in availableMonths)
            {
                var item = new ComboBoxItem { Content = monthNames[month], Tag = month };
                if (currentMonth.HasValue && currentMonth.Value == month)
                {
                    item.IsSelected = true;
                }
                MonthFilter.Items.Add(item);
            }
            MonthFilter.SelectionChanged += MonthFilter_SelectionChanged;
        }

        private void ConfigureStatusFilterComboBox()
        {
            // Limpiar items existentes
            StatusFilter.Items.Clear();

            // Agregar opción "Todos"
            StatusFilter.Items.Add(new ComboBoxItem { Content = "Todos" });

            if (_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos")
            {
                // Coordinación/Proyectos solo ve estados 0, 1, 2 - Por defecto EN PROCESO
                StatusFilter.Items.Add(new ComboBoxItem { Content = "CREADA" });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "EN PROCESO", IsSelected = true });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "LIBERADA" });

                System.Diagnostics.Debug.WriteLine("📋 ComboBox configurado para coordinacion: 3 estados (defecto: EN PROCESO)");
            }
            else if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
            {
                // Dirección/Administración ve todos los estados
                StatusFilter.Items.Add(new ComboBoxItem { Content = "CREADA" });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "EN PROCESO" });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "LIBERADA" });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "CERRADA" });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "COMPLETADA" });
                StatusFilter.Items.Add(new ComboBoxItem { Content = "CANCELADA" });

                // Cargar filtro guardado para administracion, o usar CREADA por defecto
                string savedFilter = UserPreferencesService.GetOrdersStatusFilter(_currentUser.Role);
                string defaultFilter = savedFilter ?? "CREADA";

                // Seleccionar el filtro correspondiente
                foreach (ComboBoxItem item in StatusFilter.Items)
                {
                    if (item.Content?.ToString() == defaultFilter)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"📋 ComboBox configurado para {_currentUser.Role}: todos los estados (filtro: {defaultFilter})");
            }
        }

        private void ConfigurePermissions()
        {
            // Configurar visibilidad y permisos según el rol
            // Roles v2.0: direccion, administracion, proyectos, coordinacion, ventas
            switch (_currentUser.Role)
            {
                case "direccion":
                    // Dirección tiene acceso total incluyendo columnas de gastos v2.0
                    NewOrderButton.IsEnabled = true;
                    SubtotalColumn.Visibility = Visibility.Visible;
                    TotalColumn.Visibility = Visibility.Visible;
                    InvoicedColumn.Visibility = Visibility.Visible;
                    // Columnas v2.0 - Gastos (solo direccion por ahora)
                    GastoMaterialColumn.Visibility = Visibility.Visible;
                    GastoOperativoColumn.Visibility = Visibility.Visible;
                    GastoIndirectoColumn.Visibility = Visibility.Visible;
                    break;

                case "administracion":
                    // Administración tiene acceso pero SIN columnas de gastos v2.0
                    NewOrderButton.IsEnabled = true;
                    SubtotalColumn.Visibility = Visibility.Visible;
                    TotalColumn.Visibility = Visibility.Visible;
                    InvoicedColumn.Visibility = Visibility.Visible;
                    // Columnas v2.0 - ocultas para administracion (pendiente validación)
                    GastoMaterialColumn.Visibility = Visibility.Collapsed;
                    GastoOperativoColumn.Visibility = Visibility.Collapsed;
                    GastoIndirectoColumn.Visibility = Visibility.Collapsed;
                    break;

                case "coordinacion":
                case "proyectos":
                    // Coordinación/Proyectos NO puede crear nuevas órdenes
                    NewOrderButton.IsEnabled = false;
                    NewOrderButton.Visibility = Visibility.Collapsed;

                    RefreshButton.Width = 90;

                    // Ocultar botón Exportar (no aplica para este rol)
                    ExportButton.Visibility = Visibility.Collapsed;

                    // NO puede ver campos financieros ni vendedor
                    VendorColumn.Visibility = Visibility.Collapsed;
                    SubtotalColumn.Visibility = Visibility.Collapsed;
                    TotalColumn.Visibility = Visibility.Collapsed;
                    InvoicedColumn.Visibility = Visibility.Collapsed;
                    // Columnas v2.0 - ocultas para coordinación/proyectos
                    GastoMaterialColumn.Visibility = Visibility.Collapsed;
                    GastoOperativoColumn.Visibility = Visibility.Collapsed;
                    GastoIndirectoColumn.Visibility = Visibility.Collapsed;

                    // Mostrar título del departamento (van directo a órdenes sin menú principal)
                    DepartmentTitle.Text = _currentUser.Role == "coordinacion" ? "COORDINACIÓN" : "PROYECTOS";
                    DepartmentTitle.Visibility = Visibility.Visible;
                    break;

                case "ventas":
                    // Ventas no debería poder acceder aquí
                    MessageBox.Show(
                        "No tiene permisos para acceder a este módulo.",
                        "Acceso Denegado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    this.Close();
                    break;
            }
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                StatusText.Text = "Cargando datos...";

                // Cargar clientes, vendedores y estados en paralelo
                var clientsTask = _supabaseService.GetClients();
                var vendorsTask = _supabaseService.GetVendors();
                var statusesTask = _supabaseService.GetOrderStatuses();

                await Task.WhenAll(clientsTask, vendorsTask, statusesTask);

                _clients = await clientsTask;
                _vendors = await vendorsTask;
                _orderStatuses = await statusesTask;

                System.Diagnostics.Debug.WriteLine($"✅ Datos cargados: {_clients.Count} clientes, {_vendors.Count} vendedores, {_orderStatuses.Count} estados");

                // Ahora cargar las órdenes
                await LoadOrders();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error cargando datos";
                MessageBox.Show(
                    $"Error al cargar datos iniciales:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private async Task LoadOrders(bool forceReload = false)
        {
            try
            {
                StatusText.Text = "Cargando órdenes...";

                // Verificar si podemos usar el caché
                bool shouldUseCache = !forceReload &&
                                      _allOrdersCache.Count > 0 &&
                                      (DateTime.Now - _lastFullLoadTime) < _cacheExpiration;

                if (shouldUseCache)
                {
                    System.Diagnostics.Debug.WriteLine("📦 Usando caché de órdenes");
                    RepopulateFromCache();
                    return;
                }

                System.Diagnostics.Debug.WriteLine("🔄 Recargando órdenes desde BD");
                _orders.Clear();
                _allOrdersCache.Clear();

                // Determinar filtro según el rol
                List<int> statusFilter = null;
                if ((_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos"))
                {
                    // Coordinador solo ve estados 0, 1 y 2
                    statusFilter = new List<int> { 0, 1, 2 };
                    System.Diagnostics.Debug.WriteLine("👤 Aplicando filtro de coordinador: estados 0, 1, 2");
                }
                else if ((_currentUser.Role == "direccion" || _currentUser.Role == "administracion"))
                {
                    // Admin ve todo
                    statusFilter = null;
                    System.Diagnostics.Debug.WriteLine("👑 Admin: sin filtros, mostrando todas las órdenes");
                }

                // Cargar primero las 100 órdenes más recientes con el filtro aplicado
                // Para rol "direccion" usar la vista con gastos calculados
                int ordersLoadedCount = 0;

                if (_currentUser.Role == "direccion")
                {
                    // Usar vista v_order_gastos que incluye gasto_material calculado
                    var ordersWithGastos = await _supabaseService.GetOrdersWithGastos(
                        limit: 100,
                        offset: 0,
                        filterStatuses: statusFilter
                    );

                    if (ordersWithGastos != null && ordersWithGastos.Count > 0)
                    {
                        var orderIds = ordersWithGastos.Select(o => o.Id).ToList();
                        var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(orderIds);

                        foreach (var order in ordersWithGastos)
                        {
                            var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);
                            var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                            var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                            var viewModel = order.ToViewModel(
                                clientName: client?.Name,
                                vendorName: vendor?.VendorName,
                                statusName: status?.Name
                            );

                            if (invoicedTotals.ContainsKey(order.Id))
                            {
                                viewModel.InvoicedAmount = invoicedTotals[order.Id];
                            }

                            _orders.Add(viewModel);
                            _allOrdersCache.Add(viewModel);
                        }
                        ordersLoadedCount = ordersWithGastos.Count;
                    }
                }
                else
                {
                    // Para otros roles usar tabla normal (no necesitan ver gastos)
                    var ordersFromDb = await _supabaseService.GetOrders(
                        limit: 100,
                        offset: 0,
                        filterStatuses: statusFilter
                    );

                    if (ordersFromDb != null && ordersFromDb.Count > 0)
                    {
                        var orderIds = ordersFromDb.Select(o => o.Id).ToList();
                        var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(orderIds);

                        foreach (var order in ordersFromDb)
                        {
                            var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);
                            var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                            var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                            var viewModel = order.ToViewModel(
                                clientName: client?.Name,
                                vendorName: vendor?.VendorName,
                                statusName: status?.Name
                            );

                            if (invoicedTotals.ContainsKey(order.Id))
                            {
                                viewModel.InvoicedAmount = invoicedTotals[order.Id];
                            }

                            _orders.Add(viewModel);
                            _allOrdersCache.Add(viewModel);
                        }
                        ordersLoadedCount = ordersFromDb.Count;
                    }
                }

                if (ordersLoadedCount > 0)
                {
                    // Actualizar timestamp del caché
                    _lastFullLoadTime = DateTime.Now;

                    // Mostrar mensaje específico según el rol
                    if ((_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos"))
                    {
                        StatusText.Text = $"{_orders.Count} órdenes activas cargadas (CREADA, EN PROCESO, LIBERADA)";
                    }
                    else
                    {
                        StatusText.Text = $"{_orders.Count} órdenes más recientes cargadas";
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ {_orders.Count} órdenes cargadas correctamente");

                    // Cargar el resto en segundo plano si hay más de 100
                    if (ordersLoadedCount == 100)
                    {
                        _ = LoadRemainingOrdersAsync(statusFilter);
                    }

                    // Actualizar filtros de fecha basados en los datos disponibles
                    UpdateDateFiltersFromData();

                    // Aplicar filtro inicial (CREADA por defecto)
                    ApplyInitialFilter();
                }
                else
                {
                    if ((_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos"))
                    {
                        StatusText.Text = "No se encontraron órdenes activas";
                    }
                    else
                    {
                        StatusText.Text = "No se encontraron órdenes";
                    }

                    // Mostrar mensaje de sin registros
                    NoRecordsMessage.Visibility = Visibility.Visible;
                    OrdersDataGrid.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error cargando órdenes";
                MessageBox.Show(
                    $"Error al cargar órdenes:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }
        }

        private void RepopulateFromCache()
        {
            _orders.Clear();
            foreach (var order in _allOrdersCache)
            {
                _orders.Add(order);
            }

            if ((_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos"))
            {
                StatusText.Text = $"{_orders.Count} órdenes activas (desde caché)";
            }
            else
            {
                StatusText.Text = $"{_orders.Count} órdenes (desde caché)";
            }
        }

        private async Task RefreshSingleOrder(int orderId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Actualizando orden {orderId}");

                // Obtener la orden actualizada desde la BD
                var updatedOrderDb = await _supabaseService.GetOrderById(orderId);
                if (updatedOrderDb == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Orden {orderId} no encontrada en BD");
                    return;
                }

                // Obtener totales facturados actualizados
                var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(new List<int> { orderId });

                // Obtener datos relacionados
                var client = _clients?.FirstOrDefault(c => c.Id == updatedOrderDb.ClientId);
                var vendor = _vendors?.FirstOrDefault(v => v.Id == updatedOrderDb.SalesmanId);
                var status = _orderStatuses?.FirstOrDefault(s => s.Id == updatedOrderDb.OrderStatus);

                // Crear ViewModel actualizado
                var updatedViewModel = updatedOrderDb.ToViewModel(
                    clientName: client?.Name,
                    vendorName: vendor?.VendorName,
                    statusName: status?.Name
                );

                if (invoicedTotals.ContainsKey(orderId))
                {
                    updatedViewModel.InvoicedAmount = invoicedTotals[orderId];
                }

                // Actualizar en la colección observable
                var existingOrder = _orders.FirstOrDefault(o => o.Id == orderId);
                if (existingOrder != null)
                {
                    var index = _orders.IndexOf(existingOrder);
                    _orders[index] = updatedViewModel;
                }

                // Actualizar en el caché
                var cachedOrder = _allOrdersCache.FirstOrDefault(o => o.Id == orderId);
                if (cachedOrder != null)
                {
                    var cacheIndex = _allOrdersCache.IndexOf(cachedOrder);
                    _allOrdersCache[cacheIndex] = updatedViewModel;
                }

                System.Diagnostics.Debug.WriteLine($"✅ Orden {orderId} actualizada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando orden {orderId}: {ex.Message}");
            }
        }

        private async Task LoadRemainingOrdersAsync(List<int> statusFilter = null)
        {
            try
            {
                int offset = 100;
                int batchSize = 100;
                bool hasMore = true;
                bool useGastosView = _currentUser.Role == "direccion";

                while (hasMore)
                {
                    int batchCount = 0;

                    if (useGastosView)
                    {
                        // Usar vista con gastos para rol direccion
                        var moreOrders = await _supabaseService.GetOrdersWithGastos(
                            limit: batchSize,
                            offset: offset,
                            filterStatuses: statusFilter
                        );

                        if (moreOrders != null && moreOrders.Count > 0)
                        {
                            var orderIds = moreOrders.Select(o => o.Id).ToList();
                            var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(orderIds);

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                foreach (var order in moreOrders)
                                {
                                    var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);
                                    var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                                    var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                                    var viewModel = order.ToViewModel(
                                        clientName: client?.Name,
                                        vendorName: vendor?.VendorName,
                                        statusName: status?.Name
                                    );

                                    if (invoicedTotals.ContainsKey(order.Id))
                                    {
                                        viewModel.InvoicedAmount = invoicedTotals[order.Id];
                                    }

                                    _orders.Add(viewModel);
                                    _allOrdersCache.Add(viewModel);
                                }
                                StatusText.Text = $"{_orders.Count} órdenes cargadas";
                            });

                            batchCount = moreOrders.Count;
                        }
                    }
                    else
                    {
                        // Usar tabla normal para otros roles
                        var moreOrders = await _supabaseService.GetOrders(
                            limit: batchSize,
                            offset: offset,
                            filterStatuses: statusFilter
                        );

                        if (moreOrders != null && moreOrders.Count > 0)
                        {
                            var orderIds = moreOrders.Select(o => o.Id).ToList();
                            var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(orderIds);

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                foreach (var order in moreOrders)
                                {
                                    var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);
                                    var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                                    var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                                    var viewModel = order.ToViewModel(
                                        clientName: client?.Name,
                                        vendorName: vendor?.VendorName,
                                        statusName: status?.Name
                                    );

                                    if (invoicedTotals.ContainsKey(order.Id))
                                    {
                                        viewModel.InvoicedAmount = invoicedTotals[order.Id];
                                    }

                                    _orders.Add(viewModel);
                                    _allOrdersCache.Add(viewModel);
                                }

                                if ((_currentUser.Role == "coordinacion" || _currentUser.Role == "proyectos"))
                                {
                                    StatusText.Text = $"{_orders.Count} órdenes activas cargadas";
                                }
                                else
                                {
                                    StatusText.Text = $"{_orders.Count} órdenes cargadas";
                                }
                            });

                            batchCount = moreOrders.Count;
                        }
                    }

                    offset += batchSize;
                    hasMore = batchCount == batchSize;

                    // Pequeña pausa para no saturar
                    await Task.Delay(100);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Carga completa: {_orders.Count} órdenes totales");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando órdenes adicionales: {ex.Message}");
            }
        }



        private string GetRoleDisplayName(string role)
        {
            switch (role)
            {
                // Roles v2.0
                case "direccion": return "Dirección";
                case "administracion": return "Administración";
                case "proyectos": return "Proyectos";
                case "coordinacion": return "Coordinación";
                case "ventas": return "Ventas";
                // Legacy
                case "admin": return "Administrador";
                case "coordinator": return "Coordinador";
                case "salesperson": return "Vendedor";
                default: return role;
            }
        }

        // Event Handlers
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role == "direccion" || _currentUser.Role == "administracion")
            {
                // Admin: volver al menú principal
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainMenuWindow)
                    {
                        foreach (Window win in Application.Current.Windows)
                        {
                            if (win is MainMenuWindow) continue;
                            win.Close();
                        }
                        window.Show();
                        break;
                    }
                }
            }
            else
            {
                // Coordinacion/Proyectos: cerrar sesión directo sin confirmacion
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
            }

            this.Close();
        }

        private async void NewOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentUser.Role != "direccion" && _currentUser.Role != "administracion"))
            {
                MessageBox.Show(
                    "Solo el administrador puede crear nuevas órdenes.",
                    "Permiso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Pasar el usuario actual a la ventana de nueva orden
                var newOrderWindow = new NewOrderWindow(_currentUser);
                newOrderWindow.Owner = this;

                if (newOrderWindow.ShowDialog() == true)
                {
                    // Forzar recarga para incluir la nueva orden (es rápido porque es una nueva)
                    await LoadOrders(forceReload: true);
                    StatusText.Text = "Nueva orden creada exitosamente";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el formulario:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Actualizando...";

            // Guardar el texto de búsqueda actual antes de recargar
            var currentSearchText = SearchBox.Text?.Trim();

            // Deshabilitar controles durante la actualización
            RefreshButton.IsEnabled = false;
            NewOrderButton.IsEnabled = false;

            try
            {
                // Forzar recarga completa desde BD (ignorar caché)
                await LoadOrders(forceReload: true);

                // Reaplicar filtros después de cargar
                ReapplyFilters(currentSearchText);
            }
            finally
            {
                RefreshButton.IsEnabled = true;

                // Restaurar permisos del botón Nueva Orden según el rol
                if ((_currentUser.Role == "direccion" || _currentUser.Role == "administracion"))
                {
                    NewOrderButton.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Reaplica los filtros de búsqueda, estado, mes y año después de recargar datos
        /// </summary>
        private void ReapplyFilters(string searchText)
        {
            if (_ordersViewSource?.View == null) return;

            var selectedStatusItem = StatusFilter.SelectedItem as ComboBoxItem;
            var statusFilter = selectedStatusItem?.Content?.ToString();

            var selectedMonthItem = MonthFilter.SelectedItem as ComboBoxItem;
            var monthFilter = selectedMonthItem?.Tag as int?;

            var selectedYearItem = YearFilter.SelectedItem as ComboBoxItem;
            var yearFilter = selectedYearItem?.Tag as int?;

            _ordersViewSource.View.Filter = item =>
            {
                var order = item as OrderViewModel;
                if (order == null) return false;

                // Filtro por estado
                bool matchesStatus = statusFilter == "Todos" || order.Status == statusFilter;

                // Filtro por mes (basado en f_podate / OrderDate)
                bool matchesMonth = !monthFilter.HasValue || order.OrderDate.Month == monthFilter.Value;

                // Filtro por año (basado en f_podate / OrderDate)
                bool matchesYear = !yearFilter.HasValue || order.OrderDate.Year == yearFilter.Value;

                // Filtro por texto de búsqueda
                bool matchesSearch = true;
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLower();
                    matchesSearch = order.OrderNumber.ToLower().Contains(searchLower) ||
                                   order.ClientName.ToLower().Contains(searchLower) ||
                                   order.Description.ToLower().Contains(searchLower) ||
                                   order.VendorName.ToLower().Contains(searchLower);
                }

                return matchesStatus && matchesMonth && matchesYear && matchesSearch;
            };

            UpdateStatusBar();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ordersViewSource?.View == null) return;

            // Reaplicar filtros combinados (búsqueda + estado)
            ReapplyFilters(SearchBox.Text?.Trim());
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ordersViewSource?.View == null) return;

            var selectedItem = StatusFilter.SelectedItem as ComboBoxItem;
            var filterText = selectedItem?.Content?.ToString();

            // Guardar preferencia de filtro para rol administracion
            if (_currentUser?.Role == "administracion" && !string.IsNullOrEmpty(filterText))
            {
                UserPreferencesService.SaveOrdersStatusFilter(_currentUser.Role, filterText);
            }

            // Reaplicar filtros combinados (búsqueda + estado + mes + año)
            ReapplyFilters(SearchBox.Text?.Trim());
        }

        private void MonthFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ordersViewSource?.View == null) return;

            // Reaplicar filtros combinados
            ReapplyFilters(SearchBox.Text?.Trim());
        }

        private void YearFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ordersViewSource?.View == null) return;

            // Obtener año seleccionado
            var selectedYearItem = YearFilter.SelectedItem as ComboBoxItem;
            var selectedYear = selectedYearItem?.Tag as int?;

            // Actualizar meses disponibles para el año seleccionado
            UpdateMonthFilterForYear(selectedYear);

            // Reaplicar filtros combinados
            ReapplyFilters(SearchBox.Text?.Trim());
        }

        private void ApplyInitialFilter()
        {
            if (_ordersViewSource?.View == null) return;

            // Aplicar filtro inicial usando el método combinado
            ReapplyFilters(SearchBox.Text?.Trim());
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var order = button?.Tag as OrderViewModel;

            if (order == null) return;

            try
            {
                var editWindow = new EditOrderWindow(order, _currentUser);
                editWindow.Owner = this;

                if (editWindow.ShowDialog() == true)
                {
                    await RefreshSingleOrder(order.Id);
                    StatusText.Text = "Orden actualizada correctamente";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el editor:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentUser.Role != "direccion" && _currentUser.Role != "administracion"))
            {
                MessageBox.Show(
                    "Solo el administrador puede eliminar órdenes.",
                    "Permiso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var order = button?.Tag as OrderViewModel;

            if (order == null) return;

            var result = MessageBox.Show(
                $"¿Está seguro que desea eliminar la orden {order.OrderNumber}?\n\n" +
                "Esta acción no se puede deshacer.",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔵 UI: Iniciando cancelación de orden {order.Id} - {order.OrderNumber}");
                    StatusText.Text = "Cancelando orden...";

                    // Cancelar la orden (cambiar estado a CANCELADO = 5)
                    System.Diagnostics.Debug.WriteLine($"🔵 UI: Llamando a CancelOrder({order.Id})...");
                    bool success = await _supabaseService.CancelOrder(order.Id);
                    System.Diagnostics.Debug.WriteLine($"🔵 UI: CancelOrder retornó: {success}");

                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"🔵 UI: Actualizando orden en lista local...");

                        // Actualizar la orden en la lista local
                        await RefreshSingleOrder(order.Id);

                        System.Diagnostics.Debug.WriteLine($"🔵 UI: Orden actualizada en lista local");

                        MessageBox.Show(
                            $"La orden {order.OrderNumber} ha sido cancelada exitosamente.",
                            "Orden Cancelada",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        StatusText.Text = "Orden cancelada correctamente";
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ UI: CancelOrder retornó FALSE");

                        MessageBox.Show(
                            "No se pudo cancelar la orden. Por favor, intente nuevamente.\n\n" +
                            "Revise los logs de debug para más información.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        StatusText.Text = "Error al cancelar orden";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ UI: Excepción al cancelar orden:");
                    System.Diagnostics.Debug.WriteLine($"   Tipo: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"   Mensaje: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");

                    MessageBox.Show(
                        $"Error al cancelar la orden:\n{ex.Message}\n\n" +
                        $"Tipo: {ex.GetType().Name}\n\n" +
                        "Revise los logs de debug para más información.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    StatusText.Text = "Error al cancelar orden";
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔵 UI: Usuario canceló la eliminación de orden {order.Id}");
            }
        }

        private void UpdateStatusBar()
        {
            var visibleCount = _ordersViewSource?.View?.Cast<object>().Count() ?? 0;
            StatusText.Text = $"{visibleCount} órdenes visibles de {_orders.Count} total";

            // Obtener el filtro actual
            var selectedItem = StatusFilter.SelectedItem as ComboBoxItem;
            var currentFilter = selectedItem?.Content?.ToString() ?? "CREADA";

            // Mostrar/ocultar mensaje de "Sin registros"
            if (visibleCount == 0)
            {
                NoRecordsTitle.Text = $"Sin registros para: {currentFilter}";
                NoRecordsSubtitle.Text = "Prueba con otro filtro o verifica que existan órdenes";
                NoRecordsMessage.Visibility = Visibility.Visible;
                OrdersDataGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoRecordsMessage.Visibility = Visibility.Collapsed;
                OrdersDataGrid.Visibility = Visibility.Visible;
            }
        }

        private async void InvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Verificar permisos - Solo Admin puede gestionar facturas
            if ((_currentUser.Role != "direccion" && _currentUser.Role != "administracion"))
            {
                MessageBox.Show(
                    "Solo el administrador puede gestionar facturas.",
                    "Acceso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var order = button?.Tag as OrderViewModel;

            if (order == null) return;

            try
            {
                // Verificar que la orden esté en estado correcto para facturar
                bool canInvoice = await _supabaseService.CanCreateInvoice(order.Id);

                if (!canInvoice)
                {
                    // Obtener el estado actual
                    var currentStatus = order.Status;

                    string message = currentStatus switch
                    {
                        "CREADA" => "La orden debe estar EN PROCESO para poder facturar.\n" +
                                   "Cambie el estado de la orden primero.",
                        
                        "CANCELADA" => "Esta orden está cancelada.\n" +
                                     "No se pueden gestionar facturas.",
                        _ => $"No se pueden gestionar facturas en el estado: {currentStatus}"
                    };

                    MessageBox.Show(
                        message,
                        "Facturación No Disponible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Abrir ventana de gestión de facturas
                var invoiceWindow = new InvoiceManagementWindow(order.Id, _currentUser);
                invoiceWindow.ShowDialog();

                // Solo actualizar esta orden específica para reflejar montos facturados
                await RefreshSingleOrder(order.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir gestión de facturas:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }
        }


        //El botón para adminsitrar clientes
        private async void ClientsManagementButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Abrir la ventana de gestión de clientes
                var clientsWindow = new ClientManagementWindow(_currentUser);
                clientsWindow.ShowDialog();

                // Solo recargar catálogos de clientes/vendedores (no las órdenes)
                _clients = await _supabaseService.GetClients();
                _vendors = await _supabaseService.GetVendors();

                System.Diagnostics.Debug.WriteLine($"✅ Catálogos actualizados: {_clients.Count} clientes, {_vendors.Count} vendedores");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir módulo de clientes:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}