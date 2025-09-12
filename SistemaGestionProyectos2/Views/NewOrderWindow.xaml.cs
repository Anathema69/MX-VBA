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

        //Para el campo de subtotal
        
        private decimal _subtotalValue = 0;
        

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
                    // Crear lista con opción placeholder
                    var clientsWithPlaceholder = new List<ClientDb>();

                    // Agregar placeholder al inicio
                    clientsWithPlaceholder.Add(new ClientDb
                    {
                        Id = -1,
                        Name = "-- Seleccione un cliente --"
                    });

                    // Agregar los clientes reales
                    clientsWithPlaceholder.AddRange(_clients);

                    ClientComboBox.ItemsSource = clientsWithPlaceholder;
                    ClientComboBox.DisplayMemberPath = "Name";
                    ClientComboBox.SelectedValuePath = "Id";
                    ClientComboBox.SelectedIndex = 0; // Seleccionar el placeholder

                    System.Diagnostics.Debug.WriteLine($"✅ Clientes cargados: {_clients.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontraron clientes");
                    MessageBox.Show(
                        "No se encontraron clientes en la base de datos.\n" +
                        "Por favor, cree un cliente primero.",
                        "Sin Clientes",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Cargar vendedores desde Supabase
                _vendors = await _supabaseService.GetVendors();

                if (_vendors != null && _vendors.Count > 0)
                {
                    // Crear lista con opción placeholder
                    var vendorsWithPlaceholder = new List<VendorDb>();

                    // Agregar placeholder al inicio
                    vendorsWithPlaceholder.Add(new VendorDb
                    {
                        Id = -1,
                        VendorName = "-- Seleccione un vendedor --"
                    });

                    // Agregar los vendedores reales
                    vendorsWithPlaceholder.AddRange(_vendors);

                    VendorComboBox.ItemsSource = vendorsWithPlaceholder;
                    VendorComboBox.DisplayMemberPath = "VendorName";
                    VendorComboBox.SelectedValuePath = "Id";
                    VendorComboBox.SelectedIndex = 0; // Seleccionar el placeholder

                    System.Diagnostics.Debug.WriteLine($"✅ Vendedores cargados: {_vendors.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontraron vendedores");
                }

                // Establecer fecha por defecto
                OrderDatePicker.SelectedDate = DateTime.Now;
                DeliveryDatePicker.SelectedDate = DateTime.Now.AddDays(30);

                // Deshabilitar el ComboBox de contactos hasta que se seleccione un cliente
                ContactComboBox.IsEnabled = false;

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
            System.Diagnostics.Debug.WriteLine("=== INICIO ClientComboBox_SelectionChanged ===");

            // Limpiar contactos primero
            ContactComboBox.ItemsSource = null;
            ContactComboBox.IsEnabled = false;

            // CAMBIO IMPORTANTE: Obtener el cliente seleccionado directamente
            if (ClientComboBox.SelectedItem is ClientDb selectedClient)
            {
                System.Diagnostics.Debug.WriteLine($"📋 Cliente seleccionado: {selectedClient.Name} (ID: {selectedClient.Id})");

                // Verificar que no sea un placeholder o cliente inválido
                if (selectedClient.Id <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Cliente inválido o placeholder seleccionado");
                    return;
                }

                try
                {
                    // Usar el ID del cliente seleccionado directamente
                    System.Diagnostics.Debug.WriteLine($"🔄 Llamando a GetContactsByClient({selectedClient.Id})...");
                    _contacts = await _supabaseService.GetContactsByClient(selectedClient.Id);

                    System.Diagnostics.Debug.WriteLine($"📞 Respuesta: {_contacts?.Count ?? 0} contactos");

                    if (_contacts != null && _contacts.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("✅ Cargando contactos en el ComboBox...");

                        ContactComboBox.ItemsSource = _contacts;
                        ContactComboBox.DisplayMemberPath = "ContactName";
                        ContactComboBox.SelectedValuePath = "Id";
                        ContactComboBox.IsEnabled = true;

                        System.Diagnostics.Debug.WriteLine($"   ItemsSource asignado: {ContactComboBox.Items.Count} items");

                        // Si solo hay un contacto, seleccionarlo automáticamente
                        if (_contacts.Count == 1)
                        {
                            ContactComboBox.SelectedIndex = 0;
                            System.Diagnostics.Debug.WriteLine($"✅ Auto-seleccionado único contacto: {_contacts[0].ContactName}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ No hay contactos para el cliente {selectedClient.Name}");

                        // Crear opción genérica usando los datos del cliente
                        _contacts = new List<ContactDb>
                {
                    new ContactDb
                    {
                        Id = 0,
                        ContactName = "-- Sin contacto específico --",
                        ClientId = selectedClient.Id,
                        Email = selectedClient.Email ?? "general@empresa.com",
                        Phone = selectedClient.Phone ?? ""
                    }
                };

                        ContactComboBox.ItemsSource = _contacts;
                        ContactComboBox.DisplayMemberPath = "ContactName";
                        ContactComboBox.SelectedValuePath = "Id";
                        ContactComboBox.IsEnabled = true;
                        ContactComboBox.SelectedIndex = 0;

                        System.Diagnostics.Debug.WriteLine("ℹ️ Agregado contacto genérico");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
                    MessageBox.Show(
                        $"Error al cargar contactos:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ No hay cliente seleccionado");
            }

            System.Diagnostics.Debug.WriteLine("=== FIN ClientComboBox_SelectionChanged ===");
        }


        // En NewOrderWindow.xaml.cs, verificar que estos métodos estén correctos:

        private void SubtotalTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Al enfocar, mostrar solo el número sin formato
            if (_subtotalValue > 0)
            {
                SubtotalTextBox.Text = _subtotalValue.ToString("F2");
            }
            else if (SubtotalTextBox.Text == "$0.00" || SubtotalTextBox.Text == "0.00")
            {
                SubtotalTextBox.Text = "";
            }

            SubtotalTextBox.SelectAll();

            if (SubtotalHelpText != null)
            {
                SubtotalHelpText.Text = "Ingrese el monto sin símbolo de moneda";
                SubtotalHelpText.Foreground = System.Windows.Media.Brushes.Gray;
            }

            System.Diagnostics.Debug.WriteLine($"🔍 GotFocus - _subtotalValue actual: {_subtotalValue}");
        }

        private void SubtotalTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Limpiar el texto de cualquier formato
            string cleanText = SubtotalTextBox.Text
                .Replace("$", "")
                .Replace(",", "")
                .Replace(" ", "")
                .Trim();

            System.Diagnostics.Debug.WriteLine($"🔍 LostFocus - Texto limpio: '{cleanText}'");

            if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
            {
                _subtotalValue = subtotal;
                // Mostrar con formato de moneda
                SubtotalTextBox.Text = subtotal.ToString("C", new CultureInfo("es-MX"));

                System.Diagnostics.Debug.WriteLine($"✅ _subtotalValue establecido a: {_subtotalValue}");

                if (SubtotalHelpText != null)
                    SubtotalHelpText.Text = "";
            }
            else
            {
                _subtotalValue = 0;
                SubtotalTextBox.Text = "$0.00";

                System.Diagnostics.Debug.WriteLine($"⚠️ No se pudo parsear, _subtotalValue = 0");

                if (SubtotalHelpText != null)
                    SubtotalHelpText.Text = "";
            }
        }

        private void SubtotalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Verificar que los controles existan antes de usarlos
            if (TotalTextBlock == null || SubtotalHelpText == null)
                return;

            // Solo calcular si el TextBox tiene el foco (está siendo editado)
            if (SubtotalTextBox.IsFocused)
            {
                string text = SubtotalTextBox.Text.Replace("$", "").Replace(",", "").Trim();

                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal subtotal))
                {
                    _subtotalValue = subtotal;
                    decimal total = subtotal * 1.16m;
                    TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));

                    // Mostrar preview del formato
                    SubtotalHelpText.Text = $"= {subtotal.ToString("C", new CultureInfo("es-MX"))}";
                    SubtotalHelpText.Foreground = System.Windows.Media.Brushes.Gray;

                    System.Diagnostics.Debug.WriteLine($"📝 TextChanged - _subtotalValue actualizado a: {_subtotalValue}");
                }
                else if (!string.IsNullOrEmpty(SubtotalTextBox.Text))
                {
                    SubtotalHelpText.Text = "Ingrese solo números";
                    SubtotalHelpText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }

        


        private void CalculateTotal()
        {
            if (decimal.TryParse(SubtotalTextBox.Text, out decimal subtotal))
            {
                decimal total = subtotal * 1.16m;
                TotalTextBlock.Text = total.ToString("C", new CultureInfo("es-MX"));
            }
            else
            {
                TotalTextBlock.Text = "$0.00";
            }
        }
        

        

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números y un punto decimal
            var textBox = sender as TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            // Remover formato si existe
            fullText = fullText.Replace("$", "").Replace(",", "").Trim();

            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(fullText);
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

           

            this.DialogResult = false;
            this.Close();
        }

        private bool ValidateForm()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(OrderNumberTextBox.Text))
                errors.Add("• Orden de Compra es obligatorio");

            if (!OrderDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha O.C. es obligatoria");

            if (ClientComboBox.SelectedItem == null ||
                (ClientComboBox.SelectedItem is ClientDb client && client.Id <= 0))
            {
                errors.Add("• Debe seleccionar un cliente válido");
            }

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
                errors.Add("• Descripción es obligatoria");

            // CAMBIO: Ya NO validar vendedor como obligatorio
            // (Se quita esta validación completamente)

            if (_subtotalValue <= 0)
            {
                errors.Add($"• Subtotal debe ser mayor a 0");
            }

            if (!DeliveryDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha de Entrega es obligatoria");

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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // Obtener el cliente seleccionado
                ClientDb selectedClient = ClientComboBox.SelectedItem as ClientDb;
                if (selectedClient == null || selectedClient.Id <= 0)
                {
                    throw new Exception("Debe seleccionar un cliente válido");
                }

                // CAMBIO: Vendedor es opcional ahora
                int? vendorId = null;
                decimal commissionRate = 0;

                if (VendorComboBox.SelectedItem is VendorDb selectedVendor && selectedVendor.Id > 0)
                {
                    vendorId = selectedVendor.Id;
                    commissionRate = 10; // Si hay vendedor, asignar comisión
                    System.Diagnostics.Debug.WriteLine($"✅ Vendedor seleccionado: {selectedVendor.VendorName} (ID: {vendorId})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No se asignó vendedor a esta orden");
                }

                // Obtener el ID del contacto (puede ser null)
                int? contactId = null;
                if (ContactComboBox.SelectedItem is ContactDb selectedContact && selectedContact.Id > 0)
                {
                    contactId = selectedContact.Id;
                }

                var newOrder = new OrderDb
                {
                    Po = OrderNumberTextBox.Text.Trim().ToUpper(),
                    Quote = QuotationTextBox.Text?.Trim().ToUpper(),
                    PoDate = OrderDatePicker.SelectedDate,
                    ClientId = selectedClient.Id,
                    ContactId = contactId,
                    Description = DescriptionTextBox.Text.Trim(),
                    SalesmanId = vendorId,  // Puede ser null
                    EstDelivery = DeliveryDatePicker.SelectedDate,
                    SaleSubtotal = _subtotalValue,
                    SaleTotal = _subtotalValue * 1.16m,
                    Expense = 0,
                    OrderStatus = 0, // POR DEFECTO LAS NUEAS ORDENES ESTÁN EN 0 (CREADAS)
                    ProgressPercentage = 0,
                    OrderPercentage = 0,
                    CommissionRate = commissionRate  // 0 si no hay vendedor, 10 si hay vendedor
                };

                System.Diagnostics.Debug.WriteLine($"📋 Creando orden:");
                System.Diagnostics.Debug.WriteLine($"   PO: {newOrder.Po}");
                System.Diagnostics.Debug.WriteLine($"   Cliente: {selectedClient.Name} (ID: {newOrder.ClientId})");
                System.Diagnostics.Debug.WriteLine($"   Contacto ID: {newOrder.ContactId ?? 0}");
                System.Diagnostics.Debug.WriteLine($"   Vendedor ID: {newOrder.SalesmanId?.ToString() ?? "SIN VENDEDOR"}");
                System.Diagnostics.Debug.WriteLine($"   Comisión: {newOrder.CommissionRate}%");
                System.Diagnostics.Debug.WriteLine($"   Subtotal: ${newOrder.SaleSubtotal:N2}");
                System.Diagnostics.Debug.WriteLine($"   Total: ${newOrder.SaleTotal:N2}");

                int userId = _currentUser?.Id ?? 1;
                var createdOrder = await _supabaseService.CreateOrder(newOrder, userId);

                if (createdOrder != null)
                {
                    string vendorInfo = vendorId.HasValue ?
                        $"Vendedor: {(VendorComboBox.SelectedItem as VendorDb)?.VendorName}" :
                        "Sin vendedor asignado";

                    MessageBox.Show(
                        $"✅ Orden creada exitosamente\n\n" +
                        $"Orden: {createdOrder.Po}\n" +
                        $"Cliente: {selectedClient.Name}\n" +
                        $"{vendorInfo}",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar la orden:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"❌ Error: {ex}");
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR";
            }
        }



        private async void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Abrir ventana de nuevo cliente
                var newClientWindow = new NewClientWindow(_currentUser);
                newClientWindow.Owner = this;

                if (newClientWindow.ShowDialog() == true)
                {
                    // Si se creó exitosamente, actualizar la lista de clientes
                    var createdClient = newClientWindow.CreatedClient;
                    var createdContact = newClientWindow.CreatedContact;

                    if (createdClient != null)
                    {
                        // Recargar solo la lista de clientes (no todo)
                        _clients = await _supabaseService.GetClients();

                        if (_clients != null && _clients.Count > 0)
                        {
                            // Actualizar el ComboBox con los nuevos datos
                            ClientComboBox.ItemsSource = null; // Limpiar primero
                            ClientComboBox.ItemsSource = _clients;
                            ClientComboBox.DisplayMemberPath = "Name";
                            ClientComboBox.SelectedValuePath = "Id";

                            // Seleccionar el cliente recién creado
                            ClientComboBox.SelectedValue = createdClient.Id;

                            System.Diagnostics.Debug.WriteLine($"✅ Cliente '{createdClient.Name}' seleccionado automáticamente");

                            // El evento SelectionChanged del ComboBox cargará automáticamente los contactos
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el formulario de nuevo cliente:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"❌ Error: {ex}");
            }
        }

    }
}