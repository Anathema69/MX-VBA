// Archivo: Models/InvoiceViewModel.cs - VERSIÓN CORREGIDA

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaGestionProyectos2.Models
{
    public class InvoiceViewModel : INotifyPropertyChanged
    {
        private int _id;
        private int _orderId;
        private string _folio;
        private DateTime? _invoiceDate;
        private DateTime? _receptionDate;
        private decimal _subtotal;
        private decimal _total;
        private DateTime? _paymentDate;
        private DateTime? _dueDate;
        private string _status;
        private int _statusId;
        private bool _isEditing;
        private bool _hasChanges;
        private bool _isNew;

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public int OrderId
        {
            get => _orderId;
            set
            {
                _orderId = value;
                OnPropertyChanged();
            }
        }

        public string Folio
        {
            get => _folio;
            set
            {
                _folio = value;
                _hasChanges = true;
                OnPropertyChanged();
            }
        }

        public DateTime? InvoiceDate
        {
            get => _invoiceDate;
            set
            {
                _invoiceDate = value;
                _hasChanges = true;
                OnPropertyChanged();
            }
        }

        public DateTime? ReceptionDate
        {
            get => _receptionDate;
            set
            {
                _receptionDate = value;
                _hasChanges = true;
                UpdateDueDate();
                UpdateStatus();
                OnPropertyChanged();
            }
        }

        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                _total = value * 1.16m; // Calcular total con IVA
                _hasChanges = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(SubtotalFormatted));
                OnPropertyChanged(nameof(TotalFormatted));
            }
        }

        // CAMBIO IMPORTANTE: Total ahora tiene set público
        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalFormatted));
            }
        }

        public DateTime? PaymentDate
        {
            get => _paymentDate;
            set
            {
                _paymentDate = value;
                _hasChanges = true;
                UpdateStatus();
                OnPropertyChanged();
            }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                _dueDate = value;
                UpdateStatus();
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public int StatusId
        {
            get => _statusId;
            set
            {
                _statusId = value;
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

        public bool IsNew
        {
            get => _isNew;
            set
            {
                _isNew = value;
                OnPropertyChanged();
            }
        }

        // Propiedades calculadas para formato
        public string SubtotalFormatted => Subtotal.ToString("C");
        public string TotalFormatted => Total.ToString("C");

        public string StatusColor
        {
            get
            {
                switch (StatusId)
                {
                    case 1: return "#2196F3"; // CREADA - Azul
                    case 2: return "#FFC107"; // PENDIENTE - Amarillo
                    case 3: return "#F44336"; // VENCIDA - Rojo
                    case 4: return "#4CAF50"; // PAGADA - Verde
                    default: return "#9E9E9E"; // Gris
                }
            }
        }

        // Días de crédito del cliente (se establecerá desde la ventana)
        public int ClientCreditDays { get; set; }

        // Métodos auxiliares
        private void UpdateDueDate()
        {
            if (ReceptionDate.HasValue && ClientCreditDays > 0)
            {
                DueDate = ReceptionDate.Value.AddDays(ClientCreditDays);
            }
        }

        private void UpdateStatus()
        {
            if (PaymentDate.HasValue)
            {
                StatusId = 4;
                Status = "PAGADA";
            }
            else if (ReceptionDate.HasValue)
            {
                if (DueDate.HasValue && DateTime.Now > DueDate.Value)
                {
                    StatusId = 3;
                    Status = "VENCIDA";
                }
                else
                {
                    StatusId = 2;
                    Status = "PENDIENTE";
                }
            }
            else
            {
                StatusId = 1;
                Status = "CREADA";
            }
        }

        // Método para establecer Total sin disparar el cálculo automático
        public void SetTotalDirectly(decimal total)
        {
            _total = total;
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(TotalFormatted));
        }

        // Método para recalcular el total basado en el subtotal
        public void RecalculateTotal()
        {
            _total = _subtotal * 1.16m;
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(TotalFormatted));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}