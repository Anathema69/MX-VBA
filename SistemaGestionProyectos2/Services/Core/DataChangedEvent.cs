using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SistemaGestionProyectos2.Services.Core
{
    /// <summary>
    /// Bus de eventos ligero para notificar cambios de datos entre módulos.
    /// Los Services publican tras cada mutación; las Views se suscriben para saber cuándo recargar.
    /// </summary>
    public static class DataChangedEvent
    {
        public static class Topics
        {
            public const string Orders = "orders";
            public const string Invoices = "invoices";
            public const string Expenses = "expenses";
            public const string Clients = "clients";
            public const string Vendors = "vendors";
            public const string Suppliers = "suppliers";
            public const string Payroll = "payroll";
            public const string Attendance = "attendance";
            public const string Contacts = "contacts";
            public const string Drive = "drive";
            public const string Inventory = "inventory";
        }

        private static readonly ConcurrentDictionary<string, List<SubscriptionEntry>> _subscriptions = new();
        private static readonly object _lock = new();

        private class SubscriptionEntry
        {
            public object Owner { get; set; }
            public Action Callback { get; set; }
        }

        /// <summary>
        /// Publica un cambio. Todos los suscriptores del topic reciben la notificación en el UI thread.
        /// </summary>
        public static void Publish(string topic)
        {
            if (!_subscriptions.TryGetValue(topic, out var entries)) return;

            List<SubscriptionEntry> snapshot;
            lock (_lock)
            {
                snapshot = entries.ToList();
            }

            foreach (var entry in snapshot)
            {
                try
                {
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(entry.Callback);
                    }
                    else
                    {
                        entry.Callback();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DataChangedEvent] Error notifying subscriber for '{topic}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Suscribe un owner (típicamente una Window) a un topic.
        /// Usar owner permite desuscribir todo de golpe al cerrar la ventana.
        /// </summary>
        public static void Subscribe(object owner, string topic, Action callback)
        {
            var entries = _subscriptions.GetOrAdd(topic, _ => new List<SubscriptionEntry>());
            lock (_lock)
            {
                entries.Add(new SubscriptionEntry { Owner = owner, Callback = callback });
            }
        }

        /// <summary>
        /// Suscribe a múltiples topics de una vez.
        /// </summary>
        public static void Subscribe(object owner, string[] topics, Action callback)
        {
            foreach (var topic in topics)
            {
                Subscribe(owner, topic, callback);
            }
        }

        /// <summary>
        /// Desuscribe todas las suscripciones de un owner. Llamar en OnClosed().
        /// </summary>
        public static void Unsubscribe(object owner)
        {
            lock (_lock)
            {
                foreach (var kvp in _subscriptions)
                {
                    kvp.Value.RemoveAll(e => ReferenceEquals(e.Owner, owner));
                }
            }
        }
    }
}
