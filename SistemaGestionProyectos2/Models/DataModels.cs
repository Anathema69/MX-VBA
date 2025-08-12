using System;

namespace SistemaGestionProyectos2.Models
{
    // Modelo para datos de órdenes
    public class OrderData
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public string QuotationNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public int ContactId { get; set; }
        public string ContactName { get; set; }
        public string Description { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public DateTime PromiseDate { get; set; }
        public int ProgressPercentage { get; set; }
        public int OrderPercentage { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Total { get; set; }
        public decimal Expense { get; set; }
        public string Status { get; set; }
        public int StatusId { get; set; }
        public bool Invoiced { get; set; }
        public DateTime? LastInvoiceDate { get; set; }
    }

    // Modelo para clientes
    public class ClientData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string TaxId { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }

    // Modelo para contactos
    public class ContactData
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Position { get; set; }
    }
}