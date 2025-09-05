using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SistemaGestionProyectos2.ViewModels
{
    public class SupplierExpensesSummaryViewModel : INotifyPropertyChanged
    {
        private int _supplierId;
        private string _supplierName;
        private decimal _totalPending;
        private decimal _totalPaid;
        private decimal _totalOverdue;
        private int _pendingCount;
        private int _paidCount;
        private int _overdueCount;
        private ObservableCollection<ExpenseViewModel> _expenses;

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

        public decimal TotalPending
        {
            get => _totalPending;
            set
            {
                _totalPending = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPendingDisplay));
            }
        }

        public decimal TotalPaid
        {
            get => _totalPaid;
            set
            {
                _totalPaid = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPaidDisplay));
            }
        }

        public decimal TotalOverdue
        {
            get => _totalOverdue;
            set
            {
                _totalOverdue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalOverdueDisplay));
            }
        }

        public int PendingCount
        {
            get => _pendingCount;
            set { _pendingCount = value; OnPropertyChanged(); }
        }

        public int PaidCount
        {
            get => _paidCount;
            set { _paidCount = value; OnPropertyChanged(); }
        }

        public int OverdueCount
        {
            get => _overdueCount;
            set { _overdueCount = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ExpenseViewModel> Expenses
        {
            get => _expenses;
            set { _expenses = value; OnPropertyChanged(); }
        }

        // Propiedades calculadas
        public decimal GrandTotal => TotalPending + TotalPaid;
        public string GrandTotalDisplay => $"${GrandTotal:N2}";
        public string TotalPendingDisplay => $"${TotalPending:N2}";
        public string TotalPaidDisplay => $"${TotalPaid:N2}";
        public string TotalOverdueDisplay => $"${TotalOverdue:N2}";

        public SupplierExpensesSummaryViewModel()
        {
            Expenses = new ObservableCollection<ExpenseViewModel>();
        }

        public void UpdateSummary()
        {
            if (Expenses == null) return;

            TotalPending = Expenses.Where(e => e.IsPending).Sum(e => e.TotalExpense);
            TotalPaid = Expenses.Where(e => e.IsPaid).Sum(e => e.TotalExpense);
            TotalOverdue = Expenses.Where(e => e.IsOverdue).Sum(e => e.TotalExpense);

            PendingCount = Expenses.Count(e => e.IsPending);
            PaidCount = Expenses.Count(e => e.IsPaid);
            OverdueCount = Expenses.Count(e => e.IsOverdue);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}