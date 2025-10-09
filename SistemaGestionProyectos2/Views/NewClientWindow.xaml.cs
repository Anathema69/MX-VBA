using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewClientWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private UserSession _currentUser;
        private bool _isCreating = false;
        private bool _clientAlreadyExists = false;

        // Propiedad pública para retornar el cliente creado
        public ClientDb CreatedClient { get; private set; }
        public ContactDb CreatedContact { get; private set; }

        public NewClientWindow()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;

            // Enfocar en el primer campo
            Loaded += (s, e) => ClientNameTextBox.Focus();
        }

        // Constructor con usuario
        public NewClientWindow(UserSession currentUser) : this()
        {
            _currentUser = currentUser;
        }

        private async void ClientNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Verificar si el cliente ya existe mientras escribe
            if (string.IsNullOrWhiteSpace(ClientNameTextBox.Text))
            {
                ClientNameWarning.Visibility = Visibility.Collapsed;
                _clientAlreadyExists = false;
                return;
            }

            string clientName = ClientNameTextBox.Text.Trim();

            // Solo verificar después de 3 caracteres
            if (clientName.Length >= 3)
            {
                try
                {
                    bool exists = await _supabaseService.ClientExists(clientName);
                    _clientAlreadyExists = exists;

                    if (exists)
                    {
                        ClientNameWarning.Text = "⚠ Este cliente ya existe en el sistema";
                        ClientNameWarning.Foreground = new SolidColorBrush(Colors.Red);
                        ClientNameWarning.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ClientNameWarning.Text = "✓ Cliente nuevo - disponible";
                        ClientNameWarning.Foreground = new SolidColorBrush(Colors.Green);
                        ClientNameWarning.Visibility = Visibility.Visible;
                    }
                }
                catch
                {
                    ClientNameWarning.Visibility = Visibility.Collapsed;
                    _clientAlreadyExists = false;
                }
            }
            else
            {
                ClientNameWarning.Visibility = Visibility.Collapsed;
                _clientAlreadyExists = false;
            }
        }

        private async void ClientNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Verificación final al salir del campo
            if (!string.IsNullOrWhiteSpace(ClientNameTextBox.Text))
            {
                string clientName = ClientNameTextBox.Text.Trim();
                bool exists = await _supabaseService.ClientExists(clientName);
                _clientAlreadyExists = exists;

                if (exists)
                {
                    ClientNameWarning.Text = "❌ Cliente duplicado - NO se puede crear";
                    ClientNameWarning.Foreground = new SolidColorBrush(Colors.Red);
                    ClientNameWarning.Visibility = Visibility.Visible;
                    SaveButton.IsEnabled = false;
                }
                else
                {
                    SaveButton.IsEnabled = true;
                }
            }
        }

        private void ContactEmailTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateEmail();
        }

        private bool ValidateEmail()
        {
            string email = ContactEmailTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                EmailWarning.Visibility = Visibility.Collapsed;
                return false;
            }

            // Patrón básico de validación de email
            string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            bool isValid = Regex.IsMatch(email, emailPattern);

            if (!isValid)
            {
                EmailWarning.Text = "⚠ Formato de email inválido";
                EmailWarning.Visibility = Visibility.Visible;
                return false;
            }
            else
            {
                EmailWarning.Visibility = Visibility.Collapsed;
                return true;
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Solo permitir números
            e.Handled = !IsTextNumeric(e.Text);
        }

        private bool IsTextNumeric(string text)
        {
            return text.All(char.IsDigit);
        }

        private bool ValidateForm()
        {
            var errors = new System.Collections.Generic.List<string>();

            // Validar Cliente
            if (string.IsNullOrWhiteSpace(ClientNameTextBox.Text))
                errors.Add("• La razón social/empresa es obligatoria");

            // Verificar duplicado
            if (_clientAlreadyExists)
                errors.Add("• El cliente ya existe en el sistema");

            // Validar días de crédito
            if (string.IsNullOrWhiteSpace(CreditDaysTextBox.Text))
            {
                errors.Add("• Los días de crédito son obligatorios");
            }
            else if (int.TryParse(CreditDaysTextBox.Text, out int creditDays))
            {
                if (creditDays < 0 || creditDays > 120)
                    errors.Add("• Los días de crédito deben estar entre 0 y 120");
            }
            else
            {
                errors.Add("• Los días de crédito deben ser un número válido");
            }

            // Validar Contacto
            if (string.IsNullOrWhiteSpace(ContactNameTextBox.Text))
                errors.Add("• El nombre del contacto es obligatorio");

            // Email y teléfono son opcionales, pero si se proporciona email debe ser válido
            if (!string.IsNullOrWhiteSpace(ContactEmailTextBox.Text) && !ValidateEmail())
            {
                errors.Add("• El email del contacto no tiene un formato válido");
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
            if (_isCreating) return;

            if (!ValidateForm()) return;

            try
            {
                _isCreating = true;
                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                // 1. Crear el cliente
                var newClient = new ClientDb
                {
                    Name = ClientNameTextBox.Text.Trim().ToUpper(), // Normalizar a mayúsculas
                    TaxId = TaxIdTextBox.Text?.Trim(),
                    Credit = int.Parse(CreditDaysTextBox.Text),
                    Phone = CompanyPhoneTextBox.Text?.Trim(),
                    Address1 = AddressTextBox.Text?.Trim(),
                    IsActive = true,
                    CreatedBy = _currentUser?.Id,
                    UpdatedBy = _currentUser?.Id
                };

                System.Diagnostics.Debug.WriteLine($"📋 Creando cliente: {newClient.Name}");

                // Verificar una vez más que no existe (por seguridad)
                bool exists = await _supabaseService.ClientExists(newClient.Name);
                if (exists)
                {
                    // Intentar obtener el cliente existente
                    var existingClient = await _supabaseService.GetClientByName(newClient.Name);

                    var result = MessageBox.Show(
                        $"El cliente '{newClient.Name}' ya existe en el sistema.\n\n" +
                        "¿Desea seleccionar el cliente existente?",
                        "Cliente Existente",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes && existingClient != null)
                    {
                        CreatedClient = existingClient;
                        // Obtener el primer contacto del cliente existente
                        var contacts = await _supabaseService.GetContactsByClient(existingClient.Id);
                        CreatedContact = contacts?.FirstOrDefault();

                        this.DialogResult = true;
                        this.Close();
                    }
                    return;
                }

                // Crear cliente
                int userId = _currentUser?.Id ?? 1;
                var createdClient = await _supabaseService.CreateClient(newClient, userId);

                if (createdClient == null)
                {
                    throw new Exception("No se pudo crear el cliente");
                }

                CreatedClient = createdClient;
                System.Diagnostics.Debug.WriteLine($"✅ Cliente creado con ID: {createdClient.Id}");

                // 2. Crear el contacto principal
                var newContact = new ContactDb
                {
                    ClientId = createdClient.Id,
                    ContactName = ContactNameTextBox.Text.Trim(),
                    Email = ContactEmailTextBox.Text.Trim().ToLower(),
                    Phone = ContactPhoneTextBox.Text?.Trim(),
                    Position = "Compras", // Valor por defecto fijo
                    IsPrimary = true, // El primer contacto siempre es principal
                    IsActive = true
                };

                System.Diagnostics.Debug.WriteLine($"📇 Creando contacto: {newContact.ContactName}");

                var createdContact = await _supabaseService.CreateContact(newContact);

                if (createdContact == null)
                {
                    throw new Exception("El cliente se creó pero hubo un error al crear el contacto");
                }

                CreatedContact = createdContact;
                System.Diagnostics.Debug.WriteLine($"✅ Contacto creado con ID: {createdContact.Id}");

                // Mostrar mensaje de éxito
                string message = $"✅ Cliente creado exitosamente\n\n" +
                                $"Empresa: {createdClient.Name}\n" +
                                $"Contacto: {createdContact.ContactName}\n" +
                                $"Email: {createdContact.Email}\n" +
                                $"Teléfono: {createdContact.Phone}";

                // Si está marcado crear otro
                if (CreateAndContinueCheckBox.IsChecked == true)
                {
                    MessageBox.Show(
                        message + "\n\nAhora puede crear otro cliente.",
                        "Cliente Creado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Limpiar formulario para crear otro
                    ClearForm();
                    ClientNameTextBox.Focus();
                }
                else
                {
                    MessageBox.Show(
                        message,
                        "Cliente Creado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Cerrar y retornar
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar:\n{ex.Message}\n\n" +
                    "Por favor verifique los datos e intente nuevamente.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"❌ Error completo: {ex}");
            }
            finally
            {
                _isCreating = false;
                SaveButton.IsEnabled = true;
                SaveButton.Content = "GUARDAR Y SELECCIONAR";
            }
        }

        private void ClearForm()
        {
            ClientNameTextBox.Clear();
            TaxIdTextBox.Clear();
            CreditDaysTextBox.Text = "30";
            CompanyPhoneTextBox.Clear();
            AddressTextBox.Clear();

            ContactNameTextBox.Clear();
            ContactEmailTextBox.Clear();
            ContactPhoneTextBox.Clear();

            ClientNameWarning.Visibility = Visibility.Collapsed;
            EmailWarning.Visibility = Visibility.Collapsed;
            _clientAlreadyExists = false;

            // Colapsar campos opcionales
            OptionalFieldsExpander.IsExpanded = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Verificar si hay cambios sin guardar
            bool hasChanges = !string.IsNullOrWhiteSpace(ClientNameTextBox.Text) ||
                            !string.IsNullOrWhiteSpace(ContactNameTextBox.Text) ||
                            !string.IsNullOrWhiteSpace(ContactEmailTextBox.Text);

            

            this.DialogResult = false;
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Permitir guardar con Ctrl+S
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (SaveButton.IsEnabled)
                {
                    SaveButton_Click(this, new RoutedEventArgs());
                }
            }

            base.OnKeyDown(e);
        }
    }
}