using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;
using System;
using System.Globalization;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Controls;

namespace SistemaGestionProyectos2.Views
{
    public partial class EmployeeEditWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private PayrollTable _payroll;
        private bool _isNewEmployee;
        private bool _isInitialized = false; // Bandera para controlar la inicialización
        private bool _isEdit = false;
        private decimal _originalSalary = 0;

        public EmployeeEditWindow(PayrollViewModel employee, UserSession currentUser)
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _currentUser = currentUser;

            InitializeControls();

            if (employee == null)
            {
                _isNewEmployee = true;
                _isEdit = false;
                HeaderTitle.Text = "NUEVO EMPLEADO";
                _payroll = new PayrollTable();
                SetDefaultValues();

                // Ocultar sección de fecha efectiva para nuevo empleado
                EffectiveDateBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                _isNewEmployee = false;
                _isEdit = true;
                HeaderTitle.Text = "EDITAR EMPLEADO";
                LoadEmployeeData(employee.Id);
            }

            _isInitialized = true;
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
                    // Guardar salario original
                    _originalSalary = _payroll.MonthlyPayroll ?? 0;

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

                    // Guardar el salario original
                    _originalSalary = _payroll.MonthlyPayroll ?? 0;

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
                if (SSPayrollBox == null || WeeklyPayrollBox == null ||
                    SocialSecurityBox == null || BenefitsAmountBox == null)
                {
                    return;
                }

                decimal ssPayroll = ParseDecimal(SSPayrollBox.Text);
                decimal weeklyPayroll = ParseDecimal(WeeklyPayrollBox.Text);
                decimal socialSecurity = ParseDecimal(SocialSecurityBox.Text);
                decimal benefitsAmount = ParseDecimal(BenefitsAmountBox.Text);

                decimal monthlyFromWeekly = (weeklyPayroll * 52) / 12;
                decimal total = monthlyFromWeekly + ssPayroll + socialSecurity + benefitsAmount;

                var culture = new CultureInfo("es-MX");
                if (MonthlyTotalText != null)
                {
                    MonthlyTotalText.Text = total.ToString("C", culture);
                }

                // Mostrar/ocultar sección de fecha efectiva si el salario cambió
                if (_isEdit && _originalSalary > 0 && Math.Abs(total - _originalSalary) > 0.01m)
                {
                    EffectiveDateBorder.Visibility = Visibility.Visible;

                    var currentMonth = DateTime.Now;
                    var nextMonth = currentMonth.AddMonths(1);

                    CurrentMonthText.Text = $"Este mes ({currentMonth.ToString("MMMM yyyy", culture)})";
                    NextMonthText.Text = $"Próximo mes ({nextMonth.ToString("MMMM yyyy", culture)})";

                    var difference = total - _originalSalary;
                    var percentage = _originalSalary > 0 ? (difference / _originalSalary * 100) : 0;

                    var impact = difference > 0 ?
                        $"↑ Aumento de {difference:C} ({percentage:F1}%)" :
                        $"↓ Reducción de {Math.Abs(difference):C} ({Math.Abs(percentage):F1}%)";

                    ImpactText.Text = $"Impacto mensual: {impact}";
                }
                else if (!_isNewEmployee)
                {
                    EffectiveDateBorder.Visibility = Visibility.Collapsed;
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
            try
            {
                SaveButton.IsEnabled = false;
                StatusText.Text = "Guardando...";

                // Determinar fecha efectiva
                DateTime effectiveDate = DateTime.Now;

                if (EffectiveDateBorder.Visibility == Visibility.Visible)
                {
                    if (NextMonthRadio.IsChecked == true)
                    {
                        effectiveDate = new DateTime(
                            DateTime.Now.AddMonths(1).Year,
                            DateTime.Now.AddMonths(1).Month,
                            1); // Primer día del próximo mes
                    }
                    else
                    {
                        effectiveDate = new DateTime(
                            DateTime.Now.Year,
                            DateTime.Now.Month,
                            1); // Primer día del mes actual
                    }
                }

                // Preparar el objeto
                if (_payroll == null)
                    _payroll = new PayrollTable();

                // Asegurar que los campos obligatorios no sean nulos
                _payroll.Employee = EmployeeNameBox.Text?.Trim() ?? "";
                _payroll.Title = TitleBox.Text?.Trim() ?? "";

                // Para campos opcionales, usar string vacío si están vacíos
                _payroll.EmployeeCode = string.IsNullOrWhiteSpace(EmployeeCodeBox.Text)
                    ? "" : EmployeeCodeBox.Text.Trim();

                _payroll.HiredDate = HiredDatePicker.SelectedDate;
                _payroll.LastRaise = LastRaisePicker.SelectedDate;

                // Asegurar que Range y Condition no sean nulos
                _payroll.Range = (RangeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                _payroll.Condition = (ConditionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

                _payroll.SSPayroll = ParseDecimal(SSPayrollBox.Text);
                _payroll.WeeklyPayroll = ParseDecimal(WeeklyPayrollBox.Text);
                _payroll.SocialSecurity = ParseDecimal(SocialSecurityBox.Text);
                _payroll.BenefitsAmount = ParseDecimal(BenefitsAmountBox.Text);

                // Para Benefits, usar string vacío si está vacío
                _payroll.Benefits = string.IsNullOrWhiteSpace(BenefitsBox.Text)
                    ? "" : BenefitsBox.Text.Trim();

                // Calcular nómina mensual
                decimal monthlyFromWeekly = (_payroll.WeeklyPayroll ?? 0) * 52 / 12;
                _payroll.MonthlyPayroll = monthlyFromWeekly +
                    (_payroll.SSPayroll ?? 0) +
                    (_payroll.SocialSecurity ?? 0) +
                    (_payroll.BenefitsAmount ?? 0);

                // Asegurar que IsActive tenga un valor
                if (!_payroll.IsActive)
                    _payroll.IsActive = true;

                // Guardar con fecha efectiva
                bool success = false;
                string successMessage = "";

                // Si es edición y hay cambio de salario con fecha efectiva
                if (_isEdit && EffectiveDateBorder.Visibility == Visibility.Visible)
                {
                    success = await _supabaseService.SavePayrollWithEffectiveDate(
                        _payroll, effectiveDate, _currentUser.Id);
                    successMessage = $"Empleado actualizado. Cambios aplicados desde {effectiveDate:dd/MM/yyyy}";
                }
                else if (_isNewEmployee)
                {
                    // Para nuevo empleado, usar el método existente
                    _payroll.CreatedBy = _currentUser.Id;
                    await _supabaseService.CreatePayroll(_payroll);
                    success = true;
                    successMessage = "Empleado creado exitosamente";
                }
                else
                {
                    // Para actualización sin cambio de salario
                    _payroll.UpdatedBy = _currentUser.Id;
                    await _supabaseService.UpdatePayroll(_payroll);
                    success = true;
                    successMessage = "Empleado actualizado exitosamente";
                }

                if (success)
                {
                    
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Error al guardar", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                
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