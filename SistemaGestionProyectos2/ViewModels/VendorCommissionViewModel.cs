using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaGestionProyectos2.ViewModels
{
    public class VendorCommissionViewModel : INotifyPropertyChanged
    {
        private int _orderId;
        private string _orderNumber;
        private string _vendorName;
        private string _companyName;
        private string _description;
        private decimal _commissionRate;
        private decimal _subtotal;
        private decimal _commission;
        private DateTime? _orderDate;
        private bool _isEditable;

        public int OrderId
        {
            get => _orderId;
            set
            {
                _orderId = value;
                OnPropertyChanged();
            }
        }

        public string OrderNumber
        {
            get => _orderNumber;
            set
            {
                _orderNumber = value;
                OnPropertyChanged();
            }
        }

        public string VendorName
        {
            get => _vendorName;
            set
            {
                _vendorName = value;
                OnPropertyChanged();
            }
        }

        public string CompanyName
        {
            get => _companyName;
            set
            {
                _companyName = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public decimal CommissionRate
        {
            get => _commissionRate;
            set
            {
                if (value < 0) value = 0;
                if (value > 100) value = 100;

                _commissionRate = value;
                OnPropertyChanged();
                CalculateCommission();
            }
        }

        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubtotalFormatted));
                CalculateCommission();
            }
        }

        public decimal Commission
        {
            get => _commission;
            set
            {
                _commission = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CommissionFormatted));
            }
        }

        public DateTime? OrderDate
        {
            get => _orderDate;
            set
            {
                _orderDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OrderDateFormatted));
            }
        }

        public bool IsEditable
        {
            get => _isEditable;
            set
            {
                _isEditable = value;
                OnPropertyChanged();
            }
        }

        // Propiedades formateadas para mostrar en la UI
        public string SubtotalFormatted => Subtotal.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        public string CommissionFormatted => Commission.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        public string OrderDateFormatted => OrderDate?.ToString("dd/MM/yyyy") ?? "";
        public string CommissionRateFormatted => $"{CommissionRate:F2}%";

        // Método para calcular la comisión
        private void CalculateCommission()
        {
            Commission = Math.Round((Subtotal * CommissionRate) / 100, 2);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}