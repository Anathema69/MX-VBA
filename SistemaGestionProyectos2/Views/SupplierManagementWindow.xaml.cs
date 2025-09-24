using System;
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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class SupplierManagementWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<SupplierViewModel> _suppliers;
        private ObservableCollection<SupplierViewModel> _filteredSuppliers;
        private TextBox _currentEditingTextBox;
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");

        public SupplierManagementWindow()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _suppliers = new ObservableCollection<SupplierViewModel>();
            _filteredSuppliers = new ObservableCollection<SupplierViewModel>();

            SuppliersItemsControl.ItemsSource = _filteredSuppliers;
            _ = LoadSuppliers();
        }

        private async Task LoadSuppliers()
        {
            try
            {
                StatusText.Text = "Cargando proveedores...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                var suppliers = await _supabaseService.GetAllSuppliers();

                _suppliers.Clear();
                foreach (var supplier in suppliers.OrderBy(s => s.SupplierName))
                {
                    var vm = new SupplierViewModel
                    {
                        Id = supplier.Id,
                        SupplierName = supplier.SupplierName,
                        TaxId = supplier.TaxId ?? "",
                        Phone = supplier.Phone ?? "",
                        Email = supplier.Email ?? "",
                        Address = supplier.Address ?? "",
                        CreditDays = supplier.CreditDays,
                        IsActive = supplier.IsActive,
                        HasChanges = false,
                        OriginalData = new SupplierViewModel
                        {
                            SupplierName = supplier.SupplierName,
                            TaxId = supplier.TaxId,
                            Phone = supplier.Phone,
                            Email = supplier.Email,
                            Address = supplier.Address,
                            CreditDays = supplier.CreditDays,
                            IsActive = supplier.IsActive
                        }
                    };

                    // Suscribir a cambios
                    vm.PropertyChanged += OnSupplierPropertyChanged;
                    _suppliers.Add(vm);
                }

                ApplyFilter();
                UpdateStatistics();

                StatusText.Text = "Listo";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar proveedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void OnSupplierPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is SupplierViewModel supplier)
            {
                if (e.PropertyName != nameof(SupplierViewModel.HasChanges) &&
                    e.PropertyName != nameof(SupplierViewModel.IsEditing))
                {
                    supplier.CheckForChanges();
                }
            }
        }

        private void ApplyFilter()
        {
            _filteredSuppliers.Clear();

            var searchText = SearchBox?.Text?.ToLower() ?? "";
            var filtered = _suppliers.AsEnumerable();

            // Filtro por texto
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(s =>
                    s.SupplierName?.ToLower().Contains(searchText) == true ||
                    s.TaxId?.ToLower().Contains(searchText) == true ||
                    s.Email?.ToLower().Contains(searchText) == true ||
                    s.Phone?.Contains(searchText) == true);
            }

            // Filtro por estado
            if (StatusFilterCombo?.SelectedIndex > 0)
            {
                var selectedStatus = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (selectedStatus == "Activos")
                {
                    filtered = filtered.Where(s => s.IsActive);
                }
                else if (selectedStatus == "Inactivos")
                {
                    filtered = filtered.Where(s => !s.IsActive);
                }
            }

            foreach (var supplier in filtered.OrderBy(s => s.SupplierName))
            {
                _filteredSuppliers.Add(supplier);
            }

            UpdateStatistics();
            UpdateEmptyState();
        }

        private void UpdateStatistics()
        {
            var total = _filteredSuppliers.Count;
            var activos = _filteredSuppliers.Count(s => s.IsActive);
            CountText.Text = $"{total} proveedor{(total != 1 ? "es" : "")} ({activos} activo{(activos != 1 ? "s" : "")})";
        }

        private void UpdateEmptyState()
        {
            EmptyStatePanel.Visibility = _filteredSuppliers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Eventos para edición de campos de texto
        private void Field_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var supplier = textBox?.DataContext as SupplierViewModel;

            if (textBox != null && supplier != null)
            {
                if (textBox.IsReadOnly)
                {
                    textBox.IsReadOnly = false;
                    textBox.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195));
                    textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                    textBox.BorderThickness = new Thickness(1);

                    textBox.SelectAll();
                    supplier.IsEditing = true;
                }
            }
        }

        private void Field_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var supplier = textBox?.DataContext as SupplierViewModel;

            if (textBox != null && supplier != null)
            {
                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                textBox.BorderBrush = Brushes.Transparent;
                textBox.BorderThickness = new Thickness(0);
                textBox.Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39));

                // No marcar como no-editando aquí, solo cuando se guarda o cancela
            }
        }

        private async void Field_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            var supplier = textBox?.DataContext as SupplierViewModel;

            if (textBox == null || supplier == null) return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // Validar nombre obligatorio
                if (string.IsNullOrWhiteSpace(supplier.SupplierName))
                {
                    MessageBox.Show("El nombre del proveedor es obligatorio", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    var nameTextBox = FindNameTextBoxForSupplier(supplier);
                    nameTextBox?.Focus();
                    return;
                }

                await SaveSupplier(supplier);

                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                textBox.BorderBrush = Brushes.Transparent;
                textBox.BorderThickness = new Thickness(0);
                Keyboard.ClearFocus();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;

                if (supplier.IsNew)
                {
                    _suppliers.Remove(supplier);
                    _filteredSuppliers.Remove(supplier);
                    UpdateStatistics();
                    UpdateEmptyState();
                    ShowToast("❌", "Creación cancelada");
                }
                else
                {
                    // Revertir cambios del campo actual
                    if (supplier.OriginalData != null)
                    {
                        supplier.RevertChanges();
                    }
                }

                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                textBox.BorderBrush = Brushes.Transparent;
                textBox.BorderThickness = new Thickness(0);
                textBox.Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39));
                Keyboard.ClearFocus();
                supplier.IsEditing = false;
            }
            else if (e.Key == Key.Tab && supplier.IsNew)
            {
                // Navegar entre campos para nuevo proveedor
                e.Handled = true;
                NavigateToNextField(textBox, supplier, (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
            }
        }

        private void NavigateToNextField(TextBox currentField, SupplierViewModel supplier, bool goBack = false)
        {
            var container = SuppliersItemsControl.ItemContainerGenerator.ContainerFromItem(supplier);
            if (container == null) return;

            var allTextBoxes = FindVisualChildren<TextBox>(container)
                .Where(tb => !tb.Name.Contains("Credit")).ToList();
            var currentIndex = allTextBoxes.IndexOf(currentField);

            if (currentIndex >= 0)
            {
                int nextIndex;
                if (goBack)
                {
                    nextIndex = currentIndex - 1;
                    if (nextIndex < 0) nextIndex = allTextBoxes.Count - 1;
                }
                else
                {
                    nextIndex = currentIndex + 1;
                    if (nextIndex >= allTextBoxes.Count) nextIndex = 0;
                }

                var nextField = allTextBoxes[nextIndex];
                if (nextField != null)
                {
                    currentField.IsReadOnly = true;
                    currentField.Background = Brushes.Transparent;
                    currentField.BorderBrush = Brushes.Transparent;
                    currentField.BorderThickness = new Thickness(0);

                    nextField.IsReadOnly = false;
                    nextField.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195));
                    nextField.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                    nextField.BorderThickness = new Thickness(1);
                    nextField.Focus();
                    nextField.SelectAll();
                }
            }
        }

        // Eventos para días de crédito
        private void CreditDays_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && textBox.IsReadOnly)
            {
                if (_currentEditingTextBox != null && _currentEditingTextBox != textBox)
                {
                    _ = SaveCreditDays(_currentEditingTextBox);
                }

                textBox.IsReadOnly = false;
                textBox.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195));

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                    textBox.Focus();
                }), DispatcherPriority.Render);

                _currentEditingTextBox = textBox;
            }
        }

        private void CreditDays_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Key.Enter)
            {
                _ = SaveCreditDays(textBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                var vm = textBox.Tag as SupplierViewModel;
                if (vm != null)
                {
                    textBox.Text = vm.CreditDays.ToString();
                }
                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                _currentEditingTextBox = null;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void CreditDays_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsReadOnly)
            {
                _ = SaveCreditDays(textBox);
            }
        }

        private async Task SaveCreditDays(TextBox textBox)
        {
            if (textBox == null || textBox.Tag is not SupplierViewModel supplier) return;

            try
            {
                if (int.TryParse(textBox.Text, out int days))
                {
                    if (days < 0) days = 0;
                    if (days > 365) days = 365;

                    supplier.CreditDays = days;
                    await SaveSupplier(supplier);
                }

                textBox.Text = supplier.CreditDays.ToString();
            }
            finally
            {
                textBox.IsReadOnly = true;
                textBox.Background = Brushes.Transparent;
                _currentEditingTextBox = null;
            }
        }

        private void CreditDays_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        // Botones de acción
        private void NewSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            var newSupplier = new SupplierViewModel
            {
                Id = 0,
                SupplierName = "",
                TaxId = "",
                Phone = "",
                Email = "",
                Address = "",
                CreditDays = 30,
                IsActive = true,
                IsNew = true,
                IsEditing = true,
                HasChanges = true
            };

            newSupplier.PropertyChanged += OnSupplierPropertyChanged;
            _suppliers.Insert(0, newSupplier);
            _filteredSuppliers.Insert(0, newSupplier);

            UpdateStatistics();
            UpdateEmptyState();

            // Hacer scroll al inicio
            var scrollViewer = FindVisualChild<ScrollViewer>(SuppliersItemsControl);
            scrollViewer?.ScrollToTop();

            // Esperar a que se renderice y enfocar el primer campo
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = SuppliersItemsControl.ItemContainerGenerator.ContainerFromItem(newSupplier);
                if (container != null)
                {
                    var nameTextBox = FindVisualChild<TextBox>(container, "NameTextBox");
                    if (nameTextBox != null)
                    {
                        nameTextBox.IsReadOnly = false;
                        nameTextBox.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195));
                        nameTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                        nameTextBox.BorderThickness = new Thickness(2);
                        nameTextBox.Focus();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Render);

            ShowToast("📝", "Complete los datos del nuevo proveedor (ESC para cancelar, ENTER para guardar)");
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplier = button?.Tag as SupplierViewModel;

            if (supplier != null)
            {
                await SaveSupplier(supplier);
            }
        }

        private async Task SaveSupplier(SupplierViewModel supplier)
        {
            try
            {
                StatusText.Text = "Guardando...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                var supplierDb = new SupplierDb
                {
                    Id = supplier.Id,
                    SupplierName = supplier.SupplierName,
                    TaxId = string.IsNullOrWhiteSpace(supplier.TaxId) ? null : supplier.TaxId,
                    Phone = string.IsNullOrWhiteSpace(supplier.Phone) ? null : supplier.Phone,
                    Email = string.IsNullOrWhiteSpace(supplier.Email) ? null : supplier.Email,
                    Address = string.IsNullOrWhiteSpace(supplier.Address) ? null : supplier.Address,
                    CreditDays = supplier.CreditDays,
                    IsActive = supplier.IsActive
                };

                bool success;
                if (supplier.IsNew)
                {
                    var created = await _supabaseService.CreateSupplier(supplierDb);
                    if (created != null)
                    {
                        supplier.Id = created.Id;
                        // IMPORTANTE: Quitar el estado de nuevo después de guardar
                        supplier.IsNew = false;
                        supplier.IsEditing = false;
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }
                else
                {
                    success = await _supabaseService.UpdateSupplier(supplierDb);
                    supplier.IsEditing = false;
                }

                if (success)
                {
                    supplier.HasChanges = false;
                    supplier.UpdateOriginalData();
                    StatusText.Text = "Guardado";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    UpdateStatistics();
                    ShowToast("✅", supplier.Id == 0 ? "Proveedor creado exitosamente" : "Cambios guardados");
                }
                else
                {
                    throw new Exception("No se pudo guardar");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error al guardar";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplier = button?.Tag as SupplierViewModel;

            if (supplier != null)
            {
                supplier.IsEditing = true;
                ShowToast("ℹ️", "Haz clic en cualquier campo para editarlo");
            }
        }

        private async void ToggleActiveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplier = button?.Tag as SupplierViewModel;

            if (supplier != null && !supplier.IsNew)
            {
                supplier.IsActive = !supplier.IsActive;
                await SaveSupplier(supplier);
                UpdateStatistics();
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplier = button?.Tag as SupplierViewModel;

            if (supplier == null) return;

            if (supplier.IsNew)
            {
                _suppliers.Remove(supplier);
                _filteredSuppliers.Remove(supplier);
                UpdateStatistics();
                UpdateEmptyState();
                return;
            }

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el proveedor {supplier.SupplierName}?\n\n" +
                "Esta acción no se puede deshacer.",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var success = await _supabaseService.DeleteSupplier(supplier.Id);

                if (success)
                {
                    _suppliers.Remove(supplier);
                    _filteredSuppliers.Remove(supplier);
                    UpdateStatistics();
                    UpdateEmptyState();
                    ShowToast("✓", "Proveedor eliminado");
                }
                else
                {
                    ShowToast("❌", "Error al eliminar", true);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                ApplyFilter();
            };
            timer.Start();
        }

        private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppliers != null)
            {
                ApplyFilter();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Métodos auxiliares para buscar elementos visuales
        private T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName))
                        return typedChild;

                    if (child is FrameworkElement fe && fe.Name == childName)
                        return typedChild;
                }

                var result = FindVisualChild<T>(child, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T typedChild)
                    {
                        yield return typedChild;
                    }

                    foreach (var childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private TextBox FindNameTextBoxForSupplier(SupplierViewModel supplier)
        {
            var container = SuppliersItemsControl.ItemContainerGenerator.ContainerFromItem(supplier);
            if (container != null)
            {
                return FindVisualChild<TextBox>(container, "NameTextBox");
            }
            return null;
        }

        // Sistema de notificaciones Toast
        private async void ShowToast(string icon, string message, bool isError = false)
        {
            StatusText.Text = $"{icon} {message}";
            StatusText.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                : new SolidColorBrush(Color.FromRgb(16, 185, 129));

            await Task.Delay(3000);

            StatusText.Text = "Listo";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
    }

    // ViewModel actualizado
    public class SupplierViewModel : INotifyPropertyChanged
    {
        private string _supplierName;
        private string _taxId;
        private string _phone;
        private string _email;
        private string _address;
        private int _creditDays;
        private bool _isActive;
        private bool _hasChanges;
        private bool _isEditing;
        private bool _isNew;

        public int Id { get; set; }

        public bool IsNew
        {
            get => _isNew;
            set { _isNew = value; OnPropertyChanged(); }
        }

        public string SupplierName
        {
            get => _supplierName;
            set { _supplierName = value; OnPropertyChanged(); }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); }
        }

        public string TaxId
        {
            get => _taxId;
            set { _taxId = value; OnPropertyChanged(); }
        }

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }

        public int CreditDays
        {
            get => _creditDays;
            set { _creditDays = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsInactive)); }
        }

        public bool IsInactive => !IsActive;

        public bool HasChanges
        {
            get => _hasChanges;
            set { _hasChanges = value; OnPropertyChanged(); }
        }

        public SupplierViewModel OriginalData { get; set; }

        public void CheckForChanges()
        {
            if (OriginalData == null || IsNew) return;

            HasChanges = SupplierName != OriginalData.SupplierName ||
                        TaxId != OriginalData.TaxId ||
                        Phone != OriginalData.Phone ||
                        Email != OriginalData.Email ||
                        Address != OriginalData.Address ||
                        CreditDays != OriginalData.CreditDays ||
                        IsActive != OriginalData.IsActive;
        }

        public void UpdateOriginalData()
        {
            OriginalData = new SupplierViewModel
            {
                SupplierName = SupplierName,
                TaxId = TaxId,
                Phone = Phone,
                Email = Email,
                Address = Address,
                CreditDays = CreditDays,
                IsActive = IsActive
            };
        }

        public void RevertChanges()
        {
            if (OriginalData == null) return;

            SupplierName = OriginalData.SupplierName;
            TaxId = OriginalData.TaxId;
            Phone = OriginalData.Phone;
            Email = OriginalData.Email;
            Address = OriginalData.Address;
            CreditDays = OriginalData.CreditDays;
            IsActive = OriginalData.IsActive;
            HasChanges = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}