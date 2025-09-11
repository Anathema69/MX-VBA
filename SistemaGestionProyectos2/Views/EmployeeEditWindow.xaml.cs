using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;

namespace SistemaGestionProyectos2.Views
{
    public partial class EmployeeEditWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private PayrollTable _payroll;
        private bool _isNewEmployee;
        private bool _isInitialized = false; // Bandera para controlar la inicialización

        public EmployeeEditWindow(PayrollViewModel employee, UserSession currentUser)
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _currentUser = currentUser;

            // Inicializar los controles después de InitializeComponent
            InitializeControls();

            if (employee == null)
            {
                _isNewEmployee = true;
                HeaderTitle.Text = "NUEVO EMPLEADO";
                _payroll = new PayrollTable();
                EffectiveDatePicker.SelectedDate = DateTime.Today;
                SetDefaultValues();
            }
            else
            {
                _isNewEmployee = false;
                HeaderTitle.Text = "EDITAR EMPLEADO";
                LoadEmployeeData(employee.Id);
            }

            _isInitialized = true; // Marcar como inicializado
        }

        private void InitializeControls()
        {
            // Asegurarse de que los TextBox tengan valores por defecto
            if (SSPayrollBox != null && string.IsNullOrEmpty(SSPayrollBox.Text))
                SSPayrollBox.Text = "0.00";

            if (WeeklyPayrollBox != null && string.IsNullOrEmpty(WeeklyPayrollBox.Text))
                WeeklyPayrollBox.Text = "0.00";

            if (SocialSecurityBox != null && string.IsNullOrEmpty(SocialSecurityBox.Text))
                SocialSecurityBox.Text = "0.00";

            if (BenefitsAmountBox != null && string.IsNullOrEmpty(BenefitsAmountBox.Text))
                BenefitsAmountBox.Text = "0.00";
        }

        private void SetDefaultValues()
        {
            // Establecer valores por defecto para nuevo empleado
            SSPayrollBox.Text = "0.00";
            WeeklyPayrollBox.Text = "0.00";
            SocialSecurityBox.Text = "0.00";
            BenefitsAmountBox.Text = "0.00";

            // Seleccionar valores por defecto en los ComboBox
            if (RangeCombo.Items.Count > 0)
                RangeCombo.SelectedIndex = 0;

            if (ConditionCombo.Items.Count > 0)
                ConditionCombo.SelectedIndex = 0;

            CalculateMonthlyTotal();
        }

        private async void LoadEmployeeData(int employeeId)
        {
            try
            {
                StatusText.Text = "Cargando datos...";

                _payroll = await _supabaseService.GetPayrollById(employeeId);

                if (_payroll != null)
                {
                    // Cargar datos en los controles
                    EmployeeNameBox.Text = _payroll.Employee ?? "";
                    TitleBox.Text = _payroll.Title ?? "";
                    EmployeeCodeBox.Text = _payroll.EmployeeCode ?? "";
                    HiredDatePicker.SelectedDate = _payroll.HiredDate;
                    LastRaisePicker.SelectedDate = _payroll.LastRaise;

                    // Seleccionar rango
                    SelectComboBoxItem(RangeCombo, _payroll.Range);

                    // Seleccionar condición
                    SelectComboBoxItem(ConditionCombo, _payroll.Condition);

                    // Cargar montos
                    SSPayrollBox.Text = (_payroll.SSPayroll ?? 0).ToString("F2");
                    WeeklyPayrollBox.Text = (_payroll.WeeklyPayroll ?? 0).ToString("F2");
                    SocialSecurityBox.Text = (_payroll.SocialSecurity ?? 0).ToString("F2");
                    BenefitsAmountBox.Text = (_payroll.BenefitsAmount ?? 0).ToString("F2");
                    BenefitsBox.Text = _payroll.Benefits ?? "";

                    CalculateMonthlyTotal();
                    StatusText.Text = "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar empleado: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar";
            }
        }

        private void SelectComboBoxItem(ComboBox combo, string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content?.ToString() == value)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private void PayrollBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Solo calcular si ya está inicializado
            if (_isInitialized)
            {
                CalculateMonthlyTotal();
            }
        }

        private void CalculateMonthlyTotal()
        {
            try
            {
                // Verificar que los controles no sean null
                if (SSPayrollBox == null || WeeklyPayrollBox == null ||
                    SocialSecurityBox == null || BenefitsAmountBox == null)
                {
                    return;
                }

                decimal ssPayroll = ParseDecimal(SSPayrollBox.Text);
                decimal weeklyPayroll = ParseDecimal(WeeklyPayrollBox.Text);
                decimal socialSecurity = ParseDecimal(SocialSecurityBox.Text);
                decimal benefitsAmount = ParseDecimal(BenefitsAmountBox.Text);

                // Fórmula: (Semanal * 52 / 12) + SS + Seguro Social + Prestaciones
                decimal monthlyFromWeekly = (weeklyPayroll * 52) / 12;
                decimal total = monthlyFromWeekly + ssPayroll + socialSecurity + benefitsAmount;

                var culture = new CultureInfo("es-MX");
                if (MonthlyTotalText != null)
                {
                    MonthlyTotalText.Text = total.ToString("C", culture);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculando total: {ex.Message}");
                if (MonthlyTotalText != null)
                {
                    MonthlyTotalText.Text = "$0.00";
                }
            }
        }

        private decimal ParseDecimal(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            // Limpiar el texto de símbolos de moneda y comas
            text = text.Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;

            return 0;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(EmployeeNameBox.Text))
            {
                MessageBox.Show("El nombre del empleado es obligatorio",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("El puesto es obligatorio",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RangeCombo.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar un rango",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ConditionCombo.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar una condición",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SaveButton.IsEnabled = false;
                StatusText.Text = "Guardando...";

                // Preparar el objeto
                if (_payroll == null)
                    _payroll = new PayrollTable();

                _payroll.Employee = EmployeeNameBox.Text.Trim();
                _payroll.Title = TitleBox.Text.Trim();
                _payroll.EmployeeCode = string.IsNullOrWhiteSpace(EmployeeCodeBox.Text) ? null : EmployeeCodeBox.Text.Trim();
                _payroll.HiredDate = HiredDatePicker.SelectedDate;
                _payroll.LastRaise = LastRaisePicker.SelectedDate;
                _payroll.Range = (RangeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                _payroll.Condition = (ConditionCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
                _payroll.SSPayroll = ParseDecimal(SSPayrollBox.Text);
                _payroll.WeeklyPayroll = ParseDecimal(WeeklyPayrollBox.Text);
                _payroll.SocialSecurity = ParseDecimal(SocialSecurityBox.Text);
                _payroll.BenefitsAmount = ParseDecimal(BenefitsAmountBox.Text);
                _payroll.Benefits = string.IsNullOrWhiteSpace(BenefitsBox.Text) ? null : BenefitsBox.Text.Trim();

                // Calcular nómina mensual
                decimal monthlyFromWeekly = (_payroll.WeeklyPayroll ?? 0) * 52 / 12;
                _payroll.MonthlyPayroll = monthlyFromWeekly +
                    (_payroll.SSPayroll ?? 0) +
                    (_payroll.SocialSecurity ?? 0) +
                    (_payroll.BenefitsAmount ?? 0);

                if (_isNewEmployee)
                {
                    _payroll.CreatedBy = _currentUser.Id;
                    await _supabaseService.CreatePayroll(_payroll);
                    MessageBox.Show("Empleado creado exitosamente",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _payroll.UpdatedBy = _currentUser.Id;
                    await _supabaseService.UpdatePayroll(_payroll);
                    MessageBox.Show("Empleado actualizado exitosamente",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al guardar";
                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}