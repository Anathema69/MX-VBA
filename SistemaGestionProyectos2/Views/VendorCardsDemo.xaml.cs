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

        public VendorCardsDemo(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _allCommissions = new ObservableCollection<CommissionCardViewModel>();
            _displayedCommissions = new ObservableCollection<CommissionCardViewModel>();

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

                MessageBox.Show($"Se cargaron {_allCommissions.Count} comisiones", "Datos Cargados",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
            // Por ahora, mostrar todos ordenados: Pendientes primero, luego Pagados
            _displayedCommissions.Clear();

            var sorted = _allCommissions
                .OrderBy(c => c.Status == "PENDIENTE" ? 0 : 1)  // Pendientes primero
                .ThenByDescending(c => c.CommissionAmount)       // Por monto (mayor a menor)
                .ThenBy(c => c.OrderDate);                      // Por fecha

            foreach (var commission in sorted)
            {
                _displayedCommissions.Add(commission);
            }
        }

        private void UpdateStatistics()
        {
            // Total de órdenes
            var totalOrders = _allCommissions.Count;

            // Total pagado
            var totalPaid = _allCommissions
                .Where(c => c.Status == "PAGADO")
                .Sum(c => c.CommissionAmount);

            // Total pendiente
            var totalPending = _allCommissions
                .Where(c => c.Status == "PENDIENTE")
                .Sum(c => c.CommissionAmount);

            // Total vendedores únicos
            var uniqueVendors = _allCommissions
                .Select(c => c.VendorId)
                .Distinct()
                .Count();

            // Actualizar los TextBlocks en el XAML (necesitarás darles nombres x:Name)
            System.Diagnostics.Debug.WriteLine($"Total Órdenes: {totalOrders}");
            System.Diagnostics.Debug.WriteLine($"Pagadas: {totalPaid:C}");
            System.Diagnostics.Debug.WriteLine($"Pendientes: {totalPending:C}");
            System.Diagnostics.Debug.WriteLine($"Vendedores: {uniqueVendors}");
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

        // Eventos de botones (por ahora solo mostrar mensajes)
        private void EditCommission_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Edición de comisión - Función en desarrollo", "Demo",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MarkAsPaid_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Marcar como pagado - Función en desarrollo", "Demo",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
            }
        }

        // Estado
        public string Status { get; set; } // "PENDIENTE" o "PAGADO"
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