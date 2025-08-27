// SimpleClientTestWindow.xaml.cs
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

                foreach (var client in clientsDb.OrderBy(c => c.Name))
                {
                    _clients.Add(new SimpleClientViewModel
                    {
                        Id = client.Id,
                        Name = client.Name,
                        TaxId = client.TaxId ?? "Sin RFC",
                        Phone = client.Phone ?? "Sin teléfono",
                        Address = client.Address1 ?? "Sin dirección",
                        Credit = client.Credit,
                        IsActive = client.IsActive,
                        StatusText = client.IsActive ? "ACTIVO" : "INACTIVO",
                        StatusColor = client.IsActive ? "#4CAF50" : "#f44336"
                    });
                }

                TotalClientsText.Text = _clients.Count.ToString();
                StatusText.Text = $"Se cargaron {_clients.Count} clientes";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar clientes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
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
                        Email = contact.Email ?? "Sin email",
                        Phone = contact.Phone ?? "Sin teléfono",
                        Position = contact.Position ?? "Sin cargo",
                        IsPrimary = contact.IsPrimary,
                        IsActive = contact.IsActive
                    });
                }

                TotalContactsText.Text = _contacts.Count.ToString();
                StatusText.Text = $"Cliente seleccionado - {_contacts.Count} contactos";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar contactos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar contactos";
            }
        }

        private async void ClientsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClient = ClientsListBox.SelectedItem as SimpleClientViewModel;

            if (_selectedClient != null)
            {
                // Mostrar panel de detalles
                NoSelectionPanel.Visibility = Visibility.Collapsed;
                ClientDetailsPanel.Visibility = Visibility.Visible;

                // Actualizar información del cliente
                ClientNameText.Text = _selectedClient.Name;
                ClientRfcText.Text = _selectedClient.TaxId;
                ClientPhoneText.Text = _selectedClient.Phone;
                ClientCreditText.Text = $"{_selectedClient.Credit} días";

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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Mostrar todos los clientes
                foreach (var client in _clients)
                {
                    var container = ClientsListBox.ItemContainerGenerator.ContainerFromItem(client) as ListBoxItem;
                    if (container != null)
                        container.Visibility = Visibility.Visible;
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
                    }
                }
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

        private void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            MessageBox.Show($"Agregar contacto para: {_selectedClient.Name}\n\n" +
                          "Esta funcionalidad se implementará próximamente.\n" +
                          "Por ahora puedes agregar contactos desde Nueva Orden.",
                          "Agregar Contacto",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            MessageBox.Show($"Editar contacto: {selectedContact.ContactName}\n\n" +
                          "Esta funcionalidad se implementará próximamente.",
                          "Editar Contacto",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        private async void DeleteContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            var result = MessageBox.Show(
                $"¿Estás seguro de eliminar el contacto?\n\n{selectedContact.ContactName}\n{selectedContact.Position}",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Aquí iría la llamada a Supabase para eliminar
                    // await _supabaseService.DeleteContact(selectedContact.Id);

                    MessageBox.Show("Funcionalidad de eliminación en desarrollo.\n" +
                                  "El contacto no ha sido eliminado.",
                                  "En Desarrollo",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);

                    // Recargar contactos después de eliminar
                    // await LoadContactsForClient(_selectedClient.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar contacto: {ex.Message}",
                                  "Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ViewModels simples para la prueba
    public class SimpleClientViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private string _taxId;
        private string _phone;
        private string _address;
        private int _credit;
        private bool _isActive;
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}