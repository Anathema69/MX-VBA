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

        public ClientManagementWindow(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            _clients = new ObservableCollection<SimpleClientViewModel>();
            _contacts = new ObservableCollection<SimpleContactViewModel>();

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

                var clientsDb = await _supabaseService.GetClients();
                var allContacts = await _supabaseService.GetAllContacts();

                foreach (var client in clientsDb.OrderBy(c => c.Name))
                {
                    var contactCount = allContacts.Count(c => c.ClientId == client.Id);

                    _clients.Add(new SimpleClientViewModel
                    {
                        Id = client.Id,
                        Name = client.Name,
                        TaxId = client.TaxId ?? "Sin RFC",
                        Phone = client.Phone ?? "Sin teléfono",
                        Address = client.Address1 ?? "Sin dirección",
                        Credit = client.Credit,
                        IsActive = client.IsActive,
                        ContactCount = contactCount,
                        StatusText = client.IsActive ? "ACTIVO" : "INACTIVO",
                        StatusColor = client.IsActive ? "#4CAF50" : "#f44336"
                    });
                }

                TotalClientsText.Text = _clients.Count.ToString();
                StatusText.Text = $"✓ {_clients.Count} clientes cargados";

                // Actualizar contador de búsqueda
                UpdateSearchResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar clientes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "✗ Error al cargar datos";
            }
        }

        private async Task LoadContactsForClient(int clientId)
        {
            try
            {
                StatusText.Text = "Cargando contactos...";
                _contacts.Clear();

                var contactsDb = await _supabaseService.GetContactsByClientId(clientId);

                foreach (var contact in contactsDb)
                {
                    _contacts.Add(new SimpleContactViewModel
                    {
                        Id = contact.Id,
                        ClientId = contact.ClientId,
                        ContactName = contact.ContactName,
                        Email = contact.Email ?? "",
                        Phone = contact.Phone ?? "",
                        Position = contact.Position ?? "Sin cargo",
                        IsPrimary = contact.IsPrimary,
                        IsActive = contact.IsActive,
                        StatusText = contact.IsActive ? "Activo" : "Inactivo",
                        StatusColor = contact.IsActive ? "#4CAF50" : "#f44336"
                    });
                }

                ContactCountBadge.Text = _contacts.Count.ToString();
                TotalContactsText.Text = _contacts.Count.ToString();
                StatusText.Text = $"✓ {_contacts.Count} contactos del cliente";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar contactos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "✗ Error al cargar contactos";
            }
        }

        private async void ClientsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClient = ClientsListBox.SelectedItem as SimpleClientViewModel;

            if (_selectedClient != null)
            {
                // Cerrar panel de edición si está abierto
                if (ContactEditPanel.Visibility == Visibility.Visible)
                {
                    CancelEditButton_Click(null, null);
                }

                // Mostrar panel de detalles con animación
                NoSelectionPanel.Visibility = Visibility.Collapsed;
                ClientDetailsPanel.Visibility = Visibility.Visible;

                // Aplicar animación de deslizamiento
                var storyboard = FindResource("SlideIn") as Storyboard;
                storyboard?.Begin(ClientDetailsPanel);

                // Actualizar información del cliente
                ClientNameText.Text = _selectedClient.Name;
                ClientRfcText.Text = _selectedClient.TaxId;
                ClientPhoneText.Text = _selectedClient.Phone;
                ClientCreditText.Text = $"{_selectedClient.Credit} días";
                ClientAddressText.Text = _selectedClient.Address;

                // Actualizar badge de estado
                ClientStatusTextBadge.Text = _selectedClient.StatusText;
                ClientStatusBadge.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(_selectedClient.StatusColor));

                // Cargar contactos
                await LoadContactsForClient(_selectedClient.Id);
            }
            else
            {
                // Ocultar panel de detalles
                NoSelectionPanel.Visibility = Visibility.Visible;
                ClientDetailsPanel.Visibility = Visibility.Collapsed;
                _contacts.Clear();
                TotalContactsText.Text = "0";
            }
        }

        private void ContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ContactsDataGrid.SelectedItem != null;
            EditContactButton.IsEnabled = hasSelection;
            DeleteContactButton.IsEnabled = hasSelection;
        }

        private void ContactsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Doble clic para editar
            if (ContactsDataGrid.SelectedItem != null)
            {
                EditContactButton_Click(null, null);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLower();
            int visibleCount = 0;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Mostrar todos los clientes
                foreach (var client in _clients)
                {
                    var container = ClientsListBox.ItemContainerGenerator.ContainerFromItem(client) as ListBoxItem;
                    if (container != null)
                    {
                        container.Visibility = Visibility.Visible;
                        visibleCount++;
                    }
                }
            }
            else
            {
                // Filtrar clientes
                foreach (var client in _clients)
                {
                    var container = ClientsListBox.ItemContainerGenerator.ContainerFromItem(client) as ListBoxItem;
                    if (container != null)
                    {
                        var isVisible = client.Name.ToLower().Contains(searchText) ||
                                      client.TaxId.ToLower().Contains(searchText) ||
                                      client.Phone.ToLower().Contains(searchText);

                        container.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (isVisible) visibleCount++;
                    }
                }
            }

            UpdateSearchResults(visibleCount);
        }

        private void UpdateSearchResults(int? count = null)
        {
            if (count.HasValue)
            {
                SearchResultsText.Text = $"{count}/{_clients.Count}";
            }
            else
            {
                SearchResultsText.Text = $"{_clients.Count}/{_clients.Count}";
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadClientsAsync();

            // Si había un cliente seleccionado, recargar sus contactos
            if (_selectedClient != null)
            {
                await LoadContactsForClient(_selectedClient.Id);
            }
        }

        private void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newClientWindow = new NewClientWindow(_currentUser);
                newClientWindow.Owner = this;

                if (newClientWindow.ShowDialog() == true)
                {
                    _ = LoadClientsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            MessageBox.Show($"Editar cliente: {_selectedClient.Name}\n\n" +
                          "Esta funcionalidad se implementará próximamente.",
                          "Editar Cliente",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        // ========== CRUD DE CONTACTOS ==========

        private void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            _isEditMode = false;
            _editingContact = null;

            // Preparar formulario para nuevo contacto
            ContactEditTitle.Text = "NUEVO CONTACTO";
            EditContactName.Text = "";
            EditContactPosition.Text = "Compras";
            EditContactEmail.Text = "";
            EditContactPhone.Text = "";
            EditContactIsPrimary.IsChecked = false;

            // Mostrar panel de edición
            ContactEditPanel.Visibility = Visibility.Visible;
            EditContactName.Focus();
        }

        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            _isEditMode = true;
            _editingContact = selectedContact;

            // Cargar datos en el formulario
            ContactEditTitle.Text = "EDITAR CONTACTO";
            EditContactName.Text = selectedContact.ContactName;
            EditContactPosition.Text = selectedContact.Position;
            EditContactEmail.Text = selectedContact.Email;
            EditContactPhone.Text = selectedContact.Phone;
            EditContactIsPrimary.IsChecked = selectedContact.IsPrimary;

            // Mostrar panel de edición
            ContactEditPanel.Visibility = Visibility.Visible;
            EditContactName.Focus();
            EditContactName.SelectAll();
        }

        private async void SaveContactButton_Click(object sender, RoutedEventArgs e)
        {
            // Validación básica
            if (string.IsNullOrWhiteSpace(EditContactName.Text))
            {
                MessageBox.Show("El nombre del contacto es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EditContactName.Focus();
                return;
            }

            try
            {
                StatusText.Text = "Guardando contacto...";

                if (_isEditMode && _editingContact != null)
                {
                    // Actualizar contacto existente
                    var contactToUpdate = new ContactDb
                    {
                        Id = _editingContact.Id,
                        ClientId = _editingContact.ClientId,
                        ContactName = EditContactName.Text.Trim(),
                        Position = EditContactPosition.Text?.Trim(),
                        Email = EditContactEmail.Text?.Trim(),
                        Phone = EditContactPhone.Text?.Trim(),
                        IsPrimary = EditContactIsPrimary.IsChecked ?? false,
                        IsActive = _editingContact.IsActive
                    };

                    await _supabaseService.UpdateContact(contactToUpdate);
                    StatusText.Text = "✓ Contacto actualizado";
                }
                else
                {
                    // Crear nuevo contacto
                    var newContact = new ContactDb
                    {
                        ClientId = _selectedClient.Id,
                        ContactName = EditContactName.Text.Trim(),
                        Position = EditContactPosition.Text?.Trim() ?? "Compras",
                        Email = EditContactEmail.Text?.Trim(),
                        Phone = EditContactPhone.Text?.Trim(),
                        IsPrimary = EditContactIsPrimary.IsChecked ?? false,
                        IsActive = true
                    };

                    await _supabaseService.AddContact(newContact);
                    StatusText.Text = "✓ Contacto agregado";
                }

                // Ocultar panel de edición
                ContactEditPanel.Visibility = Visibility.Collapsed;

                // Recargar contactos y actualizar el contador del cliente
                await LoadContactsForClient(_selectedClient.Id);

                // Actualizar el contador en la lista de clientes
                var clientInList = _clients.FirstOrDefault(c => c.Id == _selectedClient.Id);
                if (clientInList != null)
                {
                    clientInList.ContactCount = _contacts.Count;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar contacto: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "✗ Error al guardar";
            }
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            ContactEditPanel.Visibility = Visibility.Collapsed;
            _editingContact = null;
            _isEditMode = false;
        }

        private async void DeleteContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            var result = MessageBox.Show(
                $"¿Estás seguro de eliminar el contacto?\n\n" +
                $"Nombre: {selectedContact.ContactName}\n" +
                $"Cargo: {selectedContact.Position}\n\n" +
                $"Esta acción no se puede deshacer.",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusText.Text = "Eliminando contacto...";

                    await _supabaseService.DeleteContact(selectedContact.Id);

                    StatusText.Text = "✓ Contacto eliminado";

                    // Recargar contactos
                    await LoadContactsForClient(_selectedClient.Id);

                    // Actualizar el contador en la lista de clientes
                    var clientInList = _clients.FirstOrDefault(c => c.Id == _selectedClient.Id);
                    if (clientInList != null)
                    {
                        clientInList.ContactCount = _contacts.Count;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar contacto: {ex.Message}",
                                  "Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                    StatusText.Text = "✗ Error al eliminar";
                }
            }
        }

        private async void ContactPrimary_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var contact = (checkBox?.DataContext as SimpleContactViewModel);

            if (contact == null) return;

            try
            {
                // Si se marca como principal, desmarcar los demás
                if (checkBox.IsChecked == true)
                {
                    foreach (var c in _contacts.Where(c => c.Id != contact.Id))
                    {
                        c.IsPrimary = false;
                    }
                }

                // Actualizar en la base de datos
                var contactToUpdate = new ContactDb
                {
                    Id = contact.Id,
                    ClientId = contact.ClientId,
                    ContactName = contact.ContactName,
                    Position = contact.Position,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    IsPrimary = contact.IsPrimary,
                    IsActive = contact.IsActive
                };

                await _supabaseService.UpdateContact(contactToUpdate);
                StatusText.Text = "✓ Contacto principal actualizado";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar contacto: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Revertir el cambio
                contact.IsPrimary = !contact.IsPrimary;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ViewModels mejorados
    public class SimpleClientViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private string _taxId;
        private string _phone;
        private string _address;
        private int _credit;
        private bool _isActive;
        private int _contactCount;
        private string _statusText;
        private string _statusColor;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string TaxId
        {
            get => _taxId;
            set { _taxId = value; OnPropertyChanged(); }
        }

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }

        public int Credit
        {
            get => _credit;
            set { _credit = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public int ContactCount
        {
            get => _contactCount;
            set { _contactCount = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SimpleContactViewModel : INotifyPropertyChanged
    {
        private int _id;
        private int _clientId;
        private string _contactName;
        private string _email;
        private string _phone;
        private string _position;
        private bool _isPrimary;
        private bool _isActive;
        private string _statusText;
        private string _statusColor;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int ClientId
        {
            get => _clientId;
            set { _clientId = value; OnPropertyChanged(); }
        }

        public string ContactName
        {
            get => _contactName;
            set { _contactName = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        public string Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(); }
        }

        public bool IsPrimary
        {
            get => _isPrimary;
            set { _isPrimary = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}