using System;
using System.Globalization;
using System.Windows;

namespace SistemaGestionProyectos2.Views
{
    public partial class EffectiveDateDialog : Window
    {
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");

        public DateTime SelectedEffectiveDate { get; private set; }
        public bool IsConfirmed { get; private set; }

        public EffectiveDateDialog(string itemDescription, decimal currentAmount, decimal newAmount)
        {
            InitializeComponent();

            // Configurar textos
            ItemDescriptionText.Text = itemDescription;
            CurrentAmountText.Text = currentAmount.ToString("C", _mexicanCulture);
            NewAmountText.Text = newAmount.ToString("C", _mexicanCulture);

            // Configurar meses
            var currentMonth = DateTime.Now;
            var nextMonth = currentMonth.AddMonths(1);

            CurrentMonthText.Text = $"Este mes ({currentMonth.ToString("MMMM yyyy", _mexicanCulture)})";
            NextMonthText.Text = $"Próximo mes ({nextMonth.ToString("MMMM yyyy", _mexicanCulture)})";

            // Calcular impacto
            var difference = newAmount - currentAmount;
            var percentage = currentAmount > 0 ? Math.Abs(difference / currentAmount * 100) : 0;

            if (difference > 0)
            {
                ImpactText.Text = $"📈 Este cambio aumentará el gasto fijo mensual en {difference.ToString("C", _mexicanCulture)} ({percentage:F1}%)";
            }
            else if (difference < 0)
            {
                ImpactText.Text = $"📉 Este cambio reducirá el gasto fijo mensual en {Math.Abs(difference).ToString("C", _mexicanCulture)} ({percentage:F1}%)";
            }
            else
            {
                ImpactText.Text = "Sin cambios en el monto";
            }

            IsConfirmed = false;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (NextMonthRadio.IsChecked == true)
            {
                SelectedEffectiveDate = new DateTime(
                    DateTime.Now.AddMonths(1).Year,
                    DateTime.Now.AddMonths(1).Month, 1);
            }
            else
            {
                SelectedEffectiveDate = new DateTime(
                    DateTime.Now.Year,
                    DateTime.Now.Month, 1);
            }

            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}