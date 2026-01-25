using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class UserManagementWindow : Window
    {
        private readonly UserSession _currentUser;
        private readonly SupabaseService _supabaseService;
        private List<UserDb> _allUsers = new();
        private List<UserViewModel> _filteredUsers = new();
        private DispatcherTimer _toastTimer;

        // Colores para los roles
        private static readonly Dictionary<string, (string bg, string fg)> RoleColors = new()
        {
            ["direccion"] = ("#FEF3C7", "#D97706"),
            ["administracion"] = ("#DBEAFE", "#2563EB"),
            ["proyectos"] = ("#D1FAE5", "#059669"),
            ["coordinacion"] = ("#E0E7FF", "#4F46E5"),
            ["ventas"] = ("#FCE7F3", "#DB2777")
        };

        public UserManagementWindow(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _toastTimer.Tick += (s, e) => { ToastNotification.Visibility = Visibility.Collapsed; _toastTimer.Stop(); };
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUsers();
        }

        private async System.Threading.Tasks.Task LoadUsers()
        {
            try
            {
                StatusText.Text = "Cargando usuarios...";
                _allUsers = await _supabaseService.GetAllUsers();
                ApplyFilters();
                UpdateBadges();
                StatusText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                ShowToast("Error cargando usuarios", false);
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void ApplyFilters()
        {
            // Verificar que los controles estén inicializados
            if (SearchBox == null || RoleFilterCombo == null || ShowInactiveCheckbox == null || UsersItemsControl == null)
                return;

            var filtered = _allUsers.AsEnumerable();

            // Filtro de búsqueda
            string search = SearchBox.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(u =>
                    (u.Username?.ToLower().Contains(search) == true) ||
                    (u.FullName?.ToLower().Contains(search) == true) ||
                    (u.Email?.ToLower().Contains(search) == true));
            }

            // Filtro por rol
            if (RoleFilterCombo.SelectedItem is ComboBoxItem roleItem && roleItem.Tag != null)
            {
                string role = roleItem.Tag.ToString();
                filtered = filtered.Where(u => u.Role == role);
            }

            // Filtro de activos/inactivos
            if (ShowInactiveCheckbox.IsChecked != true)
            {
                filtered = filtered.Where(u => u.IsActive);
            }

            _filteredUsers = filtered.Select(u => new UserViewModel(u)).ToList();
            UsersItemsControl.ItemsSource = _filteredUsers;

            EmptyStatePanel.Visibility = _filteredUsers.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateBadges()
        {
            int total = _allUsers.Count;
            int active = _allUsers.Count(u => u.IsActive);
            int inactive = total - active;

            UserCountBadge.Text = $"{total} usuario{(total != 1 ? "s" : "")}";
            ActiveCountBadge.Text = $"{active} activo{(active != 1 ? "s" : "")}";
            InactiveCountBadge.Text = $"{inactive} inactivo{(inactive != 1 ? "s" : "")}";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void RoleFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void ShowInactive_Changed(object sender, RoutedEventArgs e) => ApplyFilters();

        private async void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var result = await ShowUserDialog(null);
            if (result != null)
            {
                await LoadUsers();
                ShowToast($"Usuario '{result.Username}' creado", true);
            }
        }

        private async void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is UserViewModel viewModel)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == viewModel.Id);
                if (user != null)
                {
                    var result = await ShowUserDialog(user);
                    if (result != null)
                    {
                        await LoadUsers();
                        ShowToast($"Usuario '{result.Username}' actualizado", true);
                    }
                }
            }
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is UserViewModel viewModel)
            {
                var result = ShowChangePasswordDialog(viewModel.Id, viewModel.Username);
                if (result)
                {
                    ShowToast($"Contraseña de '{viewModel.Username}' actualizada", true);
                }
            }
        }

        private async void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is UserViewModel viewModel)
            {
                bool wasActive = viewModel.IsActive;
                bool success = wasActive
                    ? await _supabaseService.DeactivateUser(viewModel.Id)
                    : await _supabaseService.ReactivateUser(viewModel.Id);

                if (success)
                {
                    await LoadUsers();
                    ShowToast($"Usuario '{viewModel.Username}' {(wasActive ? "desactivado" : "reactivado")}", true);
                }
                else
                {
                    ShowToast("Error al cambiar estado", false);
                }
            }
        }

        private async System.Threading.Tasks.Task<UserDb> ShowUserDialog(UserDb existingUser)
        {
            bool isEdit = existingUser != null;

            var dialog = new Window
            {
                Title = isEdit ? "Editar Usuario" : "Nuevo Usuario",
                Width = 650,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            // Container con sombra y bordes redondeados
            var container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(20),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 30,
                    Direction = 270,
                    ShadowDepth = 8,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };

            var mainPanel = new StackPanel { Margin = new Thickness(0) };

            // Header con gradiente
            var header = new Border
            {
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Padding = new Thickness(28, 24, 28, 20)
            };
            header.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#6366F1"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#8B5CF6"), 1)
                }
            };

            var headerContent = new Grid();
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new StackPanel();
            headerText.Children.Add(new TextBlock
            {
                Text = isEdit ? "Editar Usuario" : "Crear Nuevo Usuario",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            headerText.Children.Add(new TextBlock
            {
                Text = isEdit ? "Modifica los datos del usuario" : "Completa el formulario para registrar un nuevo usuario",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E7FF")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(headerText, 0);
            headerContent.Children.Add(headerText);

            // Botón cerrar
            var closeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                FontSize = 16,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top
            };
            closeBtn.Click += (s, ev) => dialog.DialogResult = false;
            Grid.SetColumn(closeBtn, 1);
            headerContent.Children.Add(closeBtn);

            header.Child = headerContent;
            mainPanel.Children.Add(header);

            // Contenido del formulario
            var formContent = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };

            // Fila 1: Usuario y Nombre completo
            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var (usernameContainer, usernameBox) = CreateModernFormField("👤", "Usuario", existingUser?.Username ?? "", "Ej: jgarcia");
            Grid.SetColumn(usernameContainer, 0);
            row1.Children.Add(usernameContainer);

            var (fullNameContainer, fullNameBox) = CreateModernFormField("📝", "Nombre completo", existingUser?.FullName ?? "", "Ej: Juan García López");
            Grid.SetColumn(fullNameContainer, 2);
            row1.Children.Add(fullNameContainer);

            formContent.Children.Add(row1);

            // Fila 2: Email y Rol
            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var (emailContainer, emailBox) = CreateModernFormField("📧", "Correo electrónico", existingUser?.Email ?? "", "Ej: usuario@empresa.com");
            Grid.SetColumn(emailContainer, 0);
            row2.Children.Add(emailContainer);

            // Rol con estilo moderno
            var roleContainer = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White
            };
            var roleStack = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };
            roleStack.Children.Add(new TextBlock
            {
                Text = "🎭  Rol del usuario",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var roleCombo = new ComboBox
            {
                FontSize = 16,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                Height = 28
            };
            roleCombo.Items.Add(new ComboBoxItem { Content = "🏢  Dirección", Tag = "direccion" });
            roleCombo.Items.Add(new ComboBoxItem { Content = "📊  Administración", Tag = "administracion" });
            roleCombo.Items.Add(new ComboBoxItem { Content = "🔧  Proyectos", Tag = "proyectos" });
            roleCombo.Items.Add(new ComboBoxItem { Content = "📋  Coordinación", Tag = "coordinacion" });
            roleCombo.Items.Add(new ComboBoxItem { Content = "💼  Ventas", Tag = "ventas" });

            if (isEdit && !string.IsNullOrEmpty(existingUser.Role))
            {
                for (int i = 0; i < roleCombo.Items.Count; i++)
                {
                    if ((roleCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == existingUser.Role)
                    {
                        roleCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                roleCombo.SelectedIndex = 4; // Ventas por defecto
            }
            roleStack.Children.Add(roleCombo);
            roleContainer.Child = roleStack;
            Grid.SetColumn(roleContainer, 2);
            row2.Children.Add(roleContainer);

            formContent.Children.Add(row2);

            // Contraseñas (solo para nuevo usuario)
            PasswordBox passwordBox = null;
            PasswordBox confirmPasswordBox = null;
            if (!isEdit)
            {
                // Separador visual
                formContent.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                    Margin = new Thickness(0, 12, 0, 24)
                });

                formContent.Children.Add(new TextBlock
                {
                    Text = "🔐 Seguridad",
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                    Margin = new Thickness(0, 0, 0, 18)
                });

                var row3 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var (passwordContainer, passwordBoxField) = CreateModernPasswordField("🔑", "Contraseña", "Mínimo 6 caracteres");
                passwordBox = passwordBoxField;
                Grid.SetColumn(passwordContainer, 0);
                row3.Children.Add(passwordContainer);

                var (confirmContainer, confirmBoxField) = CreateModernPasswordField("🔑", "Confirmar contraseña", "Repite la contraseña");
                confirmPasswordBox = confirmBoxField;
                Grid.SetColumn(confirmContainer, 2);
                row3.Children.Add(confirmContainer);

                formContent.Children.Add(row3);
            }

            // Toggle de estado activo (solo para edición)
            CheckBox activeCheckbox = null;
            if (isEdit)
            {
                var statusContainer = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 14, 16, 14),
                    Margin = new Thickness(0, 12, 0, 0)
                };
                var statusGrid = new Grid();
                statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var statusTextStack = new StackPanel();
                statusTextStack.Children.Add(new TextBlock
                {
                    Text = "Estado del usuario",
                    FontSize = 16,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
                });
                statusTextStack.Children.Add(new TextBlock
                {
                    Text = "Los usuarios inactivos no pueden iniciar sesión",
                    FontSize = 14,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                    Margin = new Thickness(0, 4, 0, 0)
                });
                Grid.SetColumn(statusTextStack, 0);
                statusGrid.Children.Add(statusTextStack);

                activeCheckbox = new CheckBox
                {
                    Content = "Activo",
                    IsChecked = existingUser.IsActive,
                    FontSize = 16,
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(activeCheckbox, 1);
                statusGrid.Children.Add(activeCheckbox);

                statusContainer.Child = statusGrid;
                formContent.Children.Add(statusContainer);
            }

            // Mensaje de error
            var errorText = new TextBlock
            {
                Text = "",
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")),
                Margin = new Thickness(0, 18, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            formContent.Children.Add(errorText);

            // Botones
            var buttonsPanel = new Grid { Margin = new Thickness(0, 24, 0, 0) };
            buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                Width = 120,
                Height = 46,
                FontSize = 15,
                Background = Brushes.White,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelBtn.Click += (s, ev) => dialog.DialogResult = false;
            Grid.SetColumn(cancelBtn, 1);
            buttonsPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = isEdit ? "💾  Guardar Cambios" : "✓  Crear Usuario",
                Height = 46,
                MinWidth = 180,
                FontSize = 15,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(12, 0, 0, 0)
            };
            okBtn.Click += async (s, ev) =>
            {
                // Validaciones
                string username = usernameBox.Text?.Trim() ?? "";
                string fullName = fullNameBox.Text?.Trim() ?? "";
                string email = emailBox.Text?.Trim() ?? "";
                string role = (roleCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ventas";

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
                {
                    errorText.Text = "⚠️ Todos los campos son requeridos";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                if (!email.Contains("@") || !email.Contains("."))
                {
                    errorText.Text = "⚠️ El correo electrónico no tiene un formato válido";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                if (!isEdit)
                {
                    string password = passwordBox.Password ?? "";
                    string confirmPassword = confirmPasswordBox.Password ?? "";

                    if (string.IsNullOrEmpty(password))
                    {
                        errorText.Text = "⚠️ La contraseña es requerida";
                        errorText.Visibility = Visibility.Visible;
                        return;
                    }

                    if (password.Length < 6)
                    {
                        errorText.Text = "⚠️ La contraseña debe tener al menos 6 caracteres";
                        errorText.Visibility = Visibility.Visible;
                        return;
                    }

                    if (password != confirmPassword)
                    {
                        errorText.Text = "⚠️ Las contraseñas no coinciden";
                        errorText.Visibility = Visibility.Visible;
                        return;
                    }

                    // Verificar si usuario existe
                    if (await _supabaseService.UserExists(username))
                    {
                        errorText.Text = "⚠️ El nombre de usuario ya está registrado";
                        errorText.Visibility = Visibility.Visible;
                        return;
                    }
                }

                try
                {
                    okBtn.IsEnabled = false;
                    okBtn.Content = "⏳  Guardando...";
                    errorText.Visibility = Visibility.Collapsed;

                    if (isEdit)
                    {
                        existingUser.Username = username;
                        existingUser.FullName = fullName;
                        existingUser.Email = email;
                        existingUser.Role = role;
                        existingUser.IsActive = activeCheckbox.IsChecked == true;

                        var updated = await _supabaseService.UpdateUser(existingUser);
                        if (updated != null)
                        {
                            dialog.Tag = updated;
                            dialog.DialogResult = true;
                        }
                        else
                        {
                            errorText.Text = "❌ Error al actualizar el usuario";
                            errorText.Visibility = Visibility.Visible;
                            okBtn.IsEnabled = true;
                            okBtn.Content = "💾  Guardar Cambios";
                        }
                    }
                    else
                    {
                        var newUser = new UserDb
                        {
                            Username = username,
                            FullName = fullName,
                            Email = email,
                            Role = role,
                            IsActive = true // Nuevos usuarios siempre activos
                        };

                        var created = await _supabaseService.CreateUser(newUser, passwordBox.Password);
                        if (created != null)
                        {
                            dialog.Tag = created;
                            dialog.DialogResult = true;
                        }
                        else
                        {
                            errorText.Text = "❌ Error al crear el usuario";
                            errorText.Visibility = Visibility.Visible;
                            okBtn.IsEnabled = true;
                            okBtn.Content = "✓  Crear Usuario";
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorText.Text = $"❌ {ex.Message}";
                    errorText.Visibility = Visibility.Visible;
                    okBtn.IsEnabled = true;
                    okBtn.Content = isEdit ? "💾  Guardar Cambios" : "✓  Crear Usuario";
                }
            };
            Grid.SetColumn(okBtn, 2);
            buttonsPanel.Children.Add(okBtn);

            formContent.Children.Add(buttonsPanel);
            mainPanel.Children.Add(formContent);

            container.Child = mainPanel;
            dialog.Content = container;

            // Permitir arrastrar la ventana desde el header
            header.MouseLeftButtonDown += (s, ev) => { if (ev.ClickCount == 1) dialog.DragMove(); };

            dialog.Loaded += (s, ev) => usernameBox.Focus();

            if (dialog.ShowDialog() == true)
            {
                return dialog.Tag as UserDb;
            }
            return null;
        }

        private (Border Container, TextBox TextBox) CreateModernFormField(string icon, string label, string value, string placeholder, bool isPassword = false)
        {
            var container = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White
            };

            var stack = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };

            stack.Children.Add(new TextBlock
            {
                Text = $"{icon}  {label}",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var textBox = new TextBox
            {
                Text = value,
                FontSize = 16,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                Height = 28,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"))
            };

            if (isPassword)
            {
                textBox.FontFamily = new FontFamily("Consolas");
            }

            // Placeholder
            var placeholderText = new TextBlock
            {
                Text = placeholder,
                FontSize = 16,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = string.IsNullOrEmpty(value) ? Visibility.Visible : Visibility.Collapsed
            };

            textBox.TextChanged += (s, e) =>
            {
                placeholderText.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            };

            var inputGrid = new Grid();
            inputGrid.Children.Add(placeholderText);
            inputGrid.Children.Add(textBox);

            stack.Children.Add(inputGrid);
            container.Child = stack;

            // Focus effect
            textBox.GotFocus += (s, e) =>
            {
                container.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
                container.BorderThickness = new Thickness(2);
            };
            textBox.LostFocus += (s, e) =>
            {
                container.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                container.BorderThickness = new Thickness(1);
            };

            return (container, textBox);
        }

        private (Border Container, PasswordBox PasswordBox) CreateModernPasswordField(string icon, string label, string placeholder)
        {
            var container = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White
            };

            var stack = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };

            stack.Children.Add(new TextBlock
            {
                Text = $"{icon}  {label}",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var passwordBox = new PasswordBox
            {
                FontSize = 16,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                PasswordChar = '●',
                Height = 28
            };

            // Placeholder para PasswordBox (usando un TextBlock superpuesto)
            var placeholderText = new TextBlock
            {
                Text = placeholder,
                FontSize = 16,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center
            };

            passwordBox.PasswordChanged += (s, e) =>
            {
                placeholderText.Visibility = string.IsNullOrEmpty(passwordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            };

            var inputGrid = new Grid();
            inputGrid.Children.Add(placeholderText);
            inputGrid.Children.Add(passwordBox);

            stack.Children.Add(inputGrid);
            container.Child = stack;

            // Focus effect
            passwordBox.GotFocus += (s, e) =>
            {
                container.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
                container.BorderThickness = new Thickness(2);
            };
            passwordBox.LostFocus += (s, e) =>
            {
                container.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                container.BorderThickness = new Thickness(1);
            };

            return (container, passwordBox);
        }

        private bool ShowChangePasswordDialog(int userId, string username)
        {
            var dialog = new Window
            {
                Title = "Cambiar Contraseña",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            // Container con sombra
            var container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(20),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 30,
                    Direction = 270,
                    ShadowDepth = 8,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };

            var mainPanel = new StackPanel();

            // Header
            var header = new Border
            {
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Padding = new Thickness(28, 20, 28, 16)
            };
            header.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#6366F1"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#8B5CF6"), 1)
                }
            };

            var headerContent = new Grid();
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new StackPanel();
            headerText.Children.Add(new TextBlock
            {
                Text = "🔐 Cambiar Contraseña",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            headerText.Children.Add(new TextBlock
            {
                Text = $"Usuario: @{username}",
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E7FF")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(headerText, 0);
            headerContent.Children.Add(headerText);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                FontSize = 16,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top
            };
            closeBtn.Click += (s, ev) => dialog.DialogResult = false;
            Grid.SetColumn(closeBtn, 1);
            headerContent.Children.Add(closeBtn);

            header.Child = headerContent;
            mainPanel.Children.Add(header);

            // Form content
            var formContent = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };

            var (passwordContainer, passwordBox) = CreateModernPasswordField("🔑", "Nueva contraseña", "Mínimo 6 caracteres");
            passwordContainer.Margin = new Thickness(0, 0, 0, 16);
            formContent.Children.Add(passwordContainer);

            var (confirmContainer, confirmBox) = CreateModernPasswordField("🔑", "Confirmar contraseña", "Repite la contraseña");
            formContent.Children.Add(confirmContainer);

            var errorText = new TextBlock
            {
                Text = "",
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")),
                Margin = new Thickness(0, 16, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            formContent.Children.Add(errorText);

            var buttonsPanel = new Grid { Margin = new Thickness(0, 24, 0, 0) };
            buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                Width = 110,
                Height = 42,
                FontSize = 15,
                Background = Brushes.White,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelBtn.Click += (s, ev) => dialog.DialogResult = false;
            Grid.SetColumn(cancelBtn, 1);
            buttonsPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "🔑  Cambiar",
                Height = 42,
                MinWidth = 140,
                FontSize = 15,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(12, 0, 0, 0)
            };
            okBtn.Click += async (s, ev) =>
            {
                string password = passwordBox.Password ?? "";
                string confirm = confirmBox.Password ?? "";

                if (string.IsNullOrEmpty(password))
                {
                    errorText.Text = "⚠️ La contraseña es requerida";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                if (password.Length < 6)
                {
                    errorText.Text = "⚠️ Mínimo 6 caracteres";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                if (password != confirm)
                {
                    errorText.Text = "⚠️ Las contraseñas no coinciden";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                try
                {
                    okBtn.IsEnabled = false;
                    okBtn.Content = "⏳ Cambiando...";
                    errorText.Visibility = Visibility.Collapsed;

                    bool success = await _supabaseService.ChangePassword(userId, password);
                    if (success)
                    {
                        dialog.DialogResult = true;
                    }
                    else
                    {
                        errorText.Text = "❌ Error al cambiar contraseña";
                        errorText.Visibility = Visibility.Visible;
                        okBtn.IsEnabled = true;
                        okBtn.Content = "🔑  Cambiar";
                    }
                }
                catch (Exception ex)
                {
                    errorText.Text = $"❌ {ex.Message}";
                    errorText.Visibility = Visibility.Visible;
                    okBtn.IsEnabled = true;
                    okBtn.Content = "🔑  Cambiar";
                }
            };
            Grid.SetColumn(okBtn, 2);
            buttonsPanel.Children.Add(okBtn);

            formContent.Children.Add(buttonsPanel);
            mainPanel.Children.Add(formContent);

            container.Child = mainPanel;
            dialog.Content = container;

            // Permitir arrastrar desde el header
            header.MouseLeftButtonDown += (s, ev) => { if (ev.ClickCount == 1) dialog.DragMove(); };

            dialog.Loaded += (s, ev) => passwordBox.Focus();

            return dialog.ShowDialog() == true;
        }

        private void ShowToast(string message, bool isSuccess)
        {
            ToastIcon.Text = isSuccess ? "✓" : "✗";
            ToastMessage.Text = message;
            ToastNotification.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(isSuccess ? "#10B981" : "#EF4444"));
            ToastNotification.Visibility = Visibility.Visible;
            _toastTimer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>
    /// ViewModel para mostrar usuarios en la lista
    /// </summary>
    public class UserViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }

        public string Initials => GetInitials(FullName);
        public string RoleDisplay => GetRoleDisplay(Role);
        public string StatusDisplay => IsActive ? "ACTIVO" : "INACTIVO";
        public string LastLoginDisplay => LastLogin.HasValue
            ? $"Último acceso: {LastLogin.Value.ToLocalTime():dd/MM/yy HH:mm}"
            : "Sin accesos";

        public Brush AvatarBackground => GetAvatarBrush(Role);
        public Brush RoleBackground => GetRoleBrush(Role, true);
        public Brush RoleForeground => GetRoleBrush(Role, false);
        public Brush StatusBackground => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(IsActive ? "#D1FAE5" : "#FEE2E2"));
        public Brush StatusForeground => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(IsActive ? "#059669" : "#DC2626"));

        public string ToggleStatusIcon => IsActive ? "🚫" : "✅";
        public string ToggleStatusTooltip => IsActive ? "Desactivar usuario" : "Reactivar usuario";

        public UserViewModel(UserDb user)
        {
            Id = user.Id;
            Username = user.Username;
            FullName = user.FullName;
            Email = user.Email;
            Role = user.Role;
            IsActive = user.IsActive;
            LastLogin = user.LastLogin;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "??";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Substring(0, Math.Min(2, name.Length)).ToUpper();
        }

        private static string GetRoleDisplay(string role)
        {
            return role switch
            {
                "direccion" => "DIRECCIÓN",
                "administracion" => "ADMINISTRACIÓN",
                "proyectos" => "PROYECTOS",
                "coordinacion" => "COORDINACIÓN",
                "ventas" => "VENTAS",
                _ => role?.ToUpper() ?? "SIN ROL"
            };
        }

        private static Brush GetAvatarBrush(string role)
        {
            var colors = role switch
            {
                "direccion" => ("#D97706", "#B45309"),
                "administracion" => ("#2563EB", "#1D4ED8"),
                "proyectos" => ("#059669", "#047857"),
                "coordinacion" => ("#4F46E5", "#4338CA"),
                "ventas" => ("#DB2777", "#BE185D"),
                _ => ("#6B7280", "#4B5563")
            };

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(colors.Item1), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(colors.Item2), 1));
            return brush;
        }

        private static Brush GetRoleBrush(string role, bool isBackground)
        {
            var colors = role switch
            {
                "direccion" => ("#FEF3C7", "#D97706"),
                "administracion" => ("#DBEAFE", "#2563EB"),
                "proyectos" => ("#D1FAE5", "#059669"),
                "coordinacion" => ("#E0E7FF", "#4F46E5"),
                "ventas" => ("#FCE7F3", "#DB2777"),
                _ => ("#F3F4F6", "#6B7280")
            };

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isBackground ? colors.Item1 : colors.Item2));
        }
    }
}
