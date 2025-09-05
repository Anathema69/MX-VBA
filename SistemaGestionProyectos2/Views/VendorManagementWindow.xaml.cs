using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorManagementWindow : Window
    {
        private readonly UserSession _currentUser;
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<VendorViewModel> _vendors;
        private ObservableCollection<VendorViewModel> _filteredVendors;
        private VendorViewModel _selectedVendor;

        public VendorManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _supabaseService = SupabaseService.Instance;
            _vendors = new ObservableCollection<VendorViewModel>();
            _filteredVendors = new ObservableCollection<VendorViewModel>();

            InitializeUI();
            _ = LoadVendorsAsync();
        }

        private void InitializeUI()
        {
            Title = $"Gestión de Vendedores - {_currentUser.FullName}";

            // Usar ItemsControl en lugar de DataGrid
            VendorsItemsControl.ItemsSource = _filteredVendors;

            EditVendorButton.IsEnabled = false;
            DeleteVendorButton.IsEnabled = false;
        }

        private async Task LoadVendorsAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Cargar vendedores
                var vendorsResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Select("*")
                    .Order("f_vendorname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vendors = vendorsResponse?.Models ?? new System.Collections.Generic.List<VendorTableDb>();

                // Cargar usuarios asociados
                var usersResponse = await supabaseClient
                    .From<UserDb>()
                    .Where(u => u.Role == "salesperson")
                    .Get();

                var users = usersResponse?.Models?.ToDictionary(u => u.Id, u => u)
                    ?? new System.Collections.Generic.Dictionary<int, UserDb>();

                _vendors.Clear();

                foreach (var vendor in vendors)
                {
                    UserDb user = null;
                    if (vendor.UserId.HasValue && users.ContainsKey(vendor.UserId.Value))
                    {
                        user = users[vendor.UserId.Value];
                    }

                    var vendorVm = new VendorViewModel
                    {
                        Id = vendor.Id,
                        VendorName = vendor.VendorName,
                        Email = vendor.Email,
                        Phone = vendor.Phone ?? "Sin teléfono",
                        IsActive = vendor.IsActive,
                        UserId = vendor.UserId,
                        Username = user?.Username ?? "Sin usuario",
                        UserIsActive = user?.IsActive ?? false,
                        CommissionRate = vendor.CommissionRate
                    };

                    _vendors.Add(vendorVm);
                }

                ApplyFilter();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar vendedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var searchText = SearchBox?.Text?.ToLower() ?? "";
            _filteredVendors.Clear();

            var filtered = _vendors.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(v =>
                    v.VendorName.ToLower().Contains(searchText) ||
                    v.Email.ToLower().Contains(searchText) ||
                    v.Username.ToLower().Contains(searchText) ||
                    v.Phone.ToLower().Contains(searchText));
            }

            foreach (var vendor in filtered)
            {
                _filteredVendors.Add(vendor);
            }

            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            var totalCount = _filteredVendors.Count;
            var activeCount = _filteredVendors.Count(v => v.IsActive);
            var inactiveCount = totalCount - activeCount;

            // Actualizar los TextBlocks de estadísticas
            TotalVendorsText.Text = totalCount.ToString();
            ActiveVendorsText.Text = activeCount.ToString();
            InactiveVendorsText.Text = inactiveCount.ToString();

            // Calcular comisión promedio
            if (_filteredVendors.Any())
            {
                var avgCommission = _filteredVendors.Average(v => v.CommissionRate.HasValue ? v.CommissionRate.Value : 10);
                AvgCommissionText.Text = $"{avgCommission:F1}%";
            }
            else
            {
                AvgCommissionText.Text = "0%";
            }
        }

        // Eventos de botones principales
        private void AddVendor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VendorEditDialog(null);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadVendorsAsync();
            }
        }

        private void EditVendor_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVendor == null) return;

            var dialog = new VendorEditDialog(_selectedVendor);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadVendorsAsync();
            }
        }

        private async void DeleteVendor_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVendor == null) return;

            var action = _selectedVendor.IsActive ? "desactivar" : "activar";
            var result = MessageBox.Show(
                $"¿Está seguro de {action} al vendedor {_selectedVendor.VendorName}?\n\n" +
                $"Esto también {action}á su acceso al sistema.",
                $"Confirmar {action}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Actualizar estado del vendedor
                var vendorToUpdate = await supabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == _selectedVendor.Id)
                    .Single();

                if (vendorToUpdate != null)
                {
                    vendorToUpdate.IsActive = !_selectedVendor.IsActive;
                    await supabaseClient
                        .From<VendorTableDb>()
                        .Update(vendorToUpdate);
                }

                // Actualizar estado del usuario si existe
                if (_selectedVendor.UserId.HasValue)
                {
                    var userToUpdate = await supabaseClient
                        .From<UserDb>()
                        .Where(u => u.Id == _selectedVendor.UserId.Value)
                        .Single();

                    if (userToUpdate != null)
                    {
                        userToUpdate.IsActive = !_selectedVendor.IsActive;
                        await supabaseClient
                            .From<UserDb>()
                            .Update(userToUpdate);
                    }
                }

                await LoadVendorsAsync();
                MessageBox.Show($"Vendedor {action}do correctamente", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar estado: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Eventos de las cards
        private void VendorCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is VendorViewModel vendor)
            {
                // Seleccionar este vendedor
                _selectedVendor = vendor;

                // Habilitar botones de acción
                EditVendorButton.IsEnabled = true;
                DeleteVendorButton.IsEnabled = true;

                // Actualizar el texto del botón de desactivar/activar
                DeleteVendorButton.Content = vendor.IsActive ? "🗑️ Desactivar" : "✅ Activar";

                // Si fue doble clic, abrir el diálogo de edición
                if (e.ClickCount == 2)
                {
                    EditVendor_Click(null, null);
                }
            }
        }

        // Edición rápida desde el botón en la card
        private void QuickEdit_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is VendorViewModel vendor)
            {
                var dialog = new VendorEditDialog(vendor);
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    _ = LoadVendorsAsync();
                }
            }

            // Prevenir que el evento suba al Border padre
            e.Handled = true;
        }

        // Cambio rápido de estado desde el botón en la card
        private async void QuickToggle_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is VendorViewModel vendor)
            {
                var action = vendor.IsActive ? "desactivar" : "activar";
                var result = MessageBox.Show(
                    $"¿Está seguro de {action} al vendedor {vendor.VendorName}?",
                    $"Confirmar {action}",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    var supabaseClient = _supabaseService.GetClient();

                    // Actualizar estado del vendedor
                    var vendorToUpdate = await supabaseClient
                        .From<VendorTableDb>()
                        .Where(v => v.Id == vendor.Id)
                        .Single();

                    if (vendorToUpdate != null)
                    {
                        vendorToUpdate.IsActive = !vendor.IsActive;
                        await supabaseClient
                            .From<VendorTableDb>()
                            .Update(vendorToUpdate);
                    }

                    // Actualizar estado del usuario si existe
                    if (vendor.UserId.HasValue)
                    {
                        var userToUpdate = await supabaseClient
                            .From<UserDb>()
                            .Where(u => u.Id == vendor.UserId.Value)
                            .Single();

                        if (userToUpdate != null)
                        {
                            userToUpdate.IsActive = !vendor.IsActive;
                            await supabaseClient
                                .From<UserDb>()
                                .Update(userToUpdate);
                        }
                    }

                    await LoadVendorsAsync();
                    MessageBox.Show($"Vendedor {action}do correctamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al actualizar estado: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Prevenir que el evento suba al Border padre
            e.Handled = true;
        }

        // Búsqueda
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        // Cerrar ventana
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ViewModel para mostrar en las cards
    public class VendorViewModel
    {
        public int Id { get; set; }
        public string VendorName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Username { get; set; }
        public bool IsActive { get; set; }
        public int? UserId { get; set; }
        public bool UserIsActive { get; set; }
        public decimal? CommissionRate { get; set; }

        // Propiedades calculadas para el binding
        public string StatusText => IsActive ? "ACTIVO" : "INACTIVO";

        public Brush StatusColor => IsActive
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Verde
            : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Rojo

        // Propiedad para el avatar
        public string AvatarInitial
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VendorName))
                    return "?";

                var parts = VendorName.Trim().Split(' ');
                if (parts.Length >= 2)
                {
                    // Tomar primera letra del nombre y apellido
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                }
                else
                {
                    // Tomar las primeras dos letras del nombre
                    return VendorName.Length >= 2
                        ? VendorName.Substring(0, 2).ToUpper()
                        : VendorName.ToUpper();
                }
            }
        }
    }
}