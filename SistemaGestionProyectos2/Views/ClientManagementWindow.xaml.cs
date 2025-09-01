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

        // Estado para manejo de creación/edición de contactos al igual que hicimos en facturación
        private bool _isCreatingNewContact = false;
        private bool _hasUnsavedContactChanges = false;

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
                    var contactCount = allContacts.Count(c => c.ClientId == client.Id && c.IsActive);

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

        private async void EditClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            // Cargar los valores actuales en los campos de edición
            var clients = await _supabaseService.GetActiveClients();
            var clientDb = clients.FirstOrDefault(c => c.Id == _selectedClient.Id);

            if (clientDb != null)
            {
                EditClientName.Text = clientDb.Name;
                EditClientRFC.Text = clientDb.TaxId ?? "";
                EditClientPhone.Text = clientDb.Phone ?? "";
                EditClientAddress.Text = clientDb.Address1 ?? "";
                EditClientCredit.Text = clientDb.Credit.ToString();

                // Mostrar panel de edición
                ClientEditPanel.Visibility = Visibility.Visible;
                EditClientName.Focus();
                EditClientName.SelectAll();
            }
        }


        private void NewContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient == null) return;

            if (_isCreatingNewContact)
            {
                MessageBox.Show(
                    "Ya hay un contacto nuevo en edición.\n" +
                    "Complete o cancele el contacto actual antes de crear otro.",
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

            NewContactButton.IsEnabled = false; // Bloquear botón
            StatusText.Text = "📝 Nuevo contacto - Complete todos los campos y presione Guardar o Enter";

            ContactsDataGrid.SelectedItem = newContact;
            ContactsDataGrid.ScrollIntoView(newContact);

            // Focus en la columna Nombre con delay
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
                // Validar todos los contactos antes de guardar
                foreach (var contact in _contacts.Where(c => c.IsNew || c.HasChanges))
                {
                    // Validación de campos obligatorios
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

                    if (string.IsNullOrWhiteSpace(contact.Email))
                    {
                        MessageBox.Show("El email del contacto es obligatorio", "Validación",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        ContactsDataGrid.SelectedItem = contact;
                        ContactsDataGrid.CurrentCell = new DataGridCellInfo(contact, ContactsDataGrid.Columns[3]);
                        ContactsDataGrid.BeginEdit();
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(contact.Phone))
                    {
                        MessageBox.Show("El teléfono del contacto es obligatorio", "Validación",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        ContactsDataGrid.SelectedItem = contact;
                        ContactsDataGrid.CurrentCell = new DataGridCellInfo(contact, ContactsDataGrid.Columns[4]);
                        ContactsDataGrid.BeginEdit();
                        return;
                    }

                    var contactDb = new ContactDb
                    {
                        Id = contact.Id,
                        ClientId = contact.ClientId,
                        ContactName = contact.Name.Trim(),
                        Email = contact.Email.Trim(),
                        Phone = contact.Phone.Trim(),
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

                // Recargar para sincronizar con BD
                await LoadContactsForClient(_selectedClient.Id);

                // Actualizar contador en la lista
                var clientInList = _allClients.FirstOrDefault(c => c.Id == _selectedClient.Id);
                if (clientInList != null)
                {
                    clientInList.ContactCount = _contacts.Count;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "✗ Error al guardar";
            }
        }

        // Manejar teclas Enter y Escape en el DataGrid
        private void ContactsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // IMPORTANTE: Primero commitear la edición actual
                ContactsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                ContactsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                // Delay para asegurar que el commit se complete
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
                    var result = MessageBox.Show(
                        "¿Desea cancelar la creación del nuevo contacto?",
                        "Cancelar",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _contacts.Remove(newContact);
                        _isCreatingNewContact = false;
                        NewContactButton.IsEnabled = true;
                        _hasUnsavedContactChanges = false;
                        StatusText.Text = "Creación cancelada";
                    }
                }
            }
        }


        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedContact = ContactsDataGrid.SelectedItem as SimpleContactViewModel;
            if (selectedContact == null) return;

            // Simplemente enfocar la celda para editar
            ContactsDataGrid.CurrentCell = new DataGridCellInfo(selectedContact, ContactsDataGrid.Columns[1]);
            ContactsDataGrid.BeginEdit();
        }

        private async void SaveClientEdit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EditClientName.Text))
            {
                MessageBox.Show("El nombre del cliente es obligatorio", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Guardar el ID antes de actualizar
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
                    ClientEditPanel.Visibility = Visibility.Collapsed;

                    // Recargar datos
                    await LoadClientsAsync();

                    
                    // Reseleccionar el cliente usando el ID guardado
                    var updatedClient = _clients.FirstOrDefault(c => c.Id == clientId);
                    if (updatedClient != null)
                    {
                        ClientsListBox.SelectedItem = updatedClient;
                        // El evento SelectionChanged se disparará automáticamente
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelClientEdit_Click(object sender, RoutedEventArgs e)
        {
            ClientEditPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = "Edición cancelada";

            // Limpiar campos
            EditClientName.Clear();
            EditClientRFC.Clear();
            EditClientPhone.Clear();
            EditClientAddress.Clear();
            EditClientCredit.Text = "30";
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
                if (contact != null && !contact.IsNew)
                {
                    contact.HasChanges = true;
                    _hasUnsavedContactChanges = true;
                    StatusText.Text = "✏️ Cambios pendientes - Presione Guardar o Enter";
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