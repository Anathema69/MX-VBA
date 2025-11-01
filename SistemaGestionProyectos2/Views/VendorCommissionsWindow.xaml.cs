using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorCommissionsWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<VendorSummaryViewModel> _vendors;
        private ObservableCollection<CommissionDetailViewModel> _vendorCommissions;
        private VendorSummaryViewModel _selectedVendor;
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");

        public VendorCommissionsWindow(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _vendors = new ObservableCollection<VendorSummaryViewModel>();
            _vendorCommissions = new ObservableCollection<CommissionDetailViewModel>();

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            InitializeUI();
            _ = LoadVendorsWithCommissions();
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
            Title = $"Gestión de Comisiones - {_currentUser.FullName}";
            VendorsListBox.ItemsSource = _vendors;
            CommissionsItemsControl.ItemsSource = _vendorCommissions;
        }

        private async Task LoadVendorsWithCommissions()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // 1. Cargar TODAS las comisiones (draft y pending)
                var commissionsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(c => c.PaymentStatus == "draft" || c.PaymentStatus == "pending")
                    .Select("*")
                    .Order("f_vendor", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var allCommissions = commissionsResponse?.Models ?? new List<VendorCommissionPaymentDb>();

                if (allCommissions.Count == 0)
                {
                    _vendors.Clear();
                    _vendorCommissions.Clear();
                    UpdateTotalPending(0);

                    EmptyStatePanel.Visibility = Visibility.Visible;
                    CommissionsDetailPanel.Visibility = Visibility.Collapsed;

                    var emptyPanel = EmptyStatePanel.Children[0] as StackPanel;
                    if (emptyPanel != null)
                    {
                        var textBlocks = emptyPanel.Children.OfType<TextBlock>().ToList();
                        if (textBlocks.Count >= 2)
                        {
                            textBlocks[1].Text = "No hay comisiones";
                            textBlocks[2].Text = "No se encontraron comisiones para ningún vendedor";
                        }
                    }
                    return;
                }

                // 2. Agrupar por vendedor
                var vendorGroups = allCommissions.GroupBy(c => c.VendorId);

                // 3. Cargar información de vendedores
                var vendorIds = vendorGroups.Select(g => g.Key).ToList();
                var vendorsResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Filter("f_vendor", Postgrest.Constants.Operator.In, vendorIds)
                    .Get();

                var vendors = vendorsResponse?.Models?.ToDictionary(v => v.Id) ?? new Dictionary<int, VendorTableDb>();

                // 4. Construir ViewModels de vendedores
                _vendors.Clear();
                decimal totalPendingGlobal = 0;

                foreach (var group in vendorGroups)
                {
                    var vendor = vendors.ContainsKey(group.Key) ? vendors[group.Key] : null;
                    if (vendor == null) continue;

                    var vendorCommissions = group.ToList();

                    // Separar por estado
                    var pendingCommissions = vendorCommissions.Where(c => c.PaymentStatus == "pending").ToList();
                    var draftCommissions = vendorCommissions.Where(c => c.PaymentStatus == "draft").ToList();

                    // Calcular totales
                    decimal totalPending = pendingCommissions.Sum(c => c.CommissionAmount);
                    decimal totalDraft = draftCommissions.Sum(c => c.CommissionAmount);
                    decimal totalAll = totalPending + totalDraft;

                    totalPendingGlobal += totalPending;

                    var vendorVm = new VendorSummaryViewModel
                    {
                        VendorId = vendor.Id,
                        VendorName = vendor.VendorName ?? "Sin nombre",
                        Initials = GetInitials(vendor.VendorName),
                        TotalCount = vendorCommissions.Count,  // Total de comisiones
                        PendingCount = pendingCommissions.Count,  // Mantener para lógica interna
                        DraftCount = draftCommissions.Count,  // Mantener para lógica interna
                        TotalPending = totalPending,
                        TotalDraft = totalDraft,
                        TotalAll = totalAll,
                        TotalPendingFormatted = totalPending.ToString("C", _cultureMX),
                        TotalDraftFormatted = totalDraft.ToString("C", _cultureMX),
                        TotalAllFormatted = totalAll.ToString("C", _cultureMX),
                        AvatarColor1 = GetRandomColor(vendor.Id),
                        AvatarColor2 = GetRandomColor(vendor.Id + 100),
                        HasPendingPayments = pendingCommissions.Count > 0
                    };

                    _vendors.Add(vendorVm);
                }

                // 5. Ordenar: primero los que tienen comisiones pendientes, luego por monto total
                var sortedVendors = _vendors
                    .OrderByDescending(v => v.HasPendingPayments)
                    .ThenByDescending(v => v.TotalPending)
                    .ThenByDescending(v => v.TotalAll)
                    .ToList();

                _vendors.Clear();
                foreach (var vendor in sortedVendors)
                {
                    _vendors.Add(vendor);
                }

                UpdateTotalPending(totalPendingGlobal);

                // Seleccionar el primer vendedor automáticamente
                if (_vendors.Count > 0)
                {
                    VendorsListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar vendedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void VendorsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VendorsListBox.SelectedItem is VendorSummaryViewModel vendor)
            {
                _selectedVendor = vendor;
                await LoadVendorCommissions(vendor.VendorId);

                EmptyStatePanel.Visibility = Visibility.Collapsed;
                CommissionsDetailPanel.Visibility = Visibility.Visible;

                SelectedVendorInitials.Text = vendor.Initials;
                SelectedVendorName.Text = vendor.VendorName;

                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 1);
                gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(vendor.AvatarColor1), 0));
                gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(vendor.AvatarColor2), 1));
                SelectedVendorAvatar.Background = gradientBrush;
            }
        }

        private async Task LoadVendorCommissions(int vendorId)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // 1. Cargar TODAS las comisiones del vendedor (draft y pending)
                var commissionsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(c => c.VendorId == vendorId)
                    .Where(c => c.PaymentStatus == "draft" || c.PaymentStatus == "pending")
                    .Select("*")
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var commissions = commissionsResponse?.Models ?? new List<VendorCommissionPaymentDb>();

                if (commissions.Count == 0)
                {
                    _vendorCommissions.Clear();
                    UpdateVendorSummary(0, 0, 0);
                    return;
                }

                // 2. Cargar información de órdenes
                var orderIds = commissions.Select(c => c.OrderId).Distinct().ToList();
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.In, orderIds)
                    .Get();

                var orders = ordersResponse?.Models?.ToDictionary(o => o.Id) ?? new Dictionary<int, OrderDb>();

                // 3. Cargar información de clientes
                var clientIds = orders.Values.Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value).Distinct().ToList();

                Dictionary<int, ClientDb> clients = new Dictionary<int, ClientDb>();
                if (clientIds.Count > 0)
                {
                    var clientsResponse = await supabaseClient
                        .From<ClientDb>()
                        .Filter("f_client", Postgrest.Constants.Operator.In, clientIds)
                        .Get();

                    clients = clientsResponse?.Models?.ToDictionary(c => c.Id) ?? new Dictionary<int, ClientDb>();
                }

                // 4. Construir ViewModels de comisiones
                _vendorCommissions.Clear();
                decimal totalPending = 0;
                decimal totalDraft = 0;
                decimal totalAll = 0;
                int pendingCount = 0;
                int draftCount = 0;

                var commissionViewModels = new List<CommissionDetailViewModel>();

                foreach (var commission in commissions)
                {
                    OrderDb order = orders.ContainsKey(commission.OrderId) ? orders[commission.OrderId] : null;
                    ClientDb client = null;

                    if (order?.ClientId.HasValue == true && clients.ContainsKey(order.ClientId.Value))
                    {
                        client = clients[order.ClientId.Value];
                    }

                    var commissionVm = new CommissionDetailViewModel
                    {
                        CommissionPaymentId = commission.Id,
                        OrderId = commission.OrderId,
                        VendorId = commission.VendorId,
                        OrderNumber = order?.Po ?? $"ORD-{commission.OrderId}",
                        OrderDate = order?.PoDate ?? DateTime.Now,
                        ClientName = client?.Name ?? "Sin Cliente",
                        Subtotal = order?.SaleSubtotal ?? 0,
                        SubtotalFormatted = (order?.SaleSubtotal ?? 0).ToString("C", _cultureMX),
                        CommissionRate = commission.CommissionRate,
                        CommissionAmount = commission.CommissionAmount,
                        CommissionAmountFormatted = commission.CommissionAmount.ToString("C", _cultureMX),
                        PaymentStatus = commission.PaymentStatus
                    };

                    commissionViewModels.Add(commissionVm);

                    if (commission.PaymentStatus == "pending")
                    {
                        totalPending += commission.CommissionAmount;
                        pendingCount++;
                    }
                    else if (commission.PaymentStatus == "draft")
                    {
                        totalDraft += commission.CommissionAmount;
                        draftCount++;
                    }
                }

                // 5. Ordenar: primero las pending, luego las draft, y dentro de cada grupo por fecha
                var sortedCommissions = commissionViewModels
                    .OrderBy(c => c.PaymentStatus == "pending" ? 0 : 1)
                    .ThenByDescending(c => c.OrderDate)
                    .ToList();

                foreach (var commission in sortedCommissions)
                {
                    _vendorCommissions.Add(commission);
                }

                totalAll = totalPending + totalDraft;

                // 6. Actualizar resúmenes
                VendorTotalPendingText.Text = totalPending.ToString("C", _cultureMX);
                VendorCommissionsCountText.Text = $"{pendingCount + draftCount}";
                VendorTotalAllText.Text = totalAll.ToString("C", _cultureMX);

                // Mostrar/ocultar botón de pagar todas (solo si hay pendientes)
                PayAllButton.Visibility = pendingCount > 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar comisiones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateVendorSummary(decimal totalPending, int count, decimal totalAll)
        {
            VendorTotalPendingText.Text = totalPending.ToString("C", _cultureMX);
            VendorCommissionsCountText.Text = count.ToString();
            VendorTotalAllText.Text = totalAll.ToString("C", _cultureMX);
        }

        private void UpdateTotalPending(decimal total)
        {
            TotalPendingText.Text = total.ToString("C", _cultureMX);
        }

        // Resto de métodos sin cambios (PayCommission_Click, MarkCommissionAsPaid, etc.)
        private async void PayCommission_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as CommissionDetailViewModel;

            if (commission != null && commission.PaymentStatus == "pending")
            {
                await MarkCommissionAsPaid(commission.CommissionPaymentId, button);
            }
        }

        private async void PayAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVendor == null || _vendorCommissions.Count == 0) return;

            // Solo pagar las que están en pending
            var pendingCommissions = _vendorCommissions.Where(c => c.PaymentStatus == "pending").ToList();
            if (pendingCommissions.Count > 0)
            {
                await MarkAllCommissionsAsPaid(pendingCommissions);
            }
        }

        private async Task MarkCommissionAsPaid(int commissionPaymentId, Button button = null)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Guardar el vendedor actual
                var currentVendorId = _selectedVendor?.VendorId;

                var update = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(x => x.Id == commissionPaymentId)
                    .Set(x => x.PaymentStatus, "paid")
                    .Set(x => x.PaymentDate, DateTime.Now)
                    .Set(x => x.UpdatedBy, _currentUser.Id)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                if (update != null)
                {
                    if (button != null)
                    {
                        ShowSuccessAnimation(button);
                    }

                    await Task.Delay(1500);

                    // Recargar solo las comisiones del vendedor actual
                    if (_selectedVendor != null)
                    {
                        await LoadVendorCommissions(_selectedVendor.VendorId);
                    }

                    // Recargar la lista pero mantener la selección
                    await LoadVendorsWithCommissionsKeepingSelection(currentVendorId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al marcar como pagada: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Nuevo método para recargar vendedores manteniendo la selección sin salir del vendedor actual
        private async Task LoadVendorsWithCommissionsKeepingSelection(int? vendorIdToSelect)
        {
            // Llamar al método original de carga
            await LoadVendorsWithCommissions();

            // Restaurar la selección si había un vendedor seleccionado
            if (vendorIdToSelect.HasValue)
            {
                var vendorToSelect = _vendors.FirstOrDefault(v => v.VendorId == vendorIdToSelect.Value);
                if (vendorToSelect != null)
                {
                    VendorsListBox.SelectedItem = vendorToSelect;
                }
            }
        }

        private async Task MarkAllCommissionsAsPaid(List<CommissionDetailViewModel> pendingCommissions)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                var paymentDate = DateTime.Now;
                var currentVendorId = _selectedVendor?.VendorId;  // Guardar vendedor actual
                int successCount = 0;

                foreach (var commission in pendingCommissions)
                {
                    var update = await supabaseClient
                        .From<VendorCommissionPaymentDb>()
                        .Where(x => x.Id == commission.CommissionPaymentId)
                        .Set(x => x.PaymentStatus, "paid")
                        .Set(x => x.PaymentDate, paymentDate)
                        .Set(x => x.UpdatedBy, _currentUser.Id)
                        .Set(x => x.UpdatedAt, paymentDate)
                        .Update();

                    if (update != null) successCount++;
                }

                ShowTemporaryNotification($"✓ Se pagaron {successCount} comisiones correctamente");

                await Task.Delay(1500);
                await LoadVendorsWithCommissionsKeepingSelection(currentVendorId);  // Mantener selección
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al marcar como pagadas: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Métodos de utilidad sin cambios
        private void ShowSuccessAnimation(Button button)
        {
            button.IsEnabled = false;
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78"));
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "✓", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 8, 0), Foreground = Brushes.White },
                    new TextBlock { Text = "Pagado", FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }
                }
            };

            var fadeAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                BeginTime = TimeSpan.FromMilliseconds(500)
            };
            button.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        private void ShowTemporaryNotification(string message)
        {
            var notification = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 50, 0, 0),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14
                }
            };

            if (this.Content is Grid mainGrid)
            {
                mainGrid.Children.Add(notification);
                Grid.SetColumnSpan(notification, 2);

                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
                notification.BeginAnimation(OpacityProperty, fadeIn);

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, e) =>
                {
                    var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
                    fadeOut.Completed += (s2, e2) => mainGrid.Children.Remove(notification);
                    notification.BeginAnimation(OpacityProperty, fadeOut);
                    timer.Stop();
                };
                timer.Start();
            }
        }

        // Eventos para edición de tasa - sin cambios
        private void CommissionRate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                var commission = textBox.Tag as CommissionDetailViewModel;
                if (commission != null && commission.PaymentStatus == "pending")
                {
                    commission.IsEditingRate = true;
                    textBox.IsReadOnly = false;
                    textBox.Text = commission.CommissionRate.ToString("F2");
                    textBox.SelectAll();
                    textBox.Focus();
                }
            }
        }

        // Resto de eventos sin cambios...
        private async void CommissionRate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox != null && !textBox.IsReadOnly)
                {
                    await SaveCommissionRate(textBox);
                    Keyboard.ClearFocus();
                }
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    var commission = textBox.Tag as CommissionDetailViewModel;
                    if (commission != null)
                    {
                        textBox.Text = $"{commission.CommissionRate:F2}%";
                        commission.IsEditingRate = false;
                        textBox.IsReadOnly = true;
                        Keyboard.ClearFocus();
                    }
                }
            }
        }

        private async void CommissionRate_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsReadOnly)
            {
                await SaveCommissionRate(textBox);
            }
        }

        private void CommissionRate_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !regex.IsMatch(newText);
        }

        private async Task SaveCommissionRate(TextBox textBox)
        {
            var commission = textBox.Tag as CommissionDetailViewModel;
            if (commission != null && commission.PaymentStatus == "pending")
            {
                string cleanText = textBox.Text.Replace("%", "").Trim();
                if (decimal.TryParse(cleanText, out decimal newRate) && newRate >= 0 && newRate <= 100)
                {
                    if (newRate != commission.CommissionRate)
                    {
                        try
                        {
                            var supabaseClient = _supabaseService.GetClient();
                            decimal newCommissionAmount = (commission.Subtotal * newRate) / 100;

                            var update = await supabaseClient
                                .From<VendorCommissionPaymentDb>()
                                .Where(x => x.Id == commission.CommissionPaymentId)
                                .Set(x => x.CommissionRate, newRate)
                                .Set(x => x.CommissionAmount, newCommissionAmount)
                                .Set(x => x.UpdatedBy, _currentUser.Id)
                                .Set(x => x.UpdatedAt, DateTime.Now)
                                .Update();

                            if (update != null)
                            {
                                commission.CommissionRate = newRate;
                                commission.CommissionAmount = newCommissionAmount;
                                commission.CommissionAmountFormatted = newCommissionAmount.ToString("C", _cultureMX);

                                textBox.Background = new SolidColorBrush(Color.FromArgb(50, 76, 175, 80));
                                await Task.Delay(500);
                                textBox.Background = Brushes.Transparent;

                                if (_selectedVendor != null)
                                {
                                    await LoadVendorCommissions(_selectedVendor.VendorId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error al actualizar tasa: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            textBox.Text = $"{commission.CommissionRate:F2}%";
                        }
                    }
                }
                else
                {
                    textBox.Text = $"{commission.CommissionRate:F2}%";
                }

                textBox.Text = $"{commission.CommissionRate:F2}%";
                commission.IsEditingRate = false;
                textBox.IsReadOnly = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ManageVendorsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vendorManagementWindow = new VendorManagementWindow(_currentUser);
                vendorManagementWindow.ShowDialog();
                _ = LoadVendorsWithCommissions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir gestión de vendedores: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "??";

            var words = name.Trim().Split(' ');
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();

            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private string GetRandomColor(int seed)
        {
            var colors = new[]
            {
                "#5B3FF9", "#7c5ce6", "#e84118", "#c23616",
                "#00b894", "#00cec9", "#0984e3", "#74b9ff"
            };
            return colors[Math.Abs(seed) % colors.Length];
        }
    }

    // ViewModels actualizados
    public class VendorSummaryViewModel : INotifyPropertyChanged
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string Initials { get; set; }
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int DraftCount { get; set; }
        public decimal TotalPending { get; set; }
        public decimal TotalDraft { get; set; }
        public decimal TotalAll { get; set; }
        public string TotalPendingFormatted { get; set; }
        public string TotalDraftFormatted { get; set; }
        public string TotalAllFormatted { get; set; }
        public string AvatarColor1 { get; set; }
        public string AvatarColor2 { get; set; }
        public bool HasPendingPayments { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CommissionDetailViewModel : INotifyPropertyChanged
    {
        private bool _isEditingRate;
        private decimal _commissionRate;
        private decimal _commissionAmount;
        private string _commissionAmountFormatted;
        private string _paymentStatus;

        public int CommissionPaymentId { get; set; }
        public int OrderId { get; set; }
        public int VendorId { get; set; }
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string ClientName { get; set; }
        public decimal Subtotal { get; set; }
        public string SubtotalFormatted { get; set; }

        public string PaymentStatus
        {
            get => _paymentStatus;
            set
            {
                _paymentStatus = value;
                OnPropertyChanged();
            }
        }

        public decimal CommissionRate
        {
            get => _commissionRate;
            set
            {
                _commissionRate = value;
                OnPropertyChanged();
            }
        }

        public decimal CommissionAmount
        {
            get => _commissionAmount;
            set
            {
                _commissionAmount = value;
                OnPropertyChanged();
            }
        }

        public string CommissionAmountFormatted
        {
            get => _commissionAmountFormatted;
            set
            {
                _commissionAmountFormatted = value;
                OnPropertyChanged();
            }
        }

        public bool IsEditingRate
        {
            get => _isEditingRate;
            set
            {
                _isEditingRate = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}