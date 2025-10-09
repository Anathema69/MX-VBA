using SistemaGestionProyectos2.Models.Database;
using System;
using System.Collections.Generic;

namespace SistemaGestionProyectos2.Models.DTOs
{
    public class ClientPendingData
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public decimal TotalPending { get; set; }
    }

    public class PendingInvoiceDetail
    {
        public int InvoiceId { get; set; }
        public string Folio { get; set; }
        public decimal Total { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? ReceptionDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int ClientCredit { get; set; }
        public string OrderPO { get; set; }
        public int OrderId { get; set; }
        public string Status { get; set; }
        public int DaysOverdue { get; set; }
        public int DaysUntilDue { get; set; }
    }

    public class PendingIncomesData
    {
        public List<ClientPendingInfo> ClientsWithPendingInvoices { get; set; }
        public Dictionary<int, OrderDb> OrdersDictionary { get; set; }
        public Dictionary<int, ClientDb> ClientsDictionary { get; set; }
    }

    public class ClientPendingInfo
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int ClientCredit { get; set; }
        public List<InvoiceDb> Invoices { get; set; }
        public decimal TotalPending { get; set; }
    }

    public class ClientInvoicesDetailData
    {
        public ClientDb Client { get; set; }
        public List<InvoiceDetailInfo> Invoices { get; set; }
    }

    public class InvoiceDetailInfo
    {
        public InvoiceDb Invoice { get; set; }
        public string OrderPO { get; set; }
        public int OrderId { get; set; }
    }
}
