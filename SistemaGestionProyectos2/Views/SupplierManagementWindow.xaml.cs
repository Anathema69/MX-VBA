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
    public partial class SupplierManagementWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<SupplierViewModel> _suppliers;
        private ObservableCollection<SupplierViewModel> _filteredSuppliers;
        private SupplierViewModel _newSupplierRow = null;
        private bool _isCreatingNewSupplier = false;

        public SupplierManagementWindow()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _suppliers = new ObservableCollection<SupplierViewModel>();
            _filteredSuppliers = new ObservableCollection<SupplierViewModel>();

            SuppliersDataGrid.ItemsSource = _filteredSuppliers;
            InitializeEvents();

            _ = LoadSuppliers();
        }

        private void InitializeEvents()
        {
            SuppliersDataGrid.CellEditEnding += DataGrid_CellEditEnding;
            SuppliersDataGrid.PreviewKeyDown += DataGrid_PreviewKeyDown;
            SuppliersDataGrid.BeginningEdit += DataGrid_BeginningEdit;
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
                        TaxId = supplier.TaxId,
                        Phone = supplier.Phone,
                        Email = supplier.Email,
                        Address = supplier.Address,
                        CreditDays = supplier.CreditDays,
                        IsActive = supplier.IsActive,
                        IsNew = false
                    };
                    _suppliers.Add(vm);
                }

                ApplyFilter();

                StatusText.Text = "Listo";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 197));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar proveedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void ApplyFilter()
        {
            _filteredSuppliers.Clear();

            // Mantener la fila nueva si existe
            if (_newSupplierRow != null && _isCreatingNewSupplier)
            {
                _filteredSuppliers.Add(_newSupplierRow);
            }

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

            // Ordenar y agregar
            foreach (var supplier in filtered.OrderBy(s => s.SupplierName))
            {
                _filteredSuppliers.Add(supplier);
            }

            CountText.Text = $"{_filteredSuppliers.Count(s => !s.IsNew)} proveedores";
        }

        private void NewSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreatingNewSupplier)
            {
                MessageBox.Show("Complete o cancele el proveedor actual antes de crear uno nuevo.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);

                if (_newSupplierRow != null)
                {
                    SuppliersDataGrid.SelectedItem = _newSupplierRow;
                    SuppliersDataGrid.ScrollIntoView(_newSupplierRow);
                    SuppliersDataGrid.Focus();
                }
                return;
            }

            try
            {
                _isCreatingNewSupplier = true;
                NewSupplierButton.IsEnabled = false;

                // Crear nueva fila
                _newSupplierRow = new SupplierViewModel
                {
                    Id = 0,
                    SupplierName = "",
                    TaxId = "",
                    Phone = "",
                    Email = "",
                    Address = "",
                    CreditDays = 30, // Valor por defecto
                    IsActive = true,
                    IsNew = true
                };

                // Insertar al inicio
                _filteredSuppliers.Insert(0, _newSupplierRow);

                // Seleccionar y enfocar
                SuppliersDataGrid.SelectedItem = _newSupplierRow;
                SuppliersDataGrid.ScrollIntoView(_newSupplierRow);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SuppliersDataGrid.CurrentCell = new DataGridCellInfo(
                        _newSupplierRow,
                        SuppliersDataGrid.Columns[0]); // Columna Nombre
                    SuppliersDataGrid.BeginEdit();
                }), System.Windows.Threading.DispatcherPriority.Background);

                StatusText.Text = "Creando nuevo proveedor...";
                StatusText.Foreground = new SolidColorBrush(Colors.Blue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear nuevo proveedor: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                _isCreatingNewSupplier = false;
                NewSupplierButton.IsEnabled = true;
            }
        }

        private async void SaveNewButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplier = button?.Tag as SupplierViewModel;

            if (supplier != null && supplier.IsNew)
            {
                SuppliersDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                SuppliersDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                await SaveNewSupplier();
            }
        }

        private async Task<bool> SaveNewSupplier()
        {
            if (_newSupplierRow == null) return false;

            // Validar
            if (string.IsNullOrWhiteSpace(_newSupplierRow.SupplierName))
            {
                MessageBox.Show("El nombre del proveedor es obligatorio", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                StatusText.Text = "Guardando proveedor...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                var newSupplier = new SupplierDb
                {
                    SupplierName = _newSupplierRow.SupplierName.Trim(),
                    TaxId = _newSupplierRow.TaxId?.Trim(),
                    Phone = _newSupplierRow.Phone?.Trim(),
                    Email = _newSupplierRow.Email?.Trim(),
                    Address = _newSupplierRow.Address?.Trim(),
                    CreditDays = _newSupplierRow.CreditDays,
                    IsActive = _newSupplierRow.IsActive
                };

                var created = await _supabaseService.CreateSupplier(newSupplier);

                if (created != null)
                {
                    _newSupplierRow.Id = created.Id;
                    _newSupplierRow.IsNew = false;

                    _suppliers.Add(_newSupplierRow);

                    _newSupplierRow = null;
                    _isCreatingNewSupplier = false;
                    NewSupplierButton.IsEnabled = true;

                    StatusText.Text = "Proveedor guardado exitosamente";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                    return true;
                }
                else
                {
                    throw new Exception("No se pudo crear el proveedor");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                StatusText.Text = "Error al guardar";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);

                return false;
            }
        }

        private void CancelNewSupplier()
        {
            if (_newSupplierRow != null && _isCreatingNewSupplier)
            {
                _filteredSuppliers.Remove(_newSupplierRow);
                _newSupplierRow = null;
                _isCreatingNewSupplier = false;
                NewSupplierButton.IsEnabled = true;

                StatusText.Text = "Creación cancelada";
                StatusText.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplier = button?.Tag as SupplierViewModel;

            if (supplier == null) return;

            // Si es nueva, solo cancelar
            if (supplier.IsNew)
            {
                CancelNewSupplier();
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

                    MessageBox.Show("Proveedor eliminado exitosamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Error al eliminar. Puede tener gastos asociados.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel)
                return;

            var supplier = e.Row.Item as SupplierViewModel;
            if (supplier != null && !supplier.IsNew)
            {
                await SaveSupplierChanges(supplier);
            }
        }

        private async Task SaveSupplierChanges(SupplierViewModel supplier)
        {
            try
            {
                StatusText.Text = "Guardando cambios...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);

                var supplierDb = new SupplierDb
                {
                    Id = supplier.Id,
                    SupplierName = supplier.SupplierName,
                    TaxId = supplier.TaxId,
                    Phone = supplier.Phone,
                    Email = supplier.Email,
                    Address = supplier.Address,
                    CreditDays = supplier.CreditDays,
                    IsActive = supplier.IsActive
                };

                var success = await _supabaseService.UpdateSupplier(supplierDb);

                if (success)
                {
                    StatusText.Text = "Cambios guardados";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                }
                else
                {
                    StatusText.Text = "Error al guardar";
                    StatusText.Foreground = new SolidColorBrush(Colors.Red);
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

        private async void ActiveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var supplier = checkBox?.DataContext as SupplierViewModel;

            if (supplier != null && !supplier.IsNew)
            {
                await SaveSupplierChanges(supplier);
            }
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var selectedSupplier = grid.SelectedItem as SupplierViewModel;

            if (e.Key == Key.Enter)
            {
                if (_isCreatingNewSupplier && _newSupplierRow != null && selectedSupplier == _newSupplierRow)
                {
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);

                    _ = SaveNewSupplier();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (_isCreatingNewSupplier && _newSupplierRow != null && selectedSupplier == _newSupplierRow)
                {
                    grid.CancelEdit(DataGridEditingUnit.Cell);
                    grid.CancelEdit(DataGridEditingUnit.Row);
                    CancelNewSupplier();
                    e.Handled = true;
                }
            }
        }

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // Permitir edición para todas las columnas excepto acciones
            var column = e.Column;
            if (column.Header?.ToString() == "ACCIONES")
            {
                e.Cancel = true;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isCreatingNewSupplier)
            {
                ApplyFilter();
            }
        }

        private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppliers != null && !_isCreatingNewSupplier)
            {
                ApplyFilter();
            }
        }

        private void CreditDays_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreatingNewSupplier)
            {
                var result = MessageBox.Show(
                    "Hay un proveedor sin guardar. ¿Desea salir sin guardar?",
                    "Cambios pendientes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            Close();
        }
    }

    // ViewModel para el proveedor
    public class SupplierViewModel
    {
        public int Id { get; set; }
        public string SupplierName { get; set; }
        public string TaxId { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public int CreditDays { get; set; }
        public bool IsActive { get; set; }
        public bool IsNew { get; set; }
    }
}