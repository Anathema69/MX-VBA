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
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorDashboard : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<VendorCommissionCardViewModel> _allCommissions;
        private ObservableCollection<VendorCommissionCardViewModel> _filteredCommissions;
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");
        private int? _vendorId;
        private string _currentFilter = "all";

        public VendorDashboard(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _allCommissions = new ObservableCollection<VendorCommissionCardViewModel>();
            _filteredCommissions = new ObservableCollection<VendorCommissionCardViewModel>();

            InitializeUI();
            _ = LoadVendorDataAsync();
        }

        private void InitializeUI()
        {
            Title = $"Mis Comisiones - {_currentUser.FullName}";
            CommissionsItemsControl.ItemsSource = _filteredCommissions;

            // Configurar avatar
            VendorNameText.Text = $"Bienvenido, {_currentUser.FullName}";
            VendorInitials.Text = GetInitials(_currentUser.FullName);
        }

        private async Task LoadVendorDataAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // 1. Obtener el ID del vendedor basado en el usuario actual
                var vendorResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.UserId == _currentUser.Id)
                    .Single();

                if (vendorResponse == null)
                {
                    ShowNoDataMessage("No se encontró información del vendedor");
                    return;
                }

                _vendorId = vendorResponse.Id;

                // 2. Cargar comisiones
                await LoadVendorCommissions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadVendorCommissions()
        {
            if (!_vendorId.HasValue) return;

            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // 1. CORRECCIÓN: Usar Filter con In para múltiples estados
                var commissionsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Select("*")
                    .Where(x => x.VendorId == _vendorId.Value)
                    .Filter("payment_status", Postgrest.Constants.Operator.In, new[] { "draft", "pending", "paid" })
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var commissions = commissionsResponse?.Models ?? new List<VendorCommissionPaymentDb>();

                if (commissions.Count == 0)
                {
                    ShowNoDataMessage("¡No tienes comisiones registradas!");
                    _allCommissions.Clear();
                    ApplyFilters();
                    return;
                }

                HideNoDataMessage();

                // Resto del código sin cambios...
                var orderIds = commissions.Select(c => c.OrderId).Distinct().ToList();

                // 2. Cargar las órdenes
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Get();

                var allOrders = ordersResponse?.Models ?? new List<OrderDb>();
                var orders = allOrders.Where(o => orderIds.Contains(o.Id))
                    .ToDictionary(o => o.Id);

                // 3. Cargar los clientes
                var clientIds = orders.Values.Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value).Distinct().ToList();

                var clientsResponse = await supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();

                var allClients = clientsResponse?.Models ?? new List<ClientDb>();
                var clients = allClients.Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id);

                // 4. Construir los ViewModels
                _allCommissions.Clear();
                decimal totalPending = 0;
                decimal totalPaid = 0;
                decimal totalDraft = 0;
                int pendingCount = 0;
                int paidCount = 0;
                int draftCount = 0;
                int totalCount = 0;

                foreach (var commission in commissions)
                {
                    OrderDb order = orders.ContainsKey(commission.OrderId) ? orders[commission.OrderId] : null;
                    ClientDb client = null;

                    if (order?.ClientId.HasValue == true && clients.ContainsKey(order.ClientId.Value))
                    {
                        client = clients[order.ClientId.Value];
                    }

                    var cardVm = new VendorCommissionCardViewModel
                    {
                        OrderId = commission.OrderId,
                        CommissionId = commission.Id,
                        OrderNumber = order?.Po ?? $"ORD-{commission.OrderId}",
                        OrderDate = order?.PoDate ?? DateTime.Now,
                        ClientName = client?.Name ?? "Sin Cliente",
                        CommissionAmount = commission.CommissionAmount,
                        CommissionAmountFormatted = commission.CommissionAmount.ToString("C", _cultureMX),
                        Status = commission.PaymentStatus,
                        PaymentDate = commission.PaymentDate
                    };

                    _allCommissions.Add(cardVm);

                    if (commission.PaymentStatus == "pending")
                    {
                        totalPending += commission.CommissionAmount;
                        pendingCount++;
                    }
                    else if (commission.PaymentStatus == "paid")
                    {
                        totalPaid += commission.CommissionAmount;
                        paidCount++;
                    }
                    else if (commission.PaymentStatus == "draft")
                    {
                        totalDraft += commission.CommissionAmount;
                        draftCount++;
                    }
                }

                // paidCount + draftCount+ draftCount será una sola variable
                totalCount = pendingCount + paidCount + draftCount;

                // 5. Actualizar resúmenes
                UpdateSummaryCards(totalPending, totalPaid, pendingCount, totalCount, totalDraft);

                // 6. Aplicar filtros
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar comisiones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSummaryCards(decimal totalPending, decimal totalPaid, int pendingCount, int totalCount, decimal totalDraft = 0)
        {
            TotalDraftText.Text = totalDraft.ToString("C", _cultureMX);
            TotalPendingText.Text = totalPending.ToString("C", _cultureMX);
            TotalPaidText.Text = totalPaid.ToString("C", _cultureMX);
            PendingCountText.Text = pendingCount.ToString();
            TotalCountText.Text = totalCount.ToString();
        }

        private void ApplyFilters()
        {
            _filteredCommissions.Clear();

            // Ordenar: draft primero, luego pending, luego paid
            var sorted = _allCommissions
                .OrderBy(c => c.Status == "draft" ? 0 : c.Status == "pending" ? 1 : 2)
                .ThenByDescending(c => c.OrderDate)
                .ToList();

            foreach (var commission in sorted)
            {
                _filteredCommissions.Add(commission);
            }

            if (_filteredCommissions.Count == 0)
            {
                NoDataPanel.Visibility = Visibility.Visible;
                NoDataTitle.Text = "Sin comisiones";
                NoDataMessage.Text = "No hay comisiones para mostrar";
            }
            else
            {
                NoDataPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowNoDataMessage(string message)
        {
            NoDataPanel.Visibility = Visibility.Visible;
            NoDataTitle.Text = "Sin comisiones";
            NoDataMessage.Text = message;
        }

        private void ShowFilteredNoDataMessage()
        {
            NoDataPanel.Visibility = Visibility.Visible;

            switch (_currentFilter)
            {
                case "pending":
                    NoDataTitle.Text = "Sin comisiones pendientes";
                    NoDataMessage.Text = "No tienes comisiones pendientes de pago";
                    break;
                case "paid":
                    NoDataTitle.Text = "Sin comisiones pagadas";
                    NoDataMessage.Text = "Aún no se han pagado comisiones";
                    break;
                default:
                    NoDataTitle.Text = "Sin comisiones";
                    NoDataMessage.Text = "No hay comisiones para mostrar";
                    break;
            }
        }

        private void HideNoDataMessage()
        {
            NoDataPanel.Visibility = Visibility.Collapsed;
        }

        

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "¿Está seguro que desea cerrar sesión?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Usar el método centralizado de App para cerrar sesión
                // Esto cierra TODAS las ventanas excepto la de login y detiene el timeout service
                var app = (App)Application.Current;
                app.ForceLogout("Usuario cerró sesión manualmente", "Sesión cerrada exitosamente.");
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
    }

    // ViewModel específico para el vendedor
    public class VendorCommissionCardViewModel : INotifyPropertyChanged
    {
        public int OrderId { get; set; }
        public int CommissionId { get; set; }
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string ClientName { get; set; }
        public decimal CommissionAmount { get; set; }
        public string CommissionAmountFormatted { get; set; }
        public string Status { get; set; }
        public DateTime? PaymentDate { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}