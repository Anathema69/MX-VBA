using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class ClientManagementWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<SimpleClientViewModel> _clients;
        private ObservableCollection<SimpleContactViewModel> _contacts;
        private SimpleClientViewModel _selectedClient;
        private SimpleContactViewModel _editingContact;
        private bool _isEditMode = false;
        private ObservableCollection<SimpleClientViewModel> _allClients; // Para el filtrado

        public ClientManagementWindow(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            _clients = new ObservableCollection<SimpleClientViewModel>();
            _contacts = new ObservableCollection<SimpleContactViewModel>();
            _allClients = new ObservableCollection<SimpleClientViewModel>();

            ClientsListBox.ItemsSource = _clients;
            ContactsDataGrid.ItemsSource = _contacts;

            // Cargar datos iniciales
            _ = LoadClientsAsync();
        }

        private async Task LoadClientsAsync()
        {
            try
            {
                StatusText.Text = "Cargando clientes...";
                _clients.Clear();
                _allClients.Clear();

                var clientsDb = await _supabaseService.GetActiveClients();
                var allContacts = await _supabaseService.GetAllContacts();

                foreach (var client in clientsDb.OrderBy(c => c.Name))
                {
                    var contactCount = await _supabaseService.CountActiveContactsByClientId(client.Id);

                    var clientVm = new SimpleClientViewModel
                    {
                        Id = client.Id,
                        Name = client.Name,
                        TaxId = client.TaxId ?? "Sin RFC",
                        Phone = client.Phone ?? "Sin teléfono",
                        Address = client.Address1 ?? "Sin dirección",
                        CreditDays = client.Credit, // Usar Credit del modelo
                        ContactCount = contactCount,
                        IsActive = client.IsActive
                    };

                    _allClients.Add(clientVm);
                    _clients.Add(clientVm);
                }

                TotalClientsText.Text = _clients.Count.ToString();
                ResultCountText.Text = $"{_clients.Count}/{_allClients.Count}";
                StatusText.Text = $"Se cargaron {_clients.Count} clientes";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error al cargar clientes";
                MessageBox.Show($"Error al cargar clientes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ClientsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClient = ClientsListBox.SelectedItem as SimpleClientViewModel;

            if (_selectedClient == null)
            {
                NoSelectionPanel.Visibility = Visibility.Visible;
                ClientDetailsPanel.Visibility = Visibility.Collapsed;
                ContactsSection.Visibility = Visibility.Collapsed;
                return;
            }

            // Mostrar paneles
            NoSelectionPanel.Visibility = Visibility.Collapsed;
            ClientDetailsPanel.Visibility = Visibility.Visible;
            ContactsSection.Visibility = Visibility.Visible;

            // Actualizar información del cliente
            ClientNameHeader.Text = _selectedClient.Name;
            ClientTaxIdText.Text = _selectedClient.TaxId;
            ClientPhoneText.Text = _selectedClient.Phone;
            ClientCreditDaysText.Text = $"{_selectedClient.CreditDays} días";
            ClientAddressText.Text = _selectedClient.Address;

            // Cargar contactos
            await LoadContactsForClient(_selectedClient.Id);
        }

        private async Task LoadContactsForClient(int clientId)
        {
            try
            {
                _contacts.Clear();
                var contactsDb = await _supabaseService.GetActiveContactsByClientId(clientId);

                foreach (var contact in contactsDb.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.ContactName))
                {
                    _contacts.Add(new SimpleContactViewModel
                    {
                        Id = contact.Id,
                        Name = contact.ContactName ?? "", // Usar ContactName del modelo
                        Email = contact.Email ?? "",
                        Phone = contact.Phone ?? "",
                        Position = contact.Position ?? "Sin cargo",
                        IsPrimary = contact.IsPrimary,
                        ClientId = contact.ClientId
                    });
                }

                ContactCountBadge.Text = _contacts.Count.ToString();
                TotalContactsText.Text = _contacts.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar contactos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();
            _clients.Clear();

            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? _allClients
                : _allClients.Where(c =>
                    c.Name.ToLower().Contains(searchText) ||
                    c.TaxId.ToLower().Contains(searchText) ||
                    c.Phone.ToLower().Contains(searchText));

            foreach (var client in filtered)
            {
                _clients.Add(client);
            }

            ResultCountText.Text = $"{_clients.Count}/{_allClients.Count}";
        }

        private void ContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ContactsDataGrid.SelectedItem != null;
            EditContactButton.IsEnabled = hasSelection;

            // Solo habilitar eliminar si hay más de un contacto
            DeleteContactButton.IsEnabled = hasSelection && _contacts.Count > 1;
        }

        private async void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
            var newClientWindow = new NewClientWindow(_currentUser);
            if (newClientWindow.ShowDialog() == true)
            {
                await LoadClientsAsync();
            }
        }

        private async  void EditClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            // Obtener el cliente actual de la BD
            var clients = await _supabaseService.GetActiveClients();
            var clientDb = clients.FirstOrDefault(c => c.Id == _selectedClient.Id);
            if (clientDb == null) return;

            // Crear un diálogo simple para editar
            var dialog = new Window
            {
                Title = "Editar Cliente",
                Width = 450,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ShowInTaskbar = false
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Campos del formulario
            var nameLabel = new TextBlock { Text = "Nombre:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(nameLabel, 0);
            grid.Children.Add(nameLabel);

            var nameTextBox = new TextBox { Text = clientDb.Name, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(nameTextBox, 1);
            grid.Children.Add(nameTextBox);

            var rfcLabel = new TextBlock { Text = "RFC:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(rfcLabel, 2);
            grid.Children.Add(rfcLabel);

            var rfcTextBox = new TextBox { Text = clientDb.TaxId ?? "", Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(rfcTextBox, 3);
            grid.Children.Add(rfcTextBox);

            var phoneLabel = new TextBlock { Text = "Teléfono:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(phoneLabel, 4);
            grid.Children.Add(phoneLabel);

            var phoneTextBox = new TextBox { Text = clientDb.Phone ?? "", Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(phoneTextBox, 5);
            grid.Children.Add(phoneTextBox);

            // Botones
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var saveButton = new Button
            {
                Content = "Guardar",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                Cursor = Cursors.Hand
            };

            var cancelButton = new Button
            {
                Content = "Cancelar",
                Width = 100,
                Height = 35,
                Background = Brushes.LightGray,
                Cursor = Cursors.Hand
            };

            saveButton.Click += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    MessageBox.Show("El nombre del cliente es obligatorio", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    clientDb.Name = nameTextBox.Text.Trim();
                    clientDb.TaxId = string.IsNullOrWhiteSpace(rfcTextBox.Text) ? null : rfcTextBox.Text.Trim();
                    clientDb.Phone = string.IsNullOrWhiteSpace(phoneTextBox.Text) ? null : phoneTextBox.Text.Trim();

                    await _supabaseService.UpdateClient(clientDb);

                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al actualizar cliente: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelButton.Click += (s, args) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 7);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            if (dialog.ShowDialog() == true)
            {
                await LoadClientsAsync();
                // Reseleccionar el cliente
                var updatedClient = _clients.FirstOrDefault(c => c.Id == _selectedClient.Id);
                if (updatedClient != null)
                {
                    ClientsListBox.SelectedItem = updatedClient;
                }
            }
        }

        private void NewContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            // Agregar un nuevo contacto vacío a la colección
            var newContact = new SimpleContactViewModel
            {
                Id = 0, // 0 indica que es nuevo
                Name = "Nuevo Contacto",
                Email = "",
                Phone = "",
                Position = "Sin cargo",
                IsPrimary = false,
                ClientId = _selectedClient.Id
            };

            _contacts.Add(newContact);

            // Seleccionar la nueva fila y entrar en modo edición
            ContactsDataGrid.SelectedItem = newContact;
            ContactsDataGrid.ScrollIntoView(newContact);

            // Entrar en modo edición en la primera celda editable
            ContactsDataGrid.CurrentCell = new DataGridCellInfo(newContact, ContactsDataGrid.Columns[1]);
            ContactsDataGrid.BeginEdit();
        }


        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            // Simplemente enfocar la celda para editar
            ContactsDataGrid.CurrentCell = new DataGridCellInfo(selectedContact, ContactsDataGrid.Columns[1]);
            ContactsDataGrid.BeginEdit();
        }

        // Manejar cuando se inicializa un nuevo item (si se usa CanUserAddRows)
        private void ContactsDataGrid_InitializingNewItem(object sender, InitializingNewItemEventArgs e)
        {
            var newContact = e.NewItem as SimpleContactViewModel;
            if (newContact != null && _selectedClient != null)
            {
                newContact.Id = 0; // Nuevo
                newContact.ClientId = _selectedClient.Id;
                newContact.Name = "Nuevo Contacto";
                newContact.Position = "Sin cargo";
                newContact.IsPrimary = false;
            }
        }

        private async void DeleteContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            // Verificar si es el último contacto
            if (_contacts.Count <= 1)
            {
                MessageBox.Show(
                    "No se puede eliminar el último contacto del cliente.\n" +
                    "Cada cliente debe tener al menos un contacto registrado.",
                    "Restricción de eliminación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el contacto '{selectedContact.Name}'?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _supabaseService.SoftDeleteContact(selectedContact.Id);
                    await LoadContactsForClient(_selectedClient.Id);
                    MessageBox.Show("Contacto eliminado exitosamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar contacto: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            // Verificar si tiene órdenes asociadas
            var orders = await _supabaseService.GetOrdersByClientId(_selectedClient.Id);
            if (orders.Any())
            {
                MessageBox.Show(
                    $"No se puede eliminar el cliente '{_selectedClient.Name}' porque tiene {orders.Count} orden(es) asociada(s).\n" +
                    "Debe cancelar o reasignar las órdenes antes de eliminar el cliente.",
                    "Cliente con órdenes activas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el cliente '{_selectedClient.Name}'?\n" +
                "Esta acción desactivará el cliente y todos sus contactos.",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _supabaseService.SoftDeleteClient(_selectedClient.Id);
                    if (success)
                    {
                        MessageBox.Show("Cliente eliminado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // Limpiar selección y recargar
                        _selectedClient = null;
                        NoSelectionPanel.Visibility = Visibility.Visible;
                        ClientDetailsPanel.Visibility = Visibility.Collapsed;
                        ContactsSection.Visibility = Visibility.Collapsed;

                        await LoadClientsAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar cliente: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ContactsDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var contact = e.Row.Item as SimpleContactViewModel;
                if (contact == null) return;

                // Pequeño delay para asegurar que los valores se actualicen
                await Task.Delay(100);

                try
                {
                    ContactDb contactDb;

                    if (contact.Id == 0) // Es nuevo
                    {
                        // Crear nuevo contacto
                        contactDb = new ContactDb
                        {
                            ClientId = contact.ClientId,
                            ContactName = contact.Name,
                            Email = contact.Email,
                            Phone = contact.Phone,
                            Position = contact.Position,
                            IsPrimary = contact.IsPrimary,
                            IsActive = true
                        };

                        var created = await _supabaseService.CreateContact(contactDb);
                        contact.Id = created.Id; // Actualizar el ID con el de la BD
                    }
                    else // Es edición
                    {
                        // Obtener el contacto de la BD
                        var contacts = await _supabaseService.GetActiveContactsByClientId(_selectedClient.Id);
                        contactDb = contacts.FirstOrDefault(c => c.Id == contact.Id);

                        if (contactDb != null)
                        {
                            contactDb.ContactName = contact.Name;
                            contactDb.Email = contact.Email;
                            contactDb.Phone = contact.Phone;
                            contactDb.Position = contact.Position;
                            contactDb.IsPrimary = contact.IsPrimary;

                            await _supabaseService.UpdateContact(contactDb);
                        }
                    }

                    // Si se marcó como principal, desmarcar los demás
                    if (contact.IsPrimary)
                    {
                        foreach (var c in _contacts.Where(c => c.Id != contact.Id))
                        {
                            if (c.IsPrimary)
                            {
                                c.IsPrimary = false;
                                if (c.Id > 0) // Solo actualizar si ya existe en BD
                                {
                                    var otherContact = await _supabaseService.GetActiveContactsByClientId(_selectedClient.Id);
                                    var otherDb = otherContact.FirstOrDefault(oc => oc.Id == c.Id);
                                    if (otherDb != null)
                                    {
                                        otherDb.IsPrimary = false;
                                        await _supabaseService.UpdateContact(otherDb);
                                    }
                                }
                            }
                        }
                    }

                    StatusText.Text = "Contacto guardado exitosamente";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al guardar contacto: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);

                    // Recargar para deshacer cambios en caso de error
                    await LoadContactsForClient(_selectedClient.Id);
                }
            }
        }

        private void ContactsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var contact = e.Row.Item as SimpleContactViewModel;
                if (contact == null) return;

                // Validaciones básicas
                if (e.Column.Header.ToString() == "Nombre")
                {
                    var textBox = e.EditingElement as TextBox;
                    if (string.IsNullOrWhiteSpace(textBox?.Text))
                    {
                        e.Cancel = true;
                        MessageBox.Show("El nombre del contacto es obligatorio", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else if (e.Column.Header.ToString() == "Email")
                {
                    var textBox = e.EditingElement as TextBox;
                    if (!string.IsNullOrWhiteSpace(textBox?.Text))
                    {
                        // Validación básica de email
                        if (!textBox.Text.Contains("@"))
                        {
                            e.Cancel = true;
                            MessageBox.Show("El email no tiene un formato válido", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
        }

        private async void PrimaryCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var contact = checkBox?.DataContext as SimpleContactViewModel;
            if (contact == null) return;

            // Desmarcar otros contactos como principales
            foreach (var c in _contacts.Where(c => c.Id != contact.Id))
            {
                c.IsPrimary = false;
            }

            try
            {
                // Actualizar en la base de datos
                // Primero desmarcar todos los contactos del cliente
                var allContacts = await _supabaseService.GetContactsByClientId(contact.ClientId);
                foreach (var c in allContacts)
                {
                    if (c.IsPrimary && c.Id != contact.Id)
                    {
                        c.IsPrimary = false;
                        await _supabaseService.UpdateContact(c);
                    }
                }

                // Luego marcar el contacto seleccionado como principal
                var contactDb = allContacts.FirstOrDefault(c => c.Id == contact.Id);
                if (contactDb != null)
                {
                    contactDb.IsPrimary = true;
                    await _supabaseService.UpdateContact(contactDb);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar contacto principal: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrimaryCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var contact = checkBox?.DataContext as SimpleContactViewModel;
            if (contact == null) return;

            // No permitir desmarcar si es el único marcado
            if (!_contacts.Any(c => c.IsPrimary && c.Id != contact.Id))
            {
                contact.IsPrimary = true;
                MessageBox.Show("Debe haber al menos un contacto principal", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Botones de control de ventana
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // ViewModels simplificados para la interfaz
    public class SimpleClientViewModel : INotifyPropertyChanged
    {
        private bool _isActive;

        public int Id { get; set; }
        public string Name { get; set; }
        public string TaxId { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public int CreditDays { get; set; }
        public int ContactCount { get; set; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SimpleContactViewModel : INotifyPropertyChanged
    {
        private bool _isPrimary;

        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Position { get; set; }
        public int ClientId { get; set; }

        public bool IsPrimary
        {
            get => _isPrimary;
            set
            {
                _isPrimary = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}