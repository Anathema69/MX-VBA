using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
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
        private ObservableCollection<SimpleClientViewModel> _allClients;

        // Estado para manejo de creación/edición de contactos
        private bool _isCreatingNewContact = false;
        private bool _hasUnsavedContactChanges = false;

        public ClientManagementWindow(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            _clients = new ObservableCollection<SimpleClientViewModel>();
            _contacts = new ObservableCollection<SimpleContactViewModel>();
            _allClients = new ObservableCollection<SimpleClientViewModel>();

            ClientsListBox.ItemsSource = _clients;
            ContactsDataGrid.ItemsSource = _contacts;

            // Cargar datos iniciales
            _ = LoadClientsAsync();
        }

        private void MaximizeWithTaskbar()
        {
            // Obtener el área de trabajo (sin incluir la barra de tareas)
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
        }

        private async Task LoadClientsAsync()
        {
            try
            {
                StatusText.Text = "Cargando clientes...";
                StatusText.Foreground = FindResource("InfoColor") as System.Windows.Media.Brush;

                _clients.Clear();
                _allClients.Clear();

                var clientsDb = await _supabaseService.GetActiveClients();
                var allContacts = await _supabaseService.GetAllContacts();

                foreach (var client in clientsDb.OrderBy(c => c.Name))
                {
                    var contactCount = allContacts.Count(c => c.ClientId == client.Id && c.IsActive);

                    var clientVm = new SimpleClientViewModel
                    {
                        Id = client.Id,
                        Name = client.Name,
                        TaxId = client.TaxId ?? "Sin RFC",
                        Phone = client.Phone ?? "Sin teléfono",
                        Address = client.Address1 ?? "Sin dirección",
                        CreditDays = client.Credit,
                        ContactCount = contactCount,
                        IsActive = client.IsActive
                    };

                    _allClients.Add(clientVm);
                    _clients.Add(clientVm);
                }

                TotalClientsText.Text = _clients.Count.ToString();
                ResultCountText.Text = _clients.Count.ToString();
                StatusText.Text = $"✓ {_clients.Count} clientes cargados";
                StatusText.Foreground = FindResource("SuccessColor") as System.Windows.Media.Brush;
            }
            catch (Exception ex)
            {
                StatusText.Text = "✗ Error al cargar clientes";
                StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
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
                        Name = contact.ContactName ?? "",
                        Email = contact.Email ?? "",
                        Phone = contact.Phone ?? "",
                        Position = contact.Position ?? "Sin cargo",
                        IsPrimary = contact.IsPrimary,
                        ClientId = contact.ClientId
                    });
                }

                ContactCountBadge.Text = _contacts.Count.ToString();
                TotalContactsText.Text = _contacts.Count.ToString();

                StatusText.Text = $"✓ {_contacts.Count} contacto(s) del cliente";
                StatusText.Foreground = FindResource("SuccessColor") as System.Windows.Media.Brush;
            }
            catch (Exception ex)
            {
                StatusText.Text = "✗ Error al cargar contactos";
                StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
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

            ResultCountText.Text = _clients.Count.ToString();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                StatusText.Text = $"🔍 {_clients.Count} resultado(s) encontrado(s)";
                StatusText.Foreground = FindResource("InfoColor") as System.Windows.Media.Brush;
            }
        }

        private void ContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ContactsDataGrid.SelectedItem != null;
            EditContactButton.IsEnabled = hasSelection;
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

        private async void EditClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            var clients = await _supabaseService.GetActiveClients();
            var clientDb = clients.FirstOrDefault(c => c.Id == _selectedClient.Id);

            if (clientDb != null)
            {
                EditClientName.Text = clientDb.Name;
                EditClientRFC.Text = clientDb.TaxId ?? "";
                EditClientPhone.Text = clientDb.Phone ?? "";
                EditClientAddress.Text = clientDb.Address1 ?? "";
                EditClientCredit.Text = clientDb.Credit.ToString();

                ClientEditPanel.Visibility = Visibility.Visible;
                EditClientName.Focus();
                EditClientName.SelectAll();

                StatusText.Text = "📝 Modo edición activado";
                StatusText.Foreground = FindResource("WarningColor") as System.Windows.Media.Brush;
            }
        }

        private void NewContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            if (_isCreatingNewContact)
            {
                MessageBox.Show(
                    "Ya hay un contacto nuevo en edición.\nComplete o cancele el contacto actual antes de crear otro.",
                    "Contacto en Edición",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var newContact = new SimpleContactViewModel
            {
                Id = 0,
                Name = "",
                Email = "",
                Phone = "",
                Position = "",
                IsPrimary = _contacts.Count == 0,
                ClientId = _selectedClient.Id,
                IsNew = true
            };

            _contacts.Add(newContact);
            _isCreatingNewContact = true;
            _hasUnsavedContactChanges = true;

            NewContactButton.IsEnabled = false;
            StatusText.Text = "📝 Nuevo contacto - Complete los campos y presione Guardar";
            StatusText.Foreground = FindResource("InfoColor") as System.Windows.Media.Brush;

            ContactsDataGrid.SelectedItem = newContact;
            ContactsDataGrid.ScrollIntoView(newContact);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ContactsDataGrid.CurrentCell = new DataGridCellInfo(newContact, ContactsDataGrid.Columns[1]);
                ContactsDataGrid.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void SaveContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasUnsavedContactChanges)
            {
                MessageBox.Show("No hay cambios para guardar", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                StatusText.Text = "💾 Guardando cambios...";
                StatusText.Foreground = FindResource("InfoColor") as System.Windows.Media.Brush;

                foreach (var contact in _contacts.Where(c => c.IsNew || c.HasChanges))
                {
                    if (string.IsNullOrWhiteSpace(contact.Name))
                    {
                        MessageBox.Show("El nombre del contacto es obligatorio", "Validación",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        ContactsDataGrid.SelectedItem = contact;
                        ContactsDataGrid.CurrentCell = new DataGridCellInfo(contact, ContactsDataGrid.Columns[1]);
                        ContactsDataGrid.BeginEdit();
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(contact.Position))
                    {
                        MessageBox.Show("El cargo del contacto es obligatorio", "Validación",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        ContactsDataGrid.SelectedItem = contact;
                        ContactsDataGrid.CurrentCell = new DataGridCellInfo(contact, ContactsDataGrid.Columns[2]);
                        ContactsDataGrid.BeginEdit();
                        return;
                    }

                    var contactDb = new ContactDb
                    {
                        Id = contact.IsNew ? 0 : contact.Id,
                        ClientId = contact.ClientId,
                        ContactName = contact.Name.Trim(),
                        Email = contact.Email?.Trim() ?? "",
                        Phone = contact.Phone?.Trim() ?? "",
                        Position = contact.Position.Trim(),
                        IsPrimary = contact.IsPrimary,
                        IsActive = true
                    };

                    if (contact.IsNew)
                    {
                        var created = await _supabaseService.CreateContact(contactDb);
                        contact.Id = created.Id;
                        contact.IsNew = false;
                        _isCreatingNewContact = false;
                        NewContactButton.IsEnabled = true;
                    }
                    else
                    {
                        await _supabaseService.UpdateContact(contactDb);
                    }

                    contact.HasChanges = false;
                }

                _hasUnsavedContactChanges = false;
                StatusText.Text = "✓ Contactos guardados exitosamente";
                StatusText.Foreground = FindResource("SuccessColor") as System.Windows.Media.Brush;

                await LoadContactsForClient(_selectedClient.Id);

                var clientInList = _allClients.FirstOrDefault(c => c.Id == _selectedClient.Id);
                if (clientInList != null)
                {
                    clientInList.ContactCount = _contacts.Count;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "✗ Error al guardar contactos";
                StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContactsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                ContactsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                ContactsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SaveContactsButton_Click(null, null);
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (e.Key == Key.Escape && _isCreatingNewContact)
            {
                var newContact = _contacts.FirstOrDefault(c => c.IsNew);
                if (newContact != null)
                {
                    _contacts.Remove(newContact);
                    _isCreatingNewContact = false;
                    NewContactButton.IsEnabled = true;
                    _hasUnsavedContactChanges = false;
                    StatusText.Text = "✗ Creación cancelada";
                    StatusText.Foreground = FindResource("WarningColor") as System.Windows.Media.Brush;
                }
            }
        }

        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            ContactsDataGrid.CurrentCell = new DataGridCellInfo(selectedContact, ContactsDataGrid.Columns[1]);
            ContactsDataGrid.BeginEdit();

            StatusText.Text = "📝 Editando contacto";
            StatusText.Foreground = FindResource("InfoColor") as System.Windows.Media.Brush;
        }

        private async void SaveClientEdit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EditClientName.Text))
            {
                MessageBox.Show("El nombre del cliente es obligatorio", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StatusText.Text = "💾 Guardando cliente...";
                StatusText.Foreground = FindResource("InfoColor") as System.Windows.Media.Brush;

                int clientId = _selectedClient.Id;

                var clients = await _supabaseService.GetActiveClients();
                var clientDb = clients.FirstOrDefault(c => c.Id == clientId);

                if (clientDb != null)
                {
                    clientDb.Name = EditClientName.Text.Trim();
                    clientDb.TaxId = string.IsNullOrWhiteSpace(EditClientRFC.Text) ? null : EditClientRFC.Text.Trim();
                    clientDb.Phone = string.IsNullOrWhiteSpace(EditClientPhone.Text) ? null : EditClientPhone.Text.Trim();
                    clientDb.Address1 = string.IsNullOrWhiteSpace(EditClientAddress.Text) ? null : EditClientAddress.Text.Trim();
                    clientDb.Credit = int.TryParse(EditClientCredit.Text, out int credit) ? credit : 30;

                    await _supabaseService.UpdateClient(clientDb, _currentUser.Id);

                    StatusText.Text = "✓ Cliente actualizado exitosamente";
                    StatusText.Foreground = FindResource("SuccessColor") as System.Windows.Media.Brush;
                    ClientEditPanel.Visibility = Visibility.Collapsed;

                    await LoadClientsAsync();

                    var updatedClient = _clients.FirstOrDefault(c => c.Id == clientId);
                    if (updatedClient != null)
                    {
                        ClientsListBox.SelectedItem = updatedClient;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "✗ Error al actualizar cliente";
                StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
                MessageBox.Show($"Error al actualizar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelClientEdit_Click(object sender, RoutedEventArgs e)
        {
            ClientEditPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = "✗ Edición cancelada";
            StatusText.Foreground = FindResource("WarningColor") as System.Windows.Media.Brush;

            EditClientName.Clear();
            EditClientRFC.Clear();
            EditClientPhone.Clear();
            EditClientAddress.Clear();
            EditClientCredit.Text = "30";
        }

        private async void DeleteContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            if (_contacts.Count <= 1)
            {
                MessageBox.Show(
                    "No se puede eliminar el último contacto del cliente.\nCada cliente debe tener al menos un contacto registrado.",
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
                    StatusText.Text = "🗑️ Eliminando contacto...";
                    StatusText.Foreground = FindResource("WarningColor") as System.Windows.Media.Brush;

                    await _supabaseService.SoftDeleteContact(selectedContact.Id);
                    await LoadContactsForClient(_selectedClient.Id);

                    StatusText.Text = "✓ Contacto eliminado exitosamente";
                    StatusText.Foreground = FindResource("SuccessColor") as System.Windows.Media.Brush;
                }
                catch (Exception ex)
                {
                    StatusText.Text = "✗ Error al eliminar contacto";
                    StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
                    MessageBox.Show($"Error al eliminar contacto: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            var orders = await _supabaseService.GetOrdersByClientId(_selectedClient.Id);
            if (orders.Any())
            {
                MessageBox.Show(
                    $"No se puede eliminar el cliente '{_selectedClient.Name}' porque tiene {orders.Count} orden(es) asociada(s).\nDebe cancelar o reasignar las órdenes antes de eliminar el cliente.",
                    "Cliente con órdenes activas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el cliente '{_selectedClient.Name}'?\nEsta acción desactivará el cliente y todos sus contactos.",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusText.Text = "🗑️ Eliminando cliente...";
                    StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;

                    var success = await _supabaseService.SoftDeleteClient(_selectedClient.Id);
                    if (success)
                    {
                        MessageBox.Show("Cliente eliminado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        _selectedClient = null;
                        NoSelectionPanel.Visibility = Visibility.Visible;
                        ClientDetailsPanel.Visibility = Visibility.Collapsed;
                        ContactsSection.Visibility = Visibility.Collapsed;

                        await LoadClientsAsync();
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = "✗ Error al eliminar cliente";
                    StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
                    MessageBox.Show($"Error al eliminar cliente: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ContactsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var contact = e.Row.Item as SimpleContactViewModel;
                if (contact != null && !contact.IsNew)
                {
                    contact.HasChanges = true;
                    _hasUnsavedContactChanges = true;
                    StatusText.Text = "✏️ Cambios pendientes - Presione Guardar";
                    StatusText.Foreground = FindResource("WarningColor") as System.Windows.Media.Brush;
                }
            }
        }

        private async void PrimaryCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var contact = checkBox?.DataContext as SimpleContactViewModel;
            if (contact == null) return;

            foreach (var c in _contacts.Where(c => c.Id != contact.Id))
            {
                c.IsPrimary = false;
            }

            try
            {
                var allContacts = await _supabaseService.GetContactsByClient(contact.ClientId);
                foreach (var c in allContacts)
                {
                    if (c.IsPrimary && c.Id != contact.Id)
                    {
                        c.IsPrimary = false;
                        await _supabaseService.UpdateContact(c);
                    }
                }

                var contactDb = allContacts.FirstOrDefault(c => c.Id == contact.Id);
                if (contactDb != null)
                {
                    contactDb.IsPrimary = true;
                    await _supabaseService.UpdateContact(contactDb);

                    StatusText.Text = "✓ Contacto principal actualizado";
                    StatusText.Foreground = FindResource("SuccessColor") as System.Windows.Media.Brush;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "✗ Error al actualizar contacto principal";
                StatusText.Foreground = FindResource("DangerColor") as System.Windows.Media.Brush;
                MessageBox.Show($"Error al actualizar contacto principal: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrimaryCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var contact = checkBox?.DataContext as SimpleContactViewModel;
            if (contact == null) return;

            if (!_contacts.Any(c => c.IsPrimary && c.Id != contact.Id))
            {
                contact.IsPrimary = true;
                MessageBox.Show("Debe haber al menos un contacto principal", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Botón de control de ventana
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // ViewModels
    public class SimpleClientViewModel : INotifyPropertyChanged
    {
        private bool _isActive;
        private int _contactCount;

        public int Id { get; set; }
        public string Name { get; set; }
        public string TaxId { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public int CreditDays { get; set; }

        public int ContactCount
        {
            get => _contactCount;
            set
            {
                _contactCount = value;
                OnPropertyChanged();
            }
        }

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

        // Estado para manejo de creación/edición
        public bool IsNew { get; set; }
        public bool HasChanges { get; set; }

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