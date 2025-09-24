using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorEditDialog : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly VendorViewModel _vendor;
        private bool _isEditMode;
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
                HeaderSubtext.Text = "Modifique la información del vendedor";
                HeaderIcon.Text = "✏️";

                // Cargar datos del vendedor
                VendorNameTextBox.Text = _vendor.VendorName;
                EmailTextBox.Text = _vendor.Email;
                PhoneTextBox.Text = _vendor.Phone ?? "";
                UsernameTextBox.Text = _vendor.Username;
                IsActiveCheckBox.IsChecked = _vendor.IsActive;

                // Cargar tasa de comisión
                CommissionRateTextBox.Text = (_vendor.CommissionRate == 0 ? 10m : _vendor.CommissionRate).ToString("F2");


                // Configurar campos de contraseña para edición
                PasswordLabel.Text = "Nueva Contraseña (opcional)";
                ConfirmPasswordPanel.Visibility = Visibility.Collapsed;
                PasswordNote.Visibility = Visibility.Visible;

                // Mostrar información adicional
                InfoSection.Visibility = Visibility.Visible;
                VendorIdText.Text = $"ID Vendedor: {_vendor.Id}";
                CreatedAtText.Text = $"Usuario ID: {(_vendor.UserId ?? 0)}";

                // Si no tiene usuario, permitir crear uno
                if (!_vendor.UserId.HasValue || _vendor.UserId == 0)
                {
                    PasswordLabel.Text = "Contraseña *";
                    PasswordNote.Text = "⚠️ Este vendedor no tiene usuario. Se creará uno nuevo.";
                    PasswordNote.Foreground = new SolidColorBrush(Color.FromRgb(232, 65, 24));
                    ConfirmPasswordPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                Title = "Nuevo Vendedor";
                HeaderText.Text = "NUEVO VENDEDOR";
                HeaderSubtext.Text = "Complete la información del vendedor";
                HeaderIcon.Text = "➕";
                IsActiveCheckBox.IsChecked = true;
                InfoSection.Visibility = Visibility.Collapsed;
                ConfirmPasswordPanel.Visibility = Visibility.Visible;

                // Valor por defecto para comisión
                CommissionRateTextBox.Text = "10.00";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validar campos obligatorios
            if (!ValidateForm())
                return;

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Guardando...";

                var supabaseClient = _supabaseService.GetClient();

                // Obtener el valor de comisión
                decimal commissionRate = 10;
                if (decimal.TryParse(CommissionRateTextBox.Text, out decimal parsedRate))
                {
                    commissionRate = parsedRate;
                }

                if (_isEditMode)
                {
                    // Actualizar vendedor existente
                    var vendorToUpdate = await supabaseClient
                        .From<VendorTableDb>()
                        .Where(v => v.Id == _vendor.Id)
                        .Single();

                    if (vendorToUpdate != null)
                    {
                        vendorToUpdate.VendorName = VendorNameTextBox.Text.Trim();
                        vendorToUpdate.Email = EmailTextBox.Text.Trim();
                        vendorToUpdate.Phone = PhoneTextBox.Text.Trim();
                        vendorToUpdate.IsActive = IsActiveCheckBox.IsChecked ?? true;
                        vendorToUpdate.CommissionRate = commissionRate;

                        await supabaseClient
                            .From<VendorTableDb>()
                            .Where(v => v.Id == _vendor.Id)
                            .Set(v => v.VendorName, vendorToUpdate.VendorName)
                            .Set(v => v.Email, vendorToUpdate.Email)
                            .Set(v => v.Phone, vendorToUpdate.Phone)
                            .Set(v => v.IsActive, vendorToUpdate.IsActive)
                            .Set(v => v.CommissionRate, vendorToUpdate.CommissionRate)
                            .Update();

                        // Si hay usuario asociado, actualizarlo también
                        if (_vendor.UserId.HasValue && _vendor.UserId > 0)
                        {
                            var userToUpdate = await supabaseClient
                                .From<UserDb>()
                                .Where(u => u.Id == _vendor.UserId.Value)
                                .Single();

                            if (userToUpdate != null)
                            {
                                userToUpdate.Username = UsernameTextBox.Text.Trim();
                                userToUpdate.Email = EmailTextBox.Text.Trim();
                                userToUpdate.FullName = VendorNameTextBox.Text.Trim();
                                userToUpdate.IsActive = IsActiveCheckBox.IsChecked ?? true;

                                // Si se proporcionó nueva contraseña
                                if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
                                {
                                    userToUpdate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordBox.Password);
                                }

                                await supabaseClient
                                    .From<UserDb>()
                                    .Where(u => u.Id == _vendor.UserId.Value)
                                    .Update(userToUpdate);
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
                        {
                            // Crear nuevo usuario si no existe y se proporcionó contraseña
                            var newUser = new UserDb
                            {
                                Username = UsernameTextBox.Text.Trim(),
                                Email = EmailTextBox.Text.Trim(),
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordBox.Password),
                                FullName = VendorNameTextBox.Text.Trim(),
                                Role = "salesperson",
                                IsActive = IsActiveCheckBox.IsChecked ?? true
                            };

                            var createdUser = await supabaseClient
                                .From<UserDb>()
                                .Insert(newUser);

                            if (createdUser?.Models?.Count > 0)
                            {
                                var userId = createdUser.Models.First().Id;
                                await supabaseClient
                                    .From<VendorTableDb>()
                                    .Where(v => v.Id == _vendor.Id)
                                    .Set(v => v.UserId, userId)
                                    .Update();
                            }
                        }

                        MessageBox.Show("Vendedor actualizado correctamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Crear nuevo vendedor
                    var newVendor = new VendorTableDb
                    {
                        VendorName = VendorNameTextBox.Text.Trim(),
                        Email = EmailTextBox.Text.Trim(),
                        Phone = PhoneTextBox.Text.Trim(),
                        IsActive = IsActiveCheckBox.IsChecked ?? true,
                        CommissionRate = commissionRate
                    };

                    // Primero crear el usuario si se proporcionó contraseña
                    if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
                    {
                        var newUser = new UserDb
                        {
                            Username = UsernameTextBox.Text.Trim(),
                            Email = EmailTextBox.Text.Trim(),
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordBox.Password),
                            FullName = VendorNameTextBox.Text.Trim(),
                            Role = "salesperson",
                            IsActive = IsActiveCheckBox.IsChecked ?? true
                        };

                        var createdUser = await supabaseClient
                            .From<UserDb>()
                            .Insert(newUser);

                        if (createdUser?.Models?.Count > 0)
                        {
                            newVendor.UserId = createdUser.Models.First().Id;
                        }
                    }

                    // Crear el vendedor
                    var created = await supabaseClient
                        .From<VendorTableDb>()
                        .Insert(newVendor);

                    if (created?.Models?.Count > 0)
                    {
                        MessageBox.Show("Vendedor creado correctamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        throw new Exception("No se pudo crear el vendedor");
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "💾 GUARDAR";
            }
        }

        private bool ValidateForm()
        {
            // Limpiar mensaje de validación
            ValidationText.Text = "";

            // Validar nombre del vendedor
            if (string.IsNullOrWhiteSpace(VendorNameTextBox.Text))
            {
                ValidationText.Text = "El nombre del vendedor es obligatorio";
                VendorNameTextBox.Focus();
                return false;
            }

            // Validar email
            if (!string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(EmailTextBox.Text))
                {
                    ValidationText.Text = "El formato del email no es válido";
                    EmailTextBox.Focus();
                    return false;
                }
            }

            // Validar nombre de usuario
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ValidationText.Text = "El nombre de usuario es obligatorio";
                UsernameTextBox.Focus();
                return false;
            }

            // Validar comisión
            if (!decimal.TryParse(CommissionRateTextBox.Text, out decimal rate) || rate < 0 || rate > 100)
            {
                ValidationText.Text = "La comisión debe ser un número entre 0 y 100";
                CommissionRateTextBox.Focus();
                return false;
            }

            // Validar contraseña para nuevo vendedor
            if (!_isEditMode)
            {
                if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    ValidationText.Text = "La contraseña es obligatoria para nuevos vendedores";
                    PasswordBox.Focus();
                    return false;
                }

                if (PasswordBox.Password != ConfirmPasswordBox.Password)
                {
                    ValidationText.Text = "Las contraseñas no coinciden";
                    ConfirmPasswordBox.Focus();
                    return false;
                }

                if (PasswordBox.Password.Length < 6)
                {
                    ValidationText.Text = "La contraseña debe tener al menos 6 caracteres";
                    PasswordBox.Focus();
                    return false;
                }
            }

            // Validar contraseña para vendedor sin usuario
            if (_isEditMode && (!_vendor.UserId.HasValue || _vendor.UserId == 0))
            {
                if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    ValidationText.Text = "Debe proporcionar una contraseña para crear el usuario";
                    PasswordBox.Focus();
                    return false;
                }

                if (PasswordBox.Password != ConfirmPasswordBox.Password)
                {
                    ValidationText.Text = "Las contraseñas no coinciden";
                    ConfirmPasswordBox.Focus();
                    return false;
                }
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Si hay cambios sin guardar, preguntar
            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show(
                    "¿Está seguro de cerrar sin guardar los cambios?",
                    "Confirmar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            DialogResult = false;
            Close();
        }

        private bool HasUnsavedChanges()
        {
            if (!_isEditMode)
            {
                // Para nuevo vendedor, verificar si se ha escrito algo
                return !string.IsNullOrWhiteSpace(VendorNameTextBox.Text) ||
                       !string.IsNullOrWhiteSpace(EmailTextBox.Text) ||
                       !string.IsNullOrWhiteSpace(PhoneTextBox.Text) ||
                       !string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                       !string.IsNullOrWhiteSpace(PasswordBox.Password);
            }

            // Para edición, comparar con valores originales
            return VendorNameTextBox.Text != _vendor.VendorName ||
                   EmailTextBox.Text != _vendor.Email ||
                   (PhoneTextBox.Text ?? "") != (_vendor.Phone ?? "") ||
                   UsernameTextBox.Text != _vendor.Username ||
                   !string.IsNullOrWhiteSpace(PasswordBox.Password);
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Validar que el username no esté vacío
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ValidationText.Text = "El nombre de usuario es obligatorio";
            }
            else
            {
                ValidationText.Text = "";
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Sincronizar con el campo de texto si está visible
            if (_isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
            }
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Sincronizar con el PasswordBox si está visible
            if (_isPasswordVisible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
            }
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                // Ocultar contraseña
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "👁️";
                _isPasswordVisible = false;
            }
            else
            {
                // Mostrar contraseña
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "👁‍🗨";
                _isPasswordVisible = true;
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            // Permitir solo números y un punto decimal
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(fullText);
        }
    }
}