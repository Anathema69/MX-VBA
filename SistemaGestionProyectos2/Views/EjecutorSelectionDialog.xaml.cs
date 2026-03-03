using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class EjecutorSelectionDialog : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly int _orderId;
        private readonly int _userId;
        private ObservableCollection<EjecutorItem> _allEmployees;
        private ObservableCollection<EjecutorItem> _filteredEmployees;
        private ObservableCollection<EjecutorItem> _selectedChips;

        public List<int> SelectedPayrollIds { get; private set; } = new();
        public string SelectedNames { get; private set; } = "";

        // Paleta de colores para avatares
        private static readonly (string bg, string fg)[] AvatarColors = new[]
        {
            ("#6366F1", "#FFFFFF"), // Indigo
            ("#EC4899", "#FFFFFF"), // Pink
            ("#14B8A6", "#FFFFFF"), // Teal
            ("#F97316", "#FFFFFF"), // Orange
            ("#8B5CF6", "#FFFFFF"), // Violet
            ("#06B6D4", "#FFFFFF"), // Cyan
            ("#EF4444", "#FFFFFF"), // Red
            ("#10B981", "#FFFFFF"), // Emerald
            ("#F59E0B", "#FFFFFF"), // Amber
            ("#3B82F6", "#FFFFFF"), // Blue
        };

        // Paleta para chips
        private static readonly (string bg, string fg)[] ChipColors = new[]
        {
            ("#DBEAFE", "#1E40AF"),
            ("#D1FAE5", "#065F46"),
            ("#FEF3C7", "#92400E"),
            ("#E0E7FF", "#3730A3"),
            ("#FCE7F3", "#9D174D"),
            ("#CFFAFE", "#155E75"),
            ("#FDE68A", "#78350F"),
            ("#E9D5FF", "#6B21A8"),
            ("#FFE4E6", "#9F1239"),
            ("#F3F4F6", "#374151"),
        };

        public EjecutorSelectionDialog(int orderId, string orderNumber, List<int> currentEjecutorIds, int userId)
        {
            InitializeComponent();

            _supabaseService = SupabaseService.Instance;
            _orderId = orderId;
            _userId = userId;
            _allEmployees = new ObservableCollection<EjecutorItem>();
            _filteredEmployees = new ObservableCollection<EjecutorItem>();
            _selectedChips = new ObservableCollection<EjecutorItem>();

            OrderInfoText.Text = $"Orden: {orderNumber}";
            EmployeeListBox.ItemsSource = _filteredEmployees;
            SelectedChipsPanel.ItemsSource = _selectedChips;

            LoadEmployees(currentEjecutorIds);
        }

        private async void LoadEmployees(List<int> currentEjecutorIds)
        {
            try
            {
                var payroll = await _supabaseService.GetActivePayroll();

                foreach (var emp in payroll.OrderBy(p => p.Employee))
                {
                    var colorIndex = Math.Abs((emp.Employee ?? "").GetHashCode()) % AvatarColors.Length;
                    var (bg, _) = AvatarColors[colorIndex];

                    var chipColorIndex = Math.Abs((emp.Employee ?? "").GetHashCode()) % ChipColors.Length;
                    var (chipBg, chipFg) = ChipColors[chipColorIndex];

                    var parts = (emp.Employee ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    var item = new EjecutorItem
                    {
                        PayrollId = emp.Id,
                        EmployeeName = emp.Employee ?? "",
                        Title = emp.Title ?? "",
                        IsSelected = currentEjecutorIds?.Contains(emp.Id) == true,
                        Initials = parts.Length >= 2
                            ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                            : (emp.Employee ?? "??").Substring(0, Math.Min(2, (emp.Employee ?? "").Length)).ToUpper(),
                        ShortName = parts.Length >= 2 ? $"{parts[0]} {parts[1][0]}." : emp.Employee ?? "",
                        AvatarBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
                        ChipBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chipBg)),
                        ChipForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(chipFg)),
                    };
                    item.AvatarBackground.Freeze();
                    item.ChipBackground.Freeze();
                    item.ChipForeground.Freeze();
                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(EjecutorItem.IsSelected))
                            RefreshSelectedChips();
                    };
                    _allEmployees.Add(item);
                }

                ApplyFilter("");
                RefreshSelectedChips();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando empleados:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshSelectedChips()
        {
            _selectedChips.Clear();
            foreach (var emp in _allEmployees.Where(e => e.IsSelected).OrderBy(e => e.EmployeeName))
            {
                _selectedChips.Add(emp);
            }
            var count = _selectedChips.Count;
            SelectionCountText.Text = $"{count} seleccionado{(count != 1 ? "s" : "")}";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchBox.Text?.Trim() ?? "");
        }

        private void ApplyFilter(string search)
        {
            _filteredEmployees.Clear();

            var filtered = string.IsNullOrEmpty(search)
                ? _allEmployees
                : _allEmployees.Where(e =>
                    e.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    e.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

            foreach (var emp in filtered)
            {
                _filteredEmployees.Add(emp);
            }
        }

        private void EmployeeItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is EjecutorItem item)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        private void RemoveChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EjecutorItem item)
            {
                item.IsSelected = false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPayrollIds = _allEmployees.Where(e => e.IsSelected).Select(e => e.PayrollId).ToList();
            SelectedNames = string.Join(", ", _allEmployees.Where(e => e.IsSelected)
                .OrderBy(e => e.EmployeeName).Select(e => e.EmployeeName));
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class EjecutorItem : INotifyPropertyChanged
    {
        public int PayrollId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Initials { get; set; } = "";
        public string ShortName { get; set; } = "";
        public SolidColorBrush AvatarBackground { get; set; }
        public SolidColorBrush ChipBackground { get; set; }
        public SolidColorBrush ChipForeground { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
