// Crear nuevo archivo: Services/OrderExtensions.cs

using System;
using SistemaGestionProyectos2.Models;

namespace SistemaGestionProyectos2.Services
{
    public static class OrderExtensions
    {
        // Métodos de extensión para obtener los valores con alias
        public static string GetOrderNumber(this OrderDb order)
        {
            return order.Po;
        }

        public static string GetQuotationNumber(this OrderDb order)
        {
            return order.Quote;
        }

        public static DateTime? GetOrderDate(this OrderDb order)
        {
            return order.PoDate;
        }

        public static int? GetVendorId(this OrderDb order)
        {
            return order.SalesmanId;
        }

        public static DateTime? GetPromiseDate(this OrderDb order)
        {
            return order.EstDelivery;
        }

        public static decimal GetSubtotal(this OrderDb order)
        {
            return order.SaleSubtotal ?? 0;
        }

        public static decimal GetTotal(this OrderDb order)
        {
            return order.SaleTotal ?? 0;
        }

        public static int? GetStatusId(this OrderDb order)
        {
            return order.OrderStatus;
        }

        // Método para convertir OrderDb a OrderViewModel
        public static OrderViewModel ToViewModel(this OrderDb order, string clientName = null, string vendorName = null, string statusName = null)
        {
            return new OrderViewModel
            {
                Id = order.Id,
                OrderNumber = order.Po ?? "N/A",
                OrderDate = order.PoDate ?? DateTime.Now,
                ClientName = clientName ?? "Sin cliente",
                Description = order.Description ?? "",
                VendorName = vendorName ?? "Sin vendedor",
                PromiseDate = order.EstDelivery ?? DateTime.Now.AddDays(30),
                ProgressPercentage = order.ProgressPercentage,
                OrderPercentage = order.OrderPercentage,
                Subtotal = order.SaleSubtotal ?? 0,
                Total = order.SaleTotal ?? 0,
                Status = statusName ?? "PENDIENTE",
                Invoiced = false,
                LastInvoiceDate = null
            };
        }
    }
}