using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaGestionProyectos2.ViewModels
{
    public class ExpenseViewModel : INotifyPropertyChanged
    {
        private int _expenseId;
        private int _supplierId;
        private string _supplierName;
        private string _description;
        private DateTime _expenseDate;
        private decimal _totalExpense;
        private DateTime? _scheduledDate;
        private string _status;
        private DateTime? _paidDate;
        private string _payMethod;
        private int? _orderId;
        private string _orderNumber;
        private string _expenseCategory;
        private bool _isSelected;
        private bool _isEditing;

        public int ExpenseId
        {
            get => _expenseId;
            set { _expenseId = value; OnPropertyChanged(); }
        }

        public int SupplierId
        {
            get => _supplierId;
            set { _supplierId = value; OnPropertyChanged(); }
        }

        public string SupplierName
        {
            get => _supplierName;
            set { _supplierName = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public DateTime ExpenseDate
        {
            get => _expenseDate;
            set
            {
                _expenseDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpenseDateDisplay));
            }
        }

        public decimal TotalExpense
        {
            get => _totalExpense;
            set
            {
                _totalExpense = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalExpenseDisplay));
            }
        }

        public DateTime? ScheduledDate
        {
            get => _scheduledDate;
            set
            {
                _scheduledDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScheduledDateDisplay));
                OnPropertyChanged(nameof(IsOverdue));
                OnPropertyChanged(nameof(DaysUntilDue));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPaid));
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(IsOverdue));
            }
        }

        public DateTime? PaidDate
        {
            get => _paidDate;
            set
            {
                _paidDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PaidDateDisplay));
            }
        }

        public string PayMethod
        {
            get => _payMethod;
            set { _payMethod = value; OnPropertyChanged(); }
        }

        public int? OrderId
        {
            get => _orderId;
            set { _orderId = value; OnPropertyChanged(); }
        }

        public string OrderNumber
        {
            get => _orderNumber;
            set { _orderNumber = value; OnPropertyChanged(); }
        }

        public string ExpenseCategory
        {
            get => _expenseCategory;
            set { _expenseCategory = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); }
        }

        // Propiedades calculadas
        public bool IsPaid => Status == "PAGADO";
        public bool IsPending => Status == "PENDIENTE";

        public bool IsOverdue => !IsPaid && ScheduledDate.HasValue && ScheduledDate.Value < DateTime.Now.Date;

        public int DaysUntilDue
        {
            get
            {
                if (IsPaid || !ScheduledDate.HasValue)
                    return 0;

                var days = (ScheduledDate.Value.Date - DateTime.Now.Date).Days;
                return days;
            }
        }

        public string StatusColor
        {
            get
            {
                if (IsPaid) return "#4CAF50"; // Verde
                if (IsOverdue) return "#F44336"; // Rojo
                if (DaysUntilDue <= 3) return "#FF9800"; // Naranja (próximo a vencer)
                return "#2196F3"; // Azul (pendiente normal)
            }
        }

        // Propiedades de formato para mostrar
        public string ExpenseDateDisplay => ExpenseDate.ToString("dd/MM/yyyy");
        public string ScheduledDateDisplay => ScheduledDate?.ToString("dd/MM/yyyy") ?? "-";
        public string PaidDateDisplay => PaidDate?.ToString("dd/MM/yyyy") ?? "-";
        public string TotalExpenseDisplay => $"${TotalExpense:N2}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}