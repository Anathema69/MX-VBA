using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;


using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Lógica de interacción para PendingIncomesDetailView.xaml
    /// </summary>
    public partial class PendingIncomesDetailView : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private readonly int _clientId;
        private readonly string _clientName;
        private ObservableCollection<InvoiceDetailViewModel> _invoices;
        private ClientDb _client;

        public PendingIncomesDetailView(UserSession currentUser, int clientId, string clientName)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _clientId = clientId;
            _clientName = clientName;
            _supabaseService = SupabaseService.Instance;

            _invoices = new ObservableCollection<InvoiceDetailViewModel>();
            InvoicesDataGrid.ItemsSource = _invoices;

            // Establecer nombre del cliente
            ClientNameText.Text = _clientName;

            // Generar iniciales
            var words = _clientName.Split(' ');
            if (words.Length >= 2)
                ClientInitialsText.Text = $"{words[0][0]}{words[1][0]}".ToUpper();
            else
                ClientInitialsText.Text = _clientName.Substring(0, Math.Min(2, _clientName.Length)).ToUpper();

            // Mostrar estado de carga inicial
            ShowLoadingState();

            // Cargar datos
            _ = LoadClientInvoicesAsync();
        }

        private void ShowLoadingState()
        {
            DetailStatusText.Text = "Cargando información...";
            DetailTotalPendingText.Text = "---";
            DetailTotalOverdueText.Text = "---";
            DetailTotalDueSoonText.Text = "---";
            DetailInvoiceCountText.Text = "---";
        }

        private async Task LoadClientInvoicesAsync()
        {
            try
            {
                // Mostrar estado de carga
                DetailStatusText.Text = "Cargando facturas...";

                // Llamada optimizada
                var data = await _supabaseService.GetClientInvoicesDetail(_clientId);

                if (data.Client != null)
                {
                    _client = data.Client;
                    CreditDaysText.Text = _client.Credit.ToString();
                }

                _invoices.Clear();

                decimal totalPending = 0;
                decimal totalOverdue = 0;
                decimal totalDueSoon = 0;
                int overdueCount = 0;
                int dueSoonCount = 0;

                DateTime today = DateTime.Today;
                DateTime dueSoonDate = today.AddDays(7);

                foreach (var invoiceInfo in data.Invoices)
                {
                    var invoice = invoiceInfo.Invoice;

                    var viewModel = new InvoiceDetailViewModel
                    {
                        InvoiceId = invoice.Id,
                        Folio = invoice.Folio,
                        OrderPO = invoiceInfo.OrderPO, // Ahora sí tenemos el PO correcto
                        Total = invoice.Total ?? 0,
                        InvoiceDate = invoice.InvoiceDate,
                        ReceptionDate = invoice.ReceptionDate,
                        DueDate = invoice.DueDate
                    };

                    // Calcular estado y días
                    if (invoice.DueDate.HasValue)
                    {
                        var days = (invoice.DueDate.Value - today).Days;

                        if (days < 0)
                        {
                            viewModel.Status = "VENCIDA";
                            viewModel.DaysOverdue = Math.Abs(days);
                            viewModel.DaysText = $"{Math.Abs(days)}↓";
                            totalOverdue += viewModel.Total;
                            overdueCount++;
                        }
                        else if (days <= 7)
                        {
                            viewModel.Status = "POR VENCER";
                            viewModel.DaysUntilDue = days;
                            viewModel.DaysText = $"{days}↑";
                            totalDueSoon += viewModel.Total;
                            dueSoonCount++;
                        }
                        else
                        {
                            viewModel.Status = "AL CORRIENTE";
                            viewModel.DaysUntilDue = days;
                            viewModel.DaysText = $"{days}↑";
                        }
                    }
                    else
                    {
                        viewModel.Status = "SIN FECHA";
                        viewModel.DaysText = "---";
                    }

                    totalPending += viewModel.Total;
                    _invoices.Add(viewModel);
                }

                // Ya están ordenadas desde el servicio, no necesitamos ordenar de nuevo

                // Actualizar totales
                var culture = new CultureInfo("es-MX");
                DetailTotalPendingText.Text = totalPending.ToString("C", culture);
                DetailTotalOverdueText.Text = totalOverdue.ToString("C", culture);
                DetailTotalDueSoonText.Text = totalDueSoon.ToString("C", culture);
                DetailInvoiceCountText.Text = _invoices.Count.ToString();

                DetailLastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
                DetailStatusText.Text = $"{_invoices.Count} facturas cargadas";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar las facturas: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DetailStatusText.Text = "Error al cargar";
            }
        }

        private async void RefreshDetailButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDetailButton.IsEnabled = false;
            await LoadClientInvoicesAsync();
            RefreshDetailButton.IsEnabled = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        
    }

    // ViewModel para el detalle de facturas
    public class InvoiceDetailViewModel : INotifyPropertyChanged
    {
        private int _invoiceId;
        private string _folio;
        private string _orderPO;
        private decimal _total;
        private DateTime? _invoiceDate;
        private DateTime? _receptionDate;
        private DateTime? _dueDate;
        private string _status;
        private int _daysOverdue;
        private int _daysUntilDue;
        private string _daysText;

        public int InvoiceId
        {
            get => _invoiceId;
            set { _invoiceId = value; OnPropertyChanged(); }
        }

        public string Folio
        {
            get => _folio;
            set { _folio = value; OnPropertyChanged(); }
        }

        public string OrderPO
        {
            get => _orderPO;
            set { _orderPO = value; OnPropertyChanged(); }
        }

        public decimal Total
        {
            get => _total;
            set { _total = value; OnPropertyChanged(); }
        }

        public DateTime? InvoiceDate
        {
            get => _invoiceDate;
            set { _invoiceDate = value; OnPropertyChanged(); }
        }

        public DateTime? ReceptionDate
        {
            get => _receptionDate;
            set { _receptionDate = value; OnPropertyChanged(); }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set { _dueDate = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public int DaysOverdue
        {
            get => _daysOverdue;
            set { _daysOverdue = value; OnPropertyChanged(); }
        }

        public int DaysUntilDue
        {
            get => _daysUntilDue;
            set { _daysUntilDue = value; OnPropertyChanged(); }
        }

        public string DaysText
        {
            get => _daysText;
            set { _daysText = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}