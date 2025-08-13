using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewOrderWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private List<ClientDb> _clients;
        private List<ContactDb> _contacts;
        private List<VendorDb> _vendors;
        private UserSession _currentUser;
        private bool _isLoading = false;

        public NewOrderWindow()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;

            // Obtener usuario actual del Owner window si es posible
            if (this.Owner is OrdersManagementWindow parentWindow)
            {
                // Podríamos pasar el usuario si lo necesitamos
            }

            _ = LoadInitialDataAsync();
        }

        // Constructor alternativo con usuario
        public NewOrderWindow(UserSession currentUser) : this()
        {
            _currentUser = currentUser;
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                _isLoading = true;
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Cargando datos...";

                // Cargar clientes desde Supabase
                _clients = await _supabaseService.GetClients();

                if (_clients != null && _clients.Count > 0)
                {
                    ClientComboBox.ItemsSource = _clients;
                    ClientComboBox.DisplayMemberPath = "Name";
                    ClientComboBox.SelectedValuePath = "Id";

                    System.Diagnostics.Debug.WriteLine($"✅ Clientes cargados: {_clients.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontraron clientes");
                }

                // Cargar vendedores desde Supabase
                _vendors = await _supabaseService.GetVendors();

                if (_vendors != null && _vendors.Count > 0)
                {
                    VendorComboBox.ItemsSource = _vendors;
                    VendorComboBox.DisplayMemberPath = "VendorName";
                    VendorComboBox.SelectedValuePath = "Id";

                    System.Diagnostics.Debug.WriteLine($"✅ Vendedores cargados: {_vendors.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontraron vendedores");
                }

                // Establecer fecha por defecto
                OrderDatePicker.SelectedDate = DateTime.Now;
                DeliveryDatePicker.SelectedDate = DateTime.Now.AddDays(30);

                System.Diagnostics.Debug.WriteLine($"✅ Datos iniciales cargados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando datos iniciales: {ex.Message}");
                MessageBox.Show(
                    $"Error al cargar datos:\n{ex.Message}\n\n" +
                    "Verifique su conexión a internet.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _isLoading = false;
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR";
            }
        }

        private async void ClientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientComboBox.SelectedItem is ClientDb selectedClient)
            {
                System.Diagnostics.Debug.WriteLine($"📋 Cliente seleccionado: {selectedClient.Name} (ID: {selectedClient.Id})");

                ContactComboBox.IsEnabled = false;
                ContactComboBox.ItemsSource = null;

                try
                {
                    // Cargar contactos del cliente desde Supabase
                    _contacts = await _supabaseService.GetContactsByClient(selectedClient.Id);

                    System.Diagnostics.Debug.WriteLine($"📞 Contactos encontrados: {_contacts?.Count ?? 0}");

                    if (_contacts != null && _contacts.Count > 0)
                    {
                        // Debug: Imprimir los contactos encontrados
                        foreach (var contact in _contacts)
                        {
                            System.Diagnostics.Debug.WriteLine($"   - Contacto: {contact.ContactName ?? "SIN NOMBRE"} (ID: {contact.Id})");
                        }

                        ContactComboBox.ItemsSource = _contacts;
                        ContactComboBox.DisplayMemberPath = "ContactName";
                        ContactComboBox.SelectedValuePath = "Id";
                        ContactComboBox.IsEnabled = true;

                        // Si solo hay un contacto, seleccionarlo automáticamente
                        if (_contacts.Count == 1)
                        {
                            ContactComboBox.SelectedIndex = 0;
                            System.Diagnostics.Debug.WriteLine($"✅ Auto-seleccionado único contacto");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ No hay contactos para el cliente {selectedClient.Name}");

                        // Crear un contacto temporal/genérico
                        _contacts = new List<ContactDb>
                        {
                            new ContactDb
                            {
                                Id = 0,
                                ContactName = $"Contacto General - {selectedClient.Name}",
                                ClientId = selectedClient.Id,
                                Email = "sin-email@temporal.com"
                            }
                        };

                        ContactComboBox.ItemsSource = _contacts;
                        ContactComboBox.DisplayMemberPath = "ContactName";
                        ContactComboBox.SelectedValuePath = "Id";
                        ContactComboBox.IsEnabled = true;
                        ContactComboBox.SelectedIndex = 0;

                        System.Diagnostics.Debug.WriteLine($"ℹ️ Creado contacto temporal para {selectedClient.Name}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error cargando contactos: {ex.Message}");

                    // En caso de error, crear contacto temporal
                    _contacts = new List<ContactDb>
                    {
                        new ContactDb
                        {
                            Id = 0,
                            ContactName = "Error al cargar contactos",
                            ClientId = selectedClient.Id
                        }
                    };

                    ContactComboBox.ItemsSource = _contacts;
                    ContactComboBox.DisplayMemberPath = "ContactName";
                    ContactComboBox.SelectedValuePath = "Id";
                    ContactComboBox.IsEnabled = true;
                }
            }
            else
            {
                ContactComboBox.IsEnabled = false;
                ContactComboBox.ItemsSource = null;
                System.Diagnostics.Debug.WriteLine("⚠️ No hay cliente seleccionado");
            }
        }

        private void SubtotalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateTotal();
        }

        private void CalculateTotal()
        {
            if (decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal))
            {
                decimal total = subtotal * 1.16m; // Agregar 16% de IVA
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));
            }
            else
            {
                TotalTextBlock.Text = "$ 0.00";
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números y punto decimal
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            var newText = (sender as TextBox).Text + e.Text;
            e.Handled = !regex.IsMatch(newText);
        }


        // Reemplazar el método SaveButton_Click en NewOrderWindow.xaml.cs

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validar campos obligatorios
            if (!ValidateForm())
            {
                return;
            }

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // Obtener el ID del contacto seleccionado de manera segura
                int? contactId = null;
                if (ContactComboBox.SelectedItem is ContactDb selectedContact)
                {
                    // Si es un contacto real (ID > 0), usar su ID
                    if (selectedContact.Id > 0)
                    {
                        contactId = selectedContact.Id;
                    }
                    // Si es un contacto temporal (ID = 0), dejar como null
                    else
                    {
                        contactId = null;
                    }
                }

                // Crear nueva orden con valores por defecto para los porcentajes
                var newOrder = new OrderDb
                {
                    Po = OrderNumberTextBox.Text.Trim().ToUpper(),
                    Quote = QuotationTextBox.Text?.Trim().ToUpper(),
                    PoDate = OrderDatePicker.SelectedDate,
                    ClientId = (int?)ClientComboBox.SelectedValue,
                    ContactId = contactId,
                    Description = DescriptionTextBox.Text.Trim(),
                    SalesmanId = (int?)VendorComboBox.SelectedValue,
                    EstDelivery = DeliveryDatePicker.SelectedDate,
                    SaleSubtotal = decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal) ? subtotal : 0,
                    SaleTotal = subtotal * 1.16m,
                    Expense = decimal.TryParse(ExpenseTextBox.Text, out decimal expense) ? expense : 0,
                    OrderStatus = 1, // Estado inicial: PENDIENTE
                    ProgressPercentage = 0, // Inicializar en 0
                    OrderPercentage = 0     // Inicializar en 0
                };

                // Log para depuración
                System.Diagnostics.Debug.WriteLine($"📋 Creando orden:");
                System.Diagnostics.Debug.WriteLine($"   PO: {newOrder.Po}");
                System.Diagnostics.Debug.WriteLine($"   Cliente ID: {newOrder.ClientId}");
                System.Diagnostics.Debug.WriteLine($"   Contacto ID: {newOrder.ContactId ?? 0} (null = sin contacto)");
                System.Diagnostics.Debug.WriteLine($"   Vendedor ID: {newOrder.SalesmanId}");
                System.Diagnostics.Debug.WriteLine($"   Total: {newOrder.SaleTotal}");
                System.Diagnostics.Debug.WriteLine($"   Progress%: {newOrder.ProgressPercentage}");
                System.Diagnostics.Debug.WriteLine($"   Order%: {newOrder.OrderPercentage}");

                // IMPORTANTE: Pasar el ID del usuario actual correctamente
                int userId = 0;
                if (_currentUser != null)
                {
                    userId = _currentUser.Id;
                    System.Diagnostics.Debug.WriteLine($"👤 Usuario creando la orden: {_currentUser.FullName} (ID: {userId})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ No hay usuario en sesión, usando ID por defecto");
                    userId = 1; // Fallback solo si no hay usuario
                }

                var createdOrder = await _supabaseService.CreateOrder(newOrder, userId);

                if (createdOrder != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Orden creada con ID: {createdOrder.Id}");

                    MessageBox.Show(
                        $"✅ Orden {newOrder.Po} guardada exitosamente.\n\n" +
                        $"Cliente: {(ClientComboBox.SelectedItem as ClientDb)?.Name}\n" +
                        $"Vendedor: {(VendorComboBox.SelectedItem as VendorDb)?.VendorName}\n" +
                        $"Total: {TotalTextBlock.Text}",
                        "Orden Guardada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    throw new Exception("No se pudo crear la orden en la base de datos");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar la orden:\n{ex.Message}\n\n" +
                    "Verifique que todos los campos estén correctos y que tenga conexión a internet.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"❌ Error completo: {ex}");
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR";
            }
        }

        // También actualizar el método ValidateForm para ser menos estricto con el contacto
        private bool ValidateForm()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(OrderNumberTextBox.Text))
                errors.Add("• Orden de Compra es obligatorio");

            if (!OrderDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha O.C. es obligatoria");

            if (ClientComboBox.SelectedItem == null)
                errors.Add("• Cliente es obligatorio");

            // El contacto NO es obligatorio - puede ser null
            // Removemos la validación del contacto

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
                errors.Add("• Descripción es obligatoria");

            if (VendorComboBox.SelectedItem == null)
                errors.Add("• Vendedor es obligatorio");

            if (string.IsNullOrWhiteSpace(SubtotalTextBox.Text) ||
                !decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal) ||
                subtotal <= 0)
                errors.Add("• Subtotal debe ser mayor a 0");

            if (!DeliveryDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha de Entrega es obligatoria");

            // Validar que la fecha de entrega sea posterior a la fecha de orden
            if (OrderDatePicker.SelectedDate.HasValue && DeliveryDatePicker.SelectedDate.HasValue)
            {
                if (DeliveryDatePicker.SelectedDate.Value < OrderDatePicker.SelectedDate.Value)
                {
                    errors.Add("• La fecha de entrega debe ser posterior a la fecha de O.C.");
                }
            }

            if (errors.Any())
            {
                MessageBox.Show(
                    "Por favor corrija los siguientes errores:\n\n" + string.Join("\n", errors),
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                MessageBox.Show(
                    "Por favor espere a que se carguen los datos.",
                    "Cargando",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Verificar si hay cambios sin guardar
            bool hasChanges = !string.IsNullOrWhiteSpace(OrderNumberTextBox.Text) ||
                            !string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ||
                            !string.IsNullOrWhiteSpace(SubtotalTextBox.Text) ||
                            ClientComboBox.SelectedItem != null;

            if (hasChanges)
            {
                var result = MessageBox.Show(
                    "¿Está seguro que desea cancelar?\nLos datos no guardados se perderán.",
                    "Confirmar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.DialogResult = false;
            this.Close();
        }

        private void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Función para agregar nuevo cliente.\n" +
                "Será implementada en una próxima versión.\n\n" +
                "Por ahora, use los clientes existentes.",
                "Nuevo Cliente",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Permitir solo números en el campo de gasto
        private void ExpenseTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            NumericTextBox_PreviewTextInput(sender, e);
        }
    }
}