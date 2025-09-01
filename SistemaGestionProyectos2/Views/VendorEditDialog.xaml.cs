using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorEditDialog : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly VendorViewModel _vendor;
        private bool _isEditMode;
        private string _tempPassword = "";
        private bool _isPasswordVisible = false;

        public VendorEditDialog(VendorViewModel vendor)
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _vendor = vendor;
            _isEditMode = vendor != null;

            InitializeForm();
        }

        private void InitializeForm()
        {
            if (_isEditMode)
            {
                Title = "Editar Vendedor";
                HeaderText.Text = "EDITAR VENDEDOR";

                // Cargar datos del vendedor
                VendorNameTextBox.Text = _vendor.VendorName;
                EmailTextBox.Text = _vendor.Email;
                PhoneTextBox.Text = _vendor.Phone;
                UsernameTextBox.Text = _vendor.Username;
                IsActiveCheckBox.IsChecked = _vendor.IsActive;

                // Configurar campos de contraseña para edición
                PasswordLabel.Text = "Nueva Contraseña (opcional)";
                ConfirmPasswordPanel.Visibility = Visibility.Collapsed;
                PasswordNote.Visibility = Visibility.Visible;

                // Mostrar información adicional
                InfoSection.Visibility = Visibility.Visible;
                VendorIdText.Text = $"ID Vendedor: {_vendor.Id}";
                CreatedAtText.Text = $"Usuario ID: {_vendor.UserId ?? 0}";

                // Si no tiene usuario, permitir crear uno
                if (!_vendor.UserId.HasValue || _vendor.UserId == 0)
                {
                    PasswordLabel.Text = "Contraseña *";
                    PasswordNote.Text = "Este vendedor no tiene usuario. Se creará uno nuevo.";
                    PasswordNote.Foreground = System.Windows.Media.Brushes.Blue;
                    ConfirmPasswordPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                Title = "Nuevo Vendedor";
                HeaderText.Text = "NUEVO VENDEDOR";
            }
        }

        private bool ValidateForm()
        {
            ValidationText.Text = "";

            // Validar nombre
            if (string.IsNullOrWhiteSpace(VendorNameTextBox.Text))
            {
                ValidationText.Text = "El nombre es obligatorio";
                return false;
            }

            // Validar email
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                ValidationText.Text = "El email es obligatorio";
                return false;
            }

            if (!IsValidEmail(EmailTextBox.Text))
            {
                ValidationText.Text = "El email no es válido";
                return false;
            }

            // Validar usuario
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ValidationText.Text = "El nombre de usuario es obligatorio";
                return false;
            }

            // Validar contraseña para nuevo vendedor o vendedor sin usuario
            if (!_isEditMode || !_vendor.UserId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(_tempPassword))
                {
                    ValidationText.Text = "La contraseña es obligatoria";
                    return false;
                }

                if (_tempPassword.Length < 6)
                {
                    ValidationText.Text = "La contraseña debe tener al menos 6 caracteres";
                    return false;
                }

                if (ConfirmPasswordPanel.Visibility == Visibility.Visible &&
                    _tempPassword != ConfirmPasswordBox.Password)
                {
                    ValidationText.Text = "Las contraseñas no coinciden";
                    return false;
                }
            }

            return true;
        }

        private bool IsValidEmail(string email)
        {
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            return regex.IsMatch(email);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "GUARDANDO...";

                var supabaseClient = _supabaseService.GetClient();

                if (_isEditMode)
                {
                    await UpdateVendor();
                }
                else
                {
                    await CreateVendor();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SaveButton.IsEnabled = true;
                SaveButton.Content = "💾 GUARDAR";
            }
        }

        private async Task CreateVendor()
        {
            var supabaseClient = _supabaseService.GetClient();

            // Verificar si el username ya existe
            try
            {
                var existingUser = await supabaseClient
                    .From<UserDb>()
                    .Where(u => u.Username == UsernameTextBox.Text.Trim())
                    .Single();

                if (existingUser != null)
                {
                    ValidationText.Text = "El nombre de usuario ya existe";
                    SaveButton.IsEnabled = true;
                    SaveButton.Content = "💾 GUARDAR";
                    return;
                }
            }
            catch
            {
                // No existe, continuar
            }

            // Verificar si el email ya existe
            try
            {
                var existingEmail = await supabaseClient
                    .From<UserDb>()
                    .Where(u => u.Email == EmailTextBox.Text.Trim())
                    .Single();

                if (existingEmail != null)
                {
                    ValidationText.Text = "El email ya está registrado";
                    SaveButton.IsEnabled = true;
                    SaveButton.Content = "💾 GUARDAR";
                    return;
                }
            }
            catch
            {
                // No existe, continuar
            }

            // Crear el usuario con password hasheado usando BCrypt
            var newUser = new UserDb
            {
                Username = UsernameTextBox.Text.Trim(),
                Email = EmailTextBox.Text.Trim(),
                FullName = VendorNameTextBox.Text.Trim().ToUpper(),
                Role = "salesperson",
                IsActive = IsActiveCheckBox.IsChecked ?? true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_tempPassword)
            };

            var userResponse = await supabaseClient
                .From<UserDb>()
                .Insert(newUser);

            if (userResponse?.Models?.Count > 0)
            {
                var createdUser = userResponse.Models.First();

                // Crear el vendedor
                var newVendor = new VendorTableDb
                {
                    VendorName = VendorNameTextBox.Text.Trim().ToUpper(),
                    Email = EmailTextBox.Text.Trim(),
                    Phone = PhoneTextBox.Text?.Trim(),
                    UserId = createdUser.Id,
                    IsActive = IsActiveCheckBox.IsChecked ?? true
                };

                var vendorResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Insert(newVendor);

                if (vendorResponse?.Models?.Count > 0)
                {
                    MessageBox.Show(
                        $"Vendedor creado exitosamente.\n\nUsuario: {newUser.Username}\nContraseña: {_tempPassword}",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        // Método UpdateVendor - parte donde actualiza password
        private async Task UpdateVendor()
        {
            var supabaseClient = _supabaseService.GetClient();

            // Actualizar vendedor
            var vendorToUpdate = await supabaseClient
                .From<VendorTableDb>()
                .Where(v => v.Id == _vendor.Id)
                .Single();

            if (vendorToUpdate != null)
            {
                vendorToUpdate.VendorName = VendorNameTextBox.Text.Trim().ToUpper();
                vendorToUpdate.Email = EmailTextBox.Text.Trim();
                vendorToUpdate.Phone = PhoneTextBox.Text?.Trim();
                vendorToUpdate.IsActive = IsActiveCheckBox.IsChecked ?? true;

                await supabaseClient
                    .From<VendorTableDb>()
                    .Update(vendorToUpdate);
            }

            // Si tiene usuario y se cambió la contraseña
            if (_vendor.UserId.HasValue && !string.IsNullOrWhiteSpace(_tempPassword))
            {
                var userToUpdate = await supabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == _vendor.UserId.Value)
                    .Single();

                if (userToUpdate != null)
                {
                    userToUpdate.Username = UsernameTextBox.Text.Trim();
                    userToUpdate.Email = EmailTextBox.Text.Trim();
                    userToUpdate.FullName = VendorNameTextBox.Text.Trim().ToUpper();
                    userToUpdate.IsActive = IsActiveCheckBox.IsChecked ?? true;

                    // Actualizar password si se proporcionó
                    if (!string.IsNullOrWhiteSpace(_tempPassword))
                    {
                        userToUpdate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(_tempPassword);
                    }

                    await supabaseClient
                        .From<UserDb>()
                        .Update(userToUpdate);
                }
            }
        }

        


        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _tempPassword = PasswordBox.Password;
        }

        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Mostrar contraseña
                PasswordTextBox.Text = _tempPassword;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "🙈";
            }
            else
            {
                // Ocultar contraseña
                PasswordBox.Password = _tempPassword;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "👁️";
            }
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _tempPassword = PasswordTextBox.Text;
        }

        private async Task<bool> CheckUsernameExists(string username)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                var existingUser = await supabaseClient
                    .From<UserDb>()
                    .Where(u => u.Username == username.Trim())
                    .Single();

                return existingUser != null;
            }
            catch
            {
                return false;
            }
        }

        // Agregar validación en tiempo real (opcional)
        private async void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                if (await CheckUsernameExists(UsernameTextBox.Text))
                {
                    ValidationText.Text = "Este nombre de usuario ya existe";
                    ValidationText.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    ValidationText.Text = "";
                }
            }
        }
    }
}