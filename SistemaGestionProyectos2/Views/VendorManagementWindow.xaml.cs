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
            VendorsDataGrid.ItemsSource = _filteredVendors;
            EditVendorButton.IsEnabled = false;
            DeleteVendorButton.IsEnabled = false;
        }

        private async Task LoadVendorsAsync()
        {
            try
            {
                StatusText.Text = "Cargando vendedores...";
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
                        Phone = vendor.Phone,
                        IsActive = vendor.IsActive,
                        UserId = vendor.UserId,
                        Username = user?.Username ?? "Sin usuario",
                        UserIsActive = user?.IsActive ?? false
                    };

                    _vendors.Add(vendorVm);
                }

                ApplyFilter();
                UpdateStatusBar();
                StatusText.Text = "Vendedores cargados";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar vendedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
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
                    v.Username.ToLower().Contains(searchText));
            }

            foreach (var vendor in filtered)
            {
                _filteredVendors.Add(vendor);
            }

            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            var activeCount = _filteredVendors.Count(v => v.IsActive);
            var totalCount = _filteredVendors.Count;
            VendorCountText.Text = $"Total: {totalCount} vendedores ({activeCount} activos)";
        }

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
            var selected = VendorsDataGrid.SelectedItem as VendorViewModel;
            if (selected == null) return;

            var dialog = new VendorEditDialog(selected);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadVendorsAsync();
            }
        }

        private async void DeleteVendor_Click(object sender, RoutedEventArgs e)
        {
            var selected = VendorsDataGrid.SelectedItem as VendorViewModel;
            if (selected == null) return;

            var action = selected.IsActive ? "desactivar" : "activar";
            var result = MessageBox.Show(
                $"¿Está seguro de {action} al vendedor {selected.VendorName}?\n\n" +
                $"Esto también {action}á su acceso al sistema.",
                $"Confirmar {action}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatusText.Text = $"{(selected.IsActive ? "Desactivando" : "Activando")} vendedor...";
                var supabaseClient = _supabaseService.GetClient();

                // Actualizar estado del vendedor
                var vendorToUpdate = await supabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == selected.Id)
                    .Single();

                if (vendorToUpdate != null)
                {
                    vendorToUpdate.IsActive = !selected.IsActive;
                    await supabaseClient
                        .From<VendorTableDb>()
                        .Update(vendorToUpdate);
                }

                // Actualizar estado del usuario si existe
                if (selected.UserId.HasValue)
                {
                    var userToUpdate = await supabaseClient
                        .From<UserDb>()
                        .Where(u => u.Id == selected.UserId.Value)
                        .Single();

                    if (userToUpdate != null)
                    {
                        userToUpdate.IsActive = !selected.IsActive;
                        await supabaseClient
                            .From<UserDb>()
                            .Update(userToUpdate);
                    }
                }

                await LoadVendorsAsync();
                MessageBox.Show(
                    $"Vendedor {(selected.IsActive ? "desactivado" : "activado")} correctamente.",
                    "Éxito",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar estado: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VendorsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = VendorsDataGrid.SelectedItem != null;
            EditVendorButton.IsEnabled = hasSelection;
            DeleteVendorButton.IsEnabled = hasSelection;

            if (hasSelection)
            {
                var selected = VendorsDataGrid.SelectedItem as VendorViewModel;
                DeleteVendorButton.Content = selected.IsActive ? "🗑️ DESACTIVAR" : "✅ ACTIVAR";
            }
        }

        private void VendorsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditVendor_Click(null, null);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ViewModel para mostrar en el grid
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

        public string StatusText => IsActive ? "ACTIVO" : "INACTIVO";
        public Brush StatusColor => IsActive
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Verde
            : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Rojo
    }
}