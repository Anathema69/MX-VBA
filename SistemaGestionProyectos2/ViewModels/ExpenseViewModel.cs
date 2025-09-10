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
        
        private string _expenseCategory;
        private bool _isNew;
        private bool _isEditing;
        private bool _hasChanges;

        private int? _orderId;
        private string _orderNumber;

        public int ExpenseId
        {
            get => _expenseId;
            set
            {
                _expenseId = value;
                OnPropertyChanged();
            }
        }

        


        public int SupplierId
        {
            get => _supplierId;
            set
            {
                _supplierId = value;
                _hasChanges = true;
                OnPropertyChanged();
            }
        }

        public string SupplierName
        {
            get => _supplierName;
            set
            {
                _supplierName = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                _hasChanges = true;
                OnPropertyChanged();
            }
        }

        public DateTime ExpenseDate
        {
            get => _expenseDate;
            set
            {
                _expenseDate = value;
                _hasChanges = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpenseDateDisplay));
                OnPropertyChanged(nameof(IsOverdue));
            }
        }

        public decimal TotalExpense
        {
            get => _totalExpense;
            set
            {
                _totalExpense = value;
                _hasChanges = true;
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
            set
            {
                _payMethod = value;
                OnPropertyChanged();
            }
        }

        public int? OrderId
        {
            get => _orderId;
            set
            {
                _orderId = value;
                OnPropertyChanged();
                // Si no se proporciona OrderNumber externamente, generar uno por defecto
                if (string.IsNullOrEmpty(_orderNumber) && value.HasValue)
                {
                    _orderNumber = $"ORD-{value.Value:D5}";
                    OnPropertyChanged(nameof(OrderNumber));
                }
            }
        }

        public string ExpenseCategory
        {
            get => _expenseCategory;
            set
            {
                _expenseCategory = value;
                _hasChanges = true;
                OnPropertyChanged();
            }
        }

        public bool IsNew
        {
            get => _isNew;
            set
            {
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }

        public bool HasChanges
        {
            get => _hasChanges;
            set
            {
                _hasChanges = value;
                OnPropertyChanged();
            }
        }

        // Propiedades calculadas para la visualización
        public string ExpenseDateDisplay => ExpenseDate.ToString("dd/MM/yyyy");

        public string ScheduledDateDisplay => ScheduledDate?.ToString("dd/MM/yyyy") ?? "-";

        public string PaidDateDisplay => PaidDate?.ToString("dd/MM/yyyy") ?? "-";

        public string TotalExpenseDisplay => $"${TotalExpense:N2}";

        public string OrderNumber
        {
            get => _orderNumber ?? (OrderId.HasValue ? $"ORD-{OrderId.Value:D5}" : string.Empty);
            set
            {
                _orderNumber = value;
                OnPropertyChanged();
            }
        }

        public bool IsPaid => Status == "PAGADO";

        public bool IsPending => Status == "PENDIENTE";

        public bool IsOverdue
        {
            get
            {
                if (Status == "PAGADO") return false;
                if (ScheduledDate.HasValue && ScheduledDate.Value < DateTime.Now.Date)
                {
                    return true;
                }
                return false;
            }
        }

        // Constructor
        public ExpenseViewModel()
        {
            _expenseDate = DateTime.Now;
            _status = "PENDIENTE";
            _isNew = false;
            _isEditing = false;
            _hasChanges = false;
        }

        // Implementación de INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Método para resetear los cambios
        public void ResetChanges()
        {
            _hasChanges = false;
            OnPropertyChanged(nameof(HasChanges));
        }

        // Método para clonar el objeto (útil para edición)
        public ExpenseViewModel Clone()
        {
            return new ExpenseViewModel
            {
                ExpenseId = this.ExpenseId,
                SupplierId = this.SupplierId,
                SupplierName = this.SupplierName,
                Description = this.Description,
                ExpenseDate = this.ExpenseDate,
                TotalExpense = this.TotalExpense,
                ScheduledDate = this.ScheduledDate,
                Status = this.Status,
                PaidDate = this.PaidDate,
                PayMethod = this.PayMethod,
                OrderId = this.OrderId,
                ExpenseCategory = this.ExpenseCategory,
                IsNew = this.IsNew,
                IsEditing = this.IsEditing,
                HasChanges = false
            };
        }
    }
}