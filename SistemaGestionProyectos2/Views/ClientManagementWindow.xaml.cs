using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class ClientManagementWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private UserSession _currentUser;
        private ObservableCollection<ClientViewModel> _clients;
        private ObservableCollection<ContactViewModel> _contacts;
        private CollectionViewSource _clientsViewSource;
        private CollectionViewSource _contactsViewSource;
        private List<ClientDb> _allClientsDb;
        private List<ContactDb> _allContactsDb;

        public ClientManagementWindow(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            // Inicializar colecciones
            _clients = new ObservableCollection<ClientViewModel>();
            _contacts = new ObservableCollection<ContactViewModel>();

            // Configurar DataGrids
            _clientsViewSource = new CollectionViewSource { Source = _clients };
            _contactsViewSource = new CollectionViewSource { Source = _contacts };

            ClientsDataGrid.ItemsSource = _clientsViewSource.View;
            ContactsDataGrid.ItemsSource = _contactsViewSource.View;

            // Cargar datos iniciales
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StatusText.Text = "Cargando datos...";

                // Cargar clientes
                await LoadClients();

                // Cargar contactos
                await LoadContacts();

                StatusText.Text = "Datos cargados";
                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadClients()
        {
            _clients.Clear();

            _allClientsDb = await _supabaseService.GetClients();
            var contacts = await _supabaseService.GetAllContacts();

            foreach (var client in _allClientsDb)
            {
                var clientContacts = contacts.Where(c => c.ClientId == client.Id).ToList();

                _clients.Add(new ClientViewModel
                {
                    Id = client.Id,
                    Name = client.Name,
                    TaxId = client.TaxId,
                    Address1 = client.Address1,
                    Phone = client.Phone,
                    Credit = client.Credit,
                    IsActive = client.IsActive,
                    ContactCount = clientContacts.Count,
                    StatusText = client.IsActive ? "ACTIVO" : "INACTIVO"
                });
            }

            TotalCountText.Text = _clients.Count.ToString();
        }

        private async Task LoadContacts()
        {
            _contacts.Clear();

            _allContactsDb = await _supabaseService.GetAllContacts();
            _allClientsDb = _allClientsDb ?? await _supabaseService.GetClients();

            foreach (var contact in _allContactsDb)
            {
                var client = _allClientsDb.FirstOrDefault(c => c.Id == contact.ClientId);

                _contacts.Add(new ContactViewModel
                {
                    Id = contact.Id,
                    ClientId = contact.ClientId,
                    ClientName = client?.Name ?? "Sin cliente",
                    ContactName = contact.ContactName,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Position = contact.Position ?? "Compras",
                    IsPrimary = contact.IsPrimary,
                    IsActive = contact.IsActive
                });
            }

            // Llenar combo de filtro de clientes
            ClientFilterCombo.ItemsSource = _allClientsDb;
            ClientFilterCombo.Items.Insert(0, new ClientDb { Id = 0, Name = "-- Todos los clientes --" });
            ClientFilterCombo.SelectedIndex = 0;
        }

        private void ClientsTab_Checked(object sender, RoutedEventArgs e)
        {
            if (ClientsDataGrid != null)
            {
                ClientsDataGrid.Visibility = Visibility.Visible;
                ContactsDataGrid.Visibility = Visibility.Collapsed;
                ClientsToolbar.Visibility = Visibility.Visible;
                ContactsToolbar.Visibility = Visibility.Collapsed;
                TotalCountText.Text = _clients.Count.ToString();
            }
        }

        private void ContactsTab_Checked(object sender, RoutedEventArgs e)
        {
            if (ContactsDataGrid != null)
            {
                ClientsDataGrid.Visibility = Visibility.Collapsed;
                ContactsDataGrid.Visibility = Visibility.Visible;
                ClientsToolbar.Visibility = Visibility.Collapsed;
                ContactsToolbar.Visibility = Visibility.Visible;
                TotalCountText.Text = _contacts.Count.ToString();
            }
        }

        private async void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newClientWindow = new NewClientWindow(_currentUser);
                newClientWindow.Owner = this;

                if (newClientWindow.ShowDialog() == true)
                {
                    await LoadDataAsync();
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
            var selectedClient = ClientsDataGrid.SelectedItem as ClientViewModel;
            if (selectedClient == null) return;

            MessageBox.Show($"Editar cliente: {selectedClient.Name}\n\nFuncionalidad en desarrollo...",
                "Editar Cliente", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void NewContactButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Agregar nuevo contacto\n\nFuncionalidad en desarrollo...",
                "Nuevo Contacto", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLower();

            if (ClientsTab.IsChecked == true)
            {
                // Filtrar clientes
                if (_clientsViewSource?.View != null)
                {
                    _clientsViewSource.View.Filter = item =>
                    {
                        var client = item as ClientViewModel;
                        if (client == null || string.IsNullOrWhiteSpace(searchText)) return true;

                        return client.Name.ToLower().Contains(searchText) ||
                               (client.TaxId?.ToLower().Contains(searchText) ?? false) ||
                               (client.Phone?.ToLower().Contains(searchText) ?? false);
                    };
                }
            }
            else
            {
                // Filtrar contactos
                if (_contactsViewSource?.View != null)
                {
                    _contactsViewSource.View.Filter = item =>
                    {
                        var contact = item as ContactViewModel;
                        if (contact == null || string.IsNullOrWhiteSpace(searchText)) return true;

                        return contact.ContactName.ToLower().Contains(searchText) ||
                               contact.ClientName.ToLower().Contains(searchText) ||
                               (contact.Email?.ToLower().Contains(searchText) ?? false);
                    };
                }
            }
        }

        private void ClientFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientFilterCombo.SelectedItem is ClientDb selectedClient && _contactsViewSource?.View != null)
            {
                if (selectedClient.Id == 0)
                {
                    // Mostrar todos
                    _contactsViewSource.View.Filter = null;
                }
                else
                {
                    // Filtrar por cliente
                    _contactsViewSource.View.Filter = item =>
                    {
                        var contact = item as ContactViewModel;
                        return contact?.ClientId == selectedClient.Id;
                    };
                }

                var count = _contactsViewSource.View.Cast<object>().Count();
                TotalCountText.Text = count.ToString();
            }
        }

        private void ClientsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EditClientButton.IsEnabled = ClientsDataGrid.SelectedItem != null;
        }

        private void ClientsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is ClientViewModel client)
            {
                EditClientButton_Click(null, null);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ViewModels para los DataGrids
    public class ClientViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private string _taxId;
        private string _address1;
        private string _phone;
        private int _credit;
        private bool _isActive;
        private int _contactCount;
        private string _statusText;

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

        public string Address1
        {
            get => _address1;
            set { _address1 = value; OnPropertyChanged(); }
        }

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ContactViewModel : INotifyPropertyChanged
    {
        private int _id;
        private int _clientId;
        private string _clientName;
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

        public string ClientName
        {
            get => _clientName;
            set { _clientName = value; OnPropertyChanged(); }
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