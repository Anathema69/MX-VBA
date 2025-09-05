using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Lógica de interacción para VendorCardsDemo.xaml
    /// </summary>
    public partial class VendorCardsDemo : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<CommissionCardViewModel> _allCommissions;
        private ObservableCollection<CommissionCardViewModel> _displayedCommissions;
        private string _currentStatusFilter = "Todas";
        private string _currentVendorFilter = "Todos los Vendedores";
        private string _currentSort = "Estado y Monto";

        public VendorCardsDemo(UserSession currentUser)
        {
            // PRIMERO: Inicializar las colecciones
            _allCommissions = new ObservableCollection<CommissionCardViewModel>();
            _displayedCommissions = new ObservableCollection<CommissionCardViewModel>();

            // SEGUNDO: Inicializar componentes
            InitializeComponent();

            // TERCERO: Asignar el resto
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            // CUARTO: Vincular la colección al ItemsControl
            CommissionsItemsControl.ItemsSource = _displayedCommissions;

            InitializeUI();
            _ = LoadCommissionsAsync();
        }

        private void InitializeUI()
        {
            Title = $"Portal de Comisiones - {_currentUser.FullName}";
        }

        private async Task LoadCommissionsAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // 1. Cargar registros de t_vendor_commission_payment
                var commissionsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Select("*")
                    .Order("payment_status", Postgrest.Constants.Ordering.Ascending)
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var commissions = commissionsResponse?.Models ?? new System.Collections.Generic.List<VendorCommissionPaymentDb>();

                if (commissions.Count == 0)
                {
                    MessageBox.Show("No se encontraron comisiones registradas", "Información",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatistics(); // Actualizar estadísticas con valores en 0
                    return;
                }

                // Obtener IDs únicos
                var orderIds = commissions.Select(c => c.OrderId).Distinct().ToList();
                var vendorIds = commissions.Select(c => c.VendorId).Distinct().ToList();

                // 2. Cargar TODAS las órdenes y filtrar en memoria
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Get();

                var allOrders = ordersResponse?.Models ?? new System.Collections.Generic.List<OrderDb>();
                var orders = allOrders.Where(o => orderIds.Contains(o.Id))
                    .ToDictionary(o => o.Id);

                // 3. Cargar TODOS los vendedores y filtrar en memoria
                var vendorsResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Select("*")
                    .Get();

                var allVendors = vendorsResponse?.Models ?? new System.Collections.Generic.List<VendorTableDb>();
                var vendors = allVendors.Where(v => vendorIds.Contains(v.Id))
                    .ToDictionary(v => v.Id);

                // Llenar el combo de vendedores
                if (VendorFilterCombo != null)
                {
                    VendorFilterCombo.Items.Clear();
                    VendorFilterCombo.Items.Add("Todos los Vendedores");
                    foreach (var vendor in vendors.Values.OrderBy(v => v.VendorName))
                    {
                        VendorFilterCombo.Items.Add(vendor.VendorName);
                    }
                    VendorFilterCombo.SelectedIndex = 0;
                }

                // 4. Cargar TODOS los clientes y filtrar en memoria
                var clientIds = orders.Values.Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value).Distinct().ToList();

                var clientsResponse = await supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();

                var allClients = clientsResponse?.Models ?? new System.Collections.Generic.List<ClientDb>();
                var clients = allClients.Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id);

                // 5. Construir los ViewModels
                _allCommissions.Clear();

                foreach (var commission in commissions)
                {
                    // Obtener información relacionada
                    OrderDb order = orders.ContainsKey(commission.OrderId) ? orders[commission.OrderId] : null;
                    VendorTableDb vendor = vendors.ContainsKey(commission.VendorId) ? vendors[commission.VendorId] : null;
                    ClientDb client = null;

                    if (order?.ClientId.HasValue == true && clients.ContainsKey(order.ClientId.Value))
                    {
                        client = clients[order.ClientId.Value];
                    }

                    var cardVm = new CommissionCardViewModel
                    {
                        // IDs
                        CommissionPaymentId = commission.Id,
                        OrderId = commission.OrderId,
                        VendorId = commission.VendorId,

                        // Información de orden
                        OrderNumber = order?.Po ?? $"ORD-{commission.OrderId}",
                        OrderDate = order?.PoDate ?? DateTime.Now,

                        // Información del vendedor
                        VendorName = vendor?.VendorName ?? "Sin Vendedor",

                        // Información del cliente
                        ClientName = client?.Name ?? "Sin Cliente",
                        ClientInitial = GetInitial(client?.Name),

                        // Información financiera
                        Subtotal = order?.SaleSubtotal ?? 0,
                        CommissionRate = commission.CommissionRate,
                        CommissionAmount = commission.CommissionAmount,

                        // Estado
                        Status = commission.PaymentStatus == "paid" ? "PAGADO" : "PENDIENTE",
                        PaymentDate = commission.PaymentDate,

                        // Notas
                        Notes = commission.Notes ?? ""
                    };

                    _allCommissions.Add(cardVm);
                }

                // 6. Aplicar filtro inicial y actualizar UI
                ApplyFilters();
                UpdateStatistics();

                // No mostrar mensaje, solo actualizar la UI
                System.Diagnostics.Debug.WriteLine($"Se cargaron {_allCommissions.Count} comisiones");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar comisiones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetInitial(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            var words = name.Trim().Split(' ');
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();

            return name.Substring(0, Math.Min(2, name.Length)).ToUpper();
        }

        private void ApplyFilters()
        {
            // Validación de seguridad
            if (_displayedCommissions == null)
            {
                _displayedCommissions = new ObservableCollection<CommissionCardViewModel>();
                if (CommissionsItemsControl != null)
                    CommissionsItemsControl.ItemsSource = _displayedCommissions;
            }

            if (_allCommissions == null)
            {
                _allCommissions = new ObservableCollection<CommissionCardViewModel>();
                return;
            }


            _displayedCommissions.Clear();

            var filtered = _allCommissions.AsEnumerable();

            // Aplicar filtro de estado
            if (_currentStatusFilter == "⚠️ Pendientes")
            {
                filtered = filtered.Where(c => c.Status == "PENDIENTE");
            }
            else if (_currentStatusFilter == "✅ Pagadas")
            {
                filtered = filtered.Where(c => c.Status == "PAGADO");
            }

            // Aplicar filtro de vendedor
            if (_currentVendorFilter != "Todos los Vendedores")
            {
                filtered = filtered.Where(c => c.VendorName == _currentVendorFilter);
            }

            // Aplicar ordenamiento
            IOrderedEnumerable<CommissionCardViewModel> sorted = null;

            switch (_currentSort)
            {
                case "Estado y Monto":
                    sorted = filtered
                        .OrderBy(c => c.Status == "PENDIENTE" ? 0 : 1)
                        .ThenByDescending(c => c.CommissionAmount);
                    break;
                case "Monto (Mayor a menor)":
                    sorted = filtered.OrderByDescending(c => c.CommissionAmount);
                    break;
                case "Monto (Menor a mayor)":
                    sorted = filtered.OrderBy(c => c.CommissionAmount);
                    break;
                case "Fecha (Más reciente)":
                    sorted = filtered.OrderByDescending(c => c.OrderDate);
                    break;
                case "Vendedor (A-Z)":
                    sorted = filtered.OrderBy(c => c.VendorName);
                    break;
                default:
                    sorted = filtered.OrderBy(c => c.Status == "PENDIENTE" ? 0 : 1)
                        .ThenByDescending(c => c.CommissionAmount);
                    break;
            }

            foreach (var commission in sorted)
            {
                _displayedCommissions.Add(commission);
            }
        }

        private void UpdateStatistics()
        {
            // Total pendiente
            var totalPending = _allCommissions
                .Where(c => c.Status == "PENDIENTE")
                .Sum(c => c.CommissionAmount);

            // Total pagado
            var totalPaid = _allCommissions
                .Where(c => c.Status == "PAGADO")
                .Sum(c => c.CommissionAmount);

            // Vendedores únicos
            var uniqueVendors = _allCommissions
                .Select(c => c.VendorId)
                .Distinct()
                .Count();

            // Actualizar los TextBlocks en el XAML
            TotalPendingText.Text = totalPending.ToString("C");
            TotalPaidText.Text = totalPaid.ToString("C");
            UniqueVendorsText.Text = uniqueVendors.ToString();
        }

        // Eventos de filtros
        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Evitar llamadas durante la inicialización
            if (!IsLoaded || _displayedCommissions == null) return;

            if (StatusFilterCombo?.SelectedItem is ComboBoxItem item)
            {
                _currentStatusFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void VendorFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _displayedCommissions == null) return;

            if (VendorFilterCombo?.SelectedItem is string vendorName)
            {
                _currentVendorFilter = vendorName;
                ApplyFilters();
            }
            else if (VendorFilterCombo?.SelectedItem is ComboBoxItem item)
            {
                _currentVendorFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _displayedCommissions == null) return;

            if (SortCombo?.SelectedItem is ComboBoxItem item)
            {
                _currentSort = item.Content.ToString();
                ApplyFilters();
            }
        }

        // Método para abrir gestión de vendedores
        private void ManageVendors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vendorManagementWindow = new VendorManagementWindow(_currentUser);
                vendorManagementWindow.ShowDialog();

                // Recargar datos después de cerrar
                _ = LoadCommissionsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir gestión de vendedores: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Evento del botón Marcar como Pagado
        private void MarkAsPaid_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as CommissionCardViewModel;

            if (commission != null)
            {
                _ = MarkCommissionAsPaid(commission);
            }
        }

        private async Task MarkCommissionAsPaid(CommissionCardViewModel commission)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Actualizar el registro en la base de datos
                var update = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(x => x.Id == commission.CommissionPaymentId)
                    .Set(x => x.PaymentStatus, "paid")
                    .Set(x => x.PaymentDate, DateTime.Now)
                    .Set(x => x.UpdatedBy, _currentUser.Id)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                if (update != null)
                {
                    

                    // Recargar datos
                    await LoadCommissionsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al marcar como pagada: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Métodos para edición de comisión (preparados para siguiente fase)
        private void Commission_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                MessageBox.Show("Edición de porcentaje - Función en desarrollo", "Demo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Commission_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Validación para cuando se habilite la edición
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            var regex = new System.Text.RegularExpressions.Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(newText);
        }

        private void Commission_LostFocus(object sender, RoutedEventArgs e)
        {
            // Para cuando se habilite la edición
        }

        private void Commission_GotFocus(object sender, RoutedEventArgs e)
        {
            // Para cuando se habilite la edición
        }
    }

    // ViewModel para las cards
    public class CommissionCardViewModel : INotifyPropertyChanged
    {
        // IDs de base de datos
        public int CommissionPaymentId { get; set; }
        public int OrderId { get; set; }
        public int VendorId { get; set; }

        // Información de la orden
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }

        // Información del vendedor
        public string VendorName { get; set; }

        // Información del cliente
        public string ClientName { get; set; }
        public string ClientInitial { get; set; }

        // Información financiera
        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedSubtotal));
            }
        }

        private decimal _commissionRate;
        public decimal CommissionRate
        {
            get => _commissionRate;
            set
            {
                _commissionRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedRate));
                // Recalcular comisión cuando cambie el porcentaje
                CommissionAmount = (_subtotal * _commissionRate) / 100;
            }
        }

        private decimal _commissionAmount;
        public decimal CommissionAmount
        {
            get => _commissionAmount;
            set
            {
                _commissionAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedCommission));
            }
        }

        // Estado
        private string _status;
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsPaid));
            }
        }

        public DateTime? PaymentDate { get; set; }
        public string Notes { get; set; }

        // Propiedades calculadas para el binding
        public string FormattedSubtotal => Subtotal.ToString("C");
        public string FormattedCommission => CommissionAmount.ToString("C");
        public string FormattedRate => $"{CommissionRate:F2}%";
        public bool IsPending => Status == "PENDIENTE";
        public bool IsPaid => Status == "PAGADO";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}