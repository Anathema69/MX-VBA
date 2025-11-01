using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorManagementWindow : Window
    {
        private readonly UserSession _currentUser;
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<VendorViewModel> _vendors;
        private ObservableCollection<VendorViewModel> _filteredVendors;
        private TextBox _currentEditingTextBox;
        private VendorViewModel _selectedVendor;
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");

        public VendorManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _supabaseService = SupabaseService.Instance;
            _vendors = new ObservableCollection<VendorViewModel>();
            _filteredVendors = new ObservableCollection<VendorViewModel>();

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            InitializeUI();
            _ = LoadActiveVendorsAsync();
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
            Title = $"Gestión de Vendedores - {_currentUser.FullName}";
            VendorsItemsControl.ItemsSource = _filteredVendors;
        }

        private async Task LoadActiveVendorsAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Cargar SOLO vendedores activos
                var vendorsResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Select("*")
                    .Where(v => v.IsActive == true)
                    .Order("f_vendorname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vendors = vendorsResponse?.Models ?? new System.Collections.Generic.List<VendorTableDb>();

                // Cargar usuarios asociados activos
                var usersResponse = await supabaseClient
                    .From<UserDb>()
                    .Where(u => u.Role == "salesperson")
                    .Where(u => u.IsActive == true)
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
                        CommissionRate = vendor.CommissionRate ?? 10m
                    };

                    _vendors.Add(vendorVm);
                }

                ApplyFilter();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                ShowToast("❌", $"Error al cargar vendedores", true);
            }
        }

        private void UpdateStatistics()
        {
            // Actualizar contador
            VendorCountBadge.Text = $"{_filteredVendors.Count} activos";

            // Calcular comisión promedio
            if (_filteredVendors.Any())
            {
                var avgCommission = _filteredVendors.Average(v => v.CommissionRate);
                CommissionAvgBadge.Text = $"Comisión promedio: {avgCommission:F1}%";
            }
            else
            {
                CommissionAvgBadge.Text = "Comisión promedio: 0%";
            }

            // Actualizar último update
            LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm}";

            // Mostrar/ocultar panel vacío
            EmptyStatePanel.Visibility = _filteredVendors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

            UpdateStatistics();
        }

        // Evento para edición de comisión con mejor UX
        private void CommissionTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && textBox.IsReadOnly)
            {
                // Si hay otro TextBox en edición, guardarlo primero
                if (_currentEditingTextBox != null && _currentEditingTextBox != textBox)
                {
                    _ = SaveCommissionRate(_currentEditingTextBox);
                }

                // Habilitar edición
                textBox.IsReadOnly = false;
                textBox.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195)); // Amarillo claro

                // Quitar el formato para edición
                if (textBox.Tag is VendorViewModel vm)
                {
                    textBox.Text = vm.CommissionRate.ToString("F1");
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                    textBox.Focus();
                }), DispatcherPriority.Render);

                _currentEditingTextBox = textBox;
            }
        }

        private void CommissionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Key.Enter)
            {
                _ = SaveCommissionRate(textBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Cancelar edición
                if (textBox.Tag is VendorViewModel vm)
                {
                    textBox.Text = vm.CommissionRate.ToString("F1");
                }
                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                _currentEditingTextBox = null;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Guardar y pasar al siguiente
                _ = SaveCommissionRate(textBox);
            }
        }

        private void CommissionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsReadOnly)
            {
                _ = SaveCommissionRate(textBox);
            }
        }

        // Validación de entrada para comisión
        private void CommissionTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Permitir solo números y punto decimal
            if (e.Text == ".")
            {
                e.Handled = textBox.Text.Contains(".");
            }
            else
            {
                e.Handled = !char.IsDigit(e.Text[0]);
            }

            // Verificar límite de decimales
            if (!e.Handled && textBox.Text.Contains("."))
            {
                var parts = textBox.Text.Split('.');
                if (parts.Length > 1 && parts[1].Length >= 1)
                {
                    var caretIndex = textBox.CaretIndex;
                    var dotIndex = textBox.Text.IndexOf('.');
                    if (caretIndex > dotIndex)
                    {
                        e.Handled = true;
                    }
                }
            }
        }

        private async Task SaveCommissionRate(TextBox textBox)
        {
            if (textBox == null || textBox.Tag is not VendorViewModel vendor) return;

            try
            {
                // Limpiar el texto de entrada
                var cleanText = textBox.Text.Replace("%", "").Trim();

                if (decimal.TryParse(cleanText, out decimal newRate))
                {
                    // Validar rango
                    if (newRate < 0) newRate = 0;
                    if (newRate > 100) newRate = 100;

                    // Solo actualizar si cambió
                    if (Math.Abs(newRate - vendor.CommissionRate) > 0.01m)
                    {
                        var supabaseClient = _supabaseService.GetClient();

                        var vendorToUpdate = await supabaseClient
                            .From<VendorTableDb>()
                            .Where(v => v.Id == vendor.Id)
                            .Single();

                        if (vendorToUpdate != null)
                        {
                            vendorToUpdate.CommissionRate = newRate;
                            await supabaseClient
                                .From<VendorTableDb>()
                                .Update(vendorToUpdate);

                            var oldRate = vendor.CommissionRate;
                            vendor.CommissionRate = newRate;

                            // Actualizar estadísticas
                            UpdateStatistics();

                            ShowToast("✓", $"Comisión actualizada: {oldRate:F1}% → {newRate:F1}%");
                        }
                    }
                }

                // Restaurar formato
                textBox.Text = vendor.CommissionRate.ToString("F1");
            }
            catch (Exception ex)
            {
                ShowToast("❌", "Error al actualizar", true);
                // Restaurar valor original
                textBox.Text = vendor.CommissionRate.ToString("F1");
            }
            finally
            {
                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                _currentEditingTextBox = null;
            }
        }

        // Eventos de botones
        private void AddVendor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VendorEditDialog(null);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadActiveVendorsAsync();
                ShowToast("✓", "Vendedor agregado exitosamente");
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is VendorViewModel vendor)
            {
                var dialog = new VendorEditDialog(vendor);
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    _ = LoadActiveVendorsAsync();
                    ShowToast("✓", "Vendedor actualizado");
                }
            }
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is VendorViewModel vendor)
            {
                // Aquí podrías abrir una ventana de detalles o mostrar un panel lateral
                ShowToast("ℹ️", $"Detalles de {vendor.VendorName}");
            }
        }

        private async void DeactivateButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is not VendorViewModel vendor) return;

            var result = MessageBox.Show(
                $"¿Está seguro de desactivar a {vendor.VendorName}?\n\n" +
                "El vendedor no aparecerá en la lista activa pero se conservará su historial.",
                "Confirmar desactivación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
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
                        vendorToUpdate.IsActive = false;
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
                            userToUpdate.IsActive = false;
                            await supabaseClient
                                .From<UserDb>()
                                .Update(userToUpdate);
                        }
                    }

                    // Remover de las colecciones con animación suave
                    _vendors.Remove(vendor);
                    _filteredVendors.Remove(vendor);

                    UpdateStatistics();
                    ShowToast("✓", $"{vendor.VendorName} desactivado correctamente");
                }
                catch (Exception ex)
                {
                    ShowToast("❌", "Error al desactivar", true);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Aplicar filtro con un pequeño delay para mejor rendimiento
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                ApplyFilter();
            };
            timer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Sistema de notificaciones Toast mejorado
        private async void ShowToast(string icon, string message, bool isError = false)
        {
            ToastIcon.Text = icon;
            ToastMessage.Text = message;

            if (isError)
            {
                ToastNotification.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else
            {
                ToastNotification.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }

            // Mostrar con animación
            ToastNotification.Visibility = Visibility.Visible;
            ToastNotification.Opacity = 0;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Ocultar después de 3 segundos
            await Task.Delay(3000);

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            fadeOut.Completed += (s, e) =>
            {
                ToastNotification.Visibility = Visibility.Collapsed;
            };

            ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }

    // ViewModel para mostrar vendedores
    public class VendorViewModel
    {
        public int Id { get; set; }
        public string VendorName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Username { get; set; }
        public bool IsActive { get; set; }
        public int? UserId { get; set; }
        public decimal CommissionRate { get; set; }

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
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                }
                else
                {
                    return VendorName.Length >= 2
                        ? VendorName.Substring(0, 2).ToUpper()
                        : VendorName.ToUpper();
                }
            }
        }
    }
}