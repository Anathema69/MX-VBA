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
                    if (selectedContact.Id > 0)
                    {
                        contactId = selectedContact.Id;
                    }
                    else
                    {
                        contactId = null;
                    }
                }

                // IMPORTANTE: Usar _subtotalValue aquí también
                System.Diagnostics.Debug.WriteLine($"💰 Creando orden con subtotal: {_subtotalValue:C}");

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
                    SaleSubtotal = _subtotalValue,  // Usar _subtotalValue
                    SaleTotal = _subtotalValue * 1.16m,  // Calcular con IVA
                    Expense = 0,
                    OrderStatus = 0,
                    ProgressPercentage = 0,
                    OrderPercentage = 0,
                    CommissionRate = 0 // Valor por defecto
                };

                // Log para depuración
                System.Diagnostics.Debug.WriteLine($"📋 Creando orden:");
                System.Diagnostics.Debug.WriteLine($"   PO: {newOrder.Po}");
                System.Diagnostics.Debug.WriteLine($"   Cliente ID: {newOrder.ClientId}");
                System.Diagnostics.Debug.WriteLine($"   Contacto ID: {newOrder.ContactId ?? 0}");
                System.Diagnostics.Debug.WriteLine($"   Vendedor ID: {newOrder.SalesmanId}");
                System.Diagnostics.Debug.WriteLine($"   Subtotal: ${newOrder.SaleSubtotal:N2}");
                System.Diagnostics.Debug.WriteLine($"   Total: ${newOrder.SaleTotal:N2}");

                int userId = 0;
                if (_currentUser != null)
                {
                    userId = _currentUser.Id;
                    System.Diagnostics.Debug.WriteLine($"👤 Usuario creando la orden: {_currentUser.FullName} (ID: {userId})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ No hay usuario en sesión, usando ID por defecto");
                    userId = 1;
                }

                // Llamar al servicio para crear la orden
                var createdOrder = await _supabaseService.CreateOrder(newOrder, userId);

                if (createdOrder != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Orden creada con ID: {createdOrder.Id}");

                    MessageBox.Show(
                        $"✅ Orden {newOrder.Po} guardada exitosamente.\n\n" +
                        $"Cliente: {(ClientComboBox.SelectedItem as ClientDb)?.Name}\n" +
                        $"Vendedor: {(VendorComboBox.SelectedItem as VendorDb)?.VendorName}\n" +
                        $"Subtotal: {_subtotalValue:C}\n" +
                        $"Total: {(_subtotalValue * 1.16m):C}\n" +
                        $"Creada por: {_currentUser?.FullName ?? "Sistema"}",
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
        private bool ValidateForm()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(OrderNumberTextBox.Text))
                errors.Add("• Orden de Compra es obligatorio");

            if (!OrderDatePicker.SelectedDate.HasValue)
                errors.Add("• Fecha O.C. es obligatoria");

            if (ClientComboBox.SelectedItem == null)
                errors.Add("• Cliente es obligatorio");

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
                errors.Add("• Descripción es obligatoria");

            if (VendorComboBox.SelectedItem == null)
                errors.Add("• Vendedor es obligatorio");

            // CORRECCIÓN: Usar _subtotalValue en lugar de parsear el texto
            System.Diagnostics.Debug.WriteLine($"🔍 Validando subtotal: _subtotalValue = {_subtotalValue}");
            System.Diagnostics.Debug.WriteLine($"🔍 Texto en SubtotalTextBox = '{SubtotalTextBox.Text}'");

            if (_subtotalValue <= 0)
            {
                errors.Add($"• Subtotal debe ser mayor a 0 (valor actual: {_subtotalValue:C})");
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

            System.Diagnostics.Debug.WriteLine($"✅ Validación exitosa con subtotal: {_subtotalValue:C}");
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

           

            this.DialogResult = false;
            this.Close();
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