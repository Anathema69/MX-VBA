using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Attendance;

namespace SistemaGestionProyectos2.Views
{
    public partial class CalendarView : Window
    {
        private UserSession _currentUser;
        private DateTime _selectedDate;
        private DateTime _currentMonth;
        private AttendanceService _attendanceService;
        private List<AttendanceViewModel> _currentAttendance;
        private bool _isLoading = false;

        // Hora de entrada esperada (8:00 AM)
        private readonly TimeSpan _expectedStartTime = new TimeSpan(8, 0, 0);

        // Cache de asistencia por fecha para evitar recargas innecesarias
        private Dictionary<string, List<AttendanceViewModel>> _attendanceCache = new();
        private Dictionary<string, int> _currentMonthStats;

        // Cache de botones del calendario para actualizaciones rápidas
        private Dictionary<int, Button> _calendarButtons = new();

        // Colores para la UI
        private static readonly SolidColorBrush AttendanceColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
        private static readonly SolidColorBrush AttendanceLight = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
        private static readonly SolidColorBrush LateColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
        private static readonly SolidColorBrush LateLight = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
        private static readonly SolidColorBrush AbsentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        private static readonly SolidColorBrush AbsentLight = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
        private static readonly SolidColorBrush VacationColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"));
        private static readonly SolidColorBrush VacationLight = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDE9FE"));
        private static readonly SolidColorBrush PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
        private static readonly SolidColorBrush BorderGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
        private static readonly SolidColorBrush BackgroundGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB"));
        private static readonly SolidColorBrush TextGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        private static readonly SolidColorBrush DarkText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"));

        public CalendarView(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _selectedDate = DateTime.Today;
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            var supabaseClient = SupabaseService.Instance.GetClient();
            _attendanceService = new AttendanceService(supabaseClient);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MaximizeWithTaskbar();
            InitializeUI();
        }

        private void MaximizeWithTaskbar()
        {
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
        }

        private async void InitializeUI()
        {
            UpdateMonthDisplay();
            UpdateSelectedDateDisplay();
            GenerateCalendarDays();

            try
            {
                _isLoading = true;
                StatusText.Text = "Cargando...";

                // Cargar estadísticas y asistencia en paralelo
                var statsTask = LoadMonthlyStats();
                var attendanceTask = LoadAttendanceForDate(_selectedDate);
                await System.Threading.Tasks.Task.WhenAll(statsTask, attendanceTask);

                StatusText.Text = "";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = AbsentColor;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateMonthDisplay()
        {
            var culture = new System.Globalization.CultureInfo("es-MX");
            var monthText = _currentMonth.ToString("MMMM yyyy", culture);
            monthText = char.ToUpper(monthText[0]) + monthText.Substring(1);
            CurrentMonthText.Text = monthText.ToUpper();
            CalendarMonthText.Text = monthText.ToUpper();
        }

        private void UpdateSelectedDateDisplay()
        {
            var culture = new System.Globalization.CultureInfo("es-MX");
            SelectedDateText.Text = _selectedDate.ToString("dddd, dd 'de' MMMM yyyy", culture);
            SelectedDateText.Text = char.ToUpper(SelectedDateText.Text[0]) + SelectedDateText.Text.Substring(1);
        }

        /// <summary>
        /// Genera los días del calendario dinámicamente con alineación correcta
        /// </summary>
        private void GenerateCalendarDays()
        {
            CalendarGrid.Children.Clear();
            _calendarButtons.Clear();

            var firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            // Calcular el día de la semana del primer día (Lunes=0...Domingo=6)
            int startDayOfWeek = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

            // Días del mes anterior para rellenar
            var prevMonth = firstDayOfMonth.AddMonths(-1);
            var daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

            for (int i = startDayOfWeek - 1; i >= 0; i--)
            {
                var day = daysInPrevMonth - i;
                var btn = CreateCalendarDayButton(day, true, false);
                CalendarGrid.Children.Add(btn);
            }

            // Días del mes actual
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                bool isToday = date.Date == DateTime.Today;
                bool isSelected = date.Date == _selectedDate.Date;
                bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                var btn = CreateCalendarDayButton(day, false, isWeekend, isToday, isSelected);
                btn.Tag = day;
                btn.Click += CalendarDay_Click;
                _calendarButtons[day] = btn;
                CalendarGrid.Children.Add(btn);
            }

            // Días del próximo mes para completar la grilla (42 celdas = 6 filas x 7 días)
            int totalCells = CalendarGrid.Children.Count;
            int remainingCells = 42 - totalCells;
            for (int day = 1; day <= remainingCells; day++)
            {
                var btn = CreateCalendarDayButton(day, true, false);
                CalendarGrid.Children.Add(btn);
            }
        }

        /// <summary>
        /// Actualiza solo la selección del calendario sin regenerar todos los botones
        /// </summary>
        private void UpdateCalendarSelection(int newSelectedDay)
        {
            foreach (var kvp in _calendarButtons)
            {
                var day = kvp.Key;
                var btn = kvp.Value;
                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                bool isToday = date.Date == DateTime.Today;
                bool isSelected = day == newSelectedDay;
                bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                // Actualizar el contenido visual del botón con selección más prominente
                var border = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = isSelected ? PrimaryColor : (isToday ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF")) : Brushes.Transparent),
                    BorderBrush = isSelected ? PrimaryColor : (isToday ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C7D2FE")) : Brushes.Transparent),
                    BorderThickness = new Thickness(isSelected ? 2 : (isToday ? 2 : 0)),
                    Padding = new Thickness(4)
                };

                var text = new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = isSelected ? 14 : 13,
                    FontWeight = isToday || isSelected ? FontWeights.Bold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isSelected ? Brushes.White : (isWeekend ? AbsentColor : DarkText)
                };

                border.Child = text;
                btn.Content = border;
            }
        }

        private Button CreateCalendarDayButton(int day, bool isOtherMonth, bool isWeekend, bool isToday = false, bool isSelected = false)
        {
            var btn = new Button
            {
                Width = 38,
                Height = 36,
                Margin = new Thickness(1),
                Cursor = isOtherMonth ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand,
                IsEnabled = !isOtherMonth
            };

            // Diseño más prominente para la selección
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = isSelected ? PrimaryColor : (isToday ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF")) : Brushes.Transparent),
                BorderBrush = isSelected ? PrimaryColor : (isToday ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C7D2FE")) : Brushes.Transparent),
                BorderThickness = new Thickness(isSelected ? 2 : (isToday ? 2 : 0)),
                Padding = new Thickness(4)
            };

            var text = new TextBlock
            {
                Text = day.ToString(),
                FontSize = isSelected ? 14 : 13,
                FontWeight = isToday || isSelected ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isSelected ? Brushes.White :
                            isOtherMonth ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")) :
                            isWeekend ? AbsentColor : DarkText
            };

            border.Child = text;
            btn.Content = border;
            btn.Template = CreateTransparentButtonTemplate();
            btn.Opacity = isOtherMonth ? 0.4 : 1;

            return btn;
        }

        private ControlTemplate CreateTransparentButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            template.VisualTree = cp;
            return template;
        }

        private async System.Threading.Tasks.Task LoadMonthlyStats()
        {
            try
            {
                _currentMonthStats = await _attendanceService.GetMonthlyStats(_currentMonth.Year, _currentMonth.Month);
                UpdateStatsDisplay();
            }
            catch { }
        }

        private void UpdateStatsDisplay()
        {
            if (_currentMonthStats == null) return;
            AttendanceCountText.Text = _currentMonthStats.GetValueOrDefault("Asistencias", 0).ToString();
            LateCountText.Text = _currentMonthStats.GetValueOrDefault("Retardos", 0).ToString();
            AbsentCountText.Text = _currentMonthStats.GetValueOrDefault("Faltas", 0).ToString();
            VacationCountText.Text = _currentMonthStats.GetValueOrDefault("Vacaciones", 0).ToString();
        }

        /// <summary>
        /// Actualiza estadísticas localmente sin consultar BD
        /// </summary>
        private void UpdateStatsLocally(string oldStatus, string newStatus)
        {
            if (_currentMonthStats == null) return;

            // Decrementar el contador anterior (si no era SIN_REGISTRO)
            if (oldStatus == "ASISTENCIA") _currentMonthStats["Asistencias"]--;
            else if (oldStatus == "RETARDO") _currentMonthStats["Retardos"]--;
            else if (oldStatus == "FALTA") _currentMonthStats["Faltas"]--;
            else if (oldStatus == "VACACIONES") _currentMonthStats["Vacaciones"]--;

            // Incrementar el nuevo contador
            if (newStatus == "ASISTENCIA") _currentMonthStats["Asistencias"]++;
            else if (newStatus == "RETARDO") _currentMonthStats["Retardos"]++;
            else if (newStatus == "FALTA") _currentMonthStats["Faltas"]++;
            else if (newStatus == "VACACIONES") _currentMonthStats["Vacaciones"]++;

            UpdateStatsDisplay();
        }

        private async System.Threading.Tasks.Task LoadAttendanceForDate(DateTime date)
        {
            string cacheKey = date.ToString("yyyy-MM-dd");

            // Verificar si está en cache
            if (_attendanceCache.TryGetValue(cacheKey, out var cached))
            {
                _currentAttendance = cached;
                RenderEmployeeList();
                return;
            }

            try
            {
                _currentAttendance = await _attendanceService.GetAttendanceForDate(date);
                _attendanceCache[cacheKey] = _currentAttendance;
                RenderEmployeeList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Invalida el cache para una fecha específica
        /// </summary>
        private void InvalidateDateCache(DateTime date)
        {
            string cacheKey = date.ToString("yyyy-MM-dd");
            _attendanceCache.Remove(cacheKey);
        }

        private void RenderEmployeeList()
        {
            // Ocultar indicador de carga si existe
            var loadingText = EmployeeListPanel.FindName("LoadingText") as TextBlock;
            if (loadingText != null)
            {
                EmployeeListPanel.Children.Remove(loadingText);
                EmployeeListPanel.UnregisterName("LoadingText");
            }

            // Mantener solo la leyenda (primer hijo)
            while (EmployeeListPanel.Children.Count > 1)
                EmployeeListPanel.Children.RemoveAt(1);

            if (_currentAttendance == null || !_currentAttendance.Any())
            {
                EmployeeListPanel.Children.Add(new TextBlock
                {
                    Text = "No hay empleados registrados",
                    FontSize = 14, Foreground = TextGray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                });
                return;
            }

            foreach (var emp in _currentAttendance)
                EmployeeListPanel.Children.Add(CreateEmployeeCard(emp));
        }

        /// <summary>
        /// Actualiza solo la tarjeta de un empleado específico (sin regenerar toda la lista)
        /// </summary>
        private void UpdateEmployeeCard(int employeeId, AttendanceViewModel updatedAttendance)
        {
            // Buscar el índice del empleado en la lista actual
            var index = _currentAttendance.FindIndex(a => a.EmployeeId == employeeId);
            if (index < 0) return;

            // Actualizar la lista en memoria
            _currentAttendance[index] = updatedAttendance;

            // El índice en EmployeeListPanel es index + 1 (porque el primero es la leyenda)
            int panelIndex = index + 1;
            if (panelIndex < EmployeeListPanel.Children.Count)
            {
                EmployeeListPanel.Children.RemoveAt(panelIndex);
                EmployeeListPanel.Children.Insert(panelIndex, CreateEmployeeCard(updatedAttendance));
            }
        }

        private Border CreateEmployeeCard(AttendanceViewModel emp)
        {
            SolidColorBrush bgColor = Brushes.White;
            SolidColorBrush borderColor = BorderGray;
            SolidColorBrush avatarBg = BackgroundGray;
            SolidColorBrush avatarFg = TextGray;
            string statusText = "";

            switch (emp.Status)
            {
                case "ASISTENCIA":
                    avatarBg = AttendanceLight; avatarFg = AttendanceColor;
                    break;
                case "RETARDO":
                    borderColor = LateColor; avatarBg = LateLight; avatarFg = LateColor;
                    statusText = $"Retardo {emp.LateMinutes} min";
                    break;
                case "FALTA":
                    bgColor = AbsentLight; borderColor = AbsentColor;
                    avatarBg = Brushes.White; avatarFg = AbsentColor;
                    statusText = emp.IsJustified ? "Falta justificada" : "Falta";
                    break;
                case "VACACIONES":
                    bgColor = VacationLight; borderColor = VacationColor;
                    avatarBg = Brushes.White; avatarFg = VacationColor;
                    statusText = "Vacaciones";
                    break;
                default:
                    statusText = "Sin registro";
                    break;
            }

            if (emp.OnVacation && emp.Status == "SIN_REGISTRO")
            {
                bgColor = VacationLight; borderColor = VacationColor;
                avatarBg = Brushes.White; avatarFg = VacationColor;
                statusText = "Vacaciones";
            }

            var card = new Border
            {
                Background = bgColor, CornerRadius = new CornerRadius(10),
                BorderBrush = borderColor, BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatar
            var avatar = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = avatarBg, Margin = new Thickness(0, 0, 14, 0) };
            avatar.Child = new TextBlock { Text = emp.Initials, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = avatarFg, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(avatar, 0);

            // Info
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoPanel.Children.Add(new TextBlock { Text = emp.EmployeeName, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = DarkText });

            var detailPanel = new StackPanel { Orientation = Orientation.Horizontal };
            detailPanel.Children.Add(new TextBlock { Text = emp.Title ?? "Sin cargo", FontSize = 12, Foreground = TextGray });

            if (!string.IsNullOrEmpty(statusText) && statusText != "Sin registro")
            {
                var badge = new Border { Background = emp.Status == "RETARDO" ? LateLight : BackgroundGray, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(8, 0, 0, 0) };
                badge.Child = new TextBlock { Text = statusText, FontSize = 10, FontWeight = FontWeights.Medium, Foreground = emp.Status switch { "RETARDO" => LateColor, "FALTA" => AbsentColor, "VACACIONES" => VacationColor, _ => TextGray } };
                detailPanel.Children.Add(badge);
            }
            infoPanel.Children.Add(detailPanel);
            Grid.SetColumn(infoPanel, 1);

            // Botones
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            bool isOnVacation = emp.OnVacation || emp.Status == "VACACIONES";

            buttonsPanel.Children.Add(CreateStatusButton("ASISTENCIA", "✓", emp, isOnVacation));
            buttonsPanel.Children.Add(CreateStatusButton("RETARDO", "🕐", emp, isOnVacation));
            buttonsPanel.Children.Add(CreateStatusButton("FALTA", "✗", emp, isOnVacation));
            buttonsPanel.Children.Add(CreateStatusButton("VACACIONES", "🏖", emp, false));
            Grid.SetColumn(buttonsPanel, 2);

            grid.Children.Add(avatar);
            grid.Children.Add(infoPanel);
            grid.Children.Add(buttonsPanel);
            card.Child = grid;
            return card;
        }

        private Button CreateStatusButton(string status, string icon, AttendanceViewModel emp, bool disabled)
        {
            bool isSelected = emp.Status == status || (status == "VACACIONES" && emp.OnVacation);

            SolidColorBrush bgColor = BorderGray;
            SolidColorBrush fgColor = TextGray;

            if (isSelected)
            {
                switch (status)
                {
                    case "ASISTENCIA": bgColor = AttendanceColor; fgColor = Brushes.White; break;
                    case "RETARDO": bgColor = LateColor; fgColor = Brushes.White; break;
                    case "FALTA": bgColor = AbsentColor; fgColor = Brushes.White; break;
                    case "VACACIONES": bgColor = VacationColor; fgColor = Brushes.White; break;
                }
            }

            var button = new Button
            {
                Width = 36, Height = 36, Margin = new Thickness(0, 0, 6, 0),
                Cursor = System.Windows.Input.Cursors.Hand, IsEnabled = !disabled, Opacity = disabled ? 0.5 : 1,
                Background = bgColor,
                ToolTip = status switch { "ASISTENCIA" => "Asistencia", "RETARDO" => "Retardo (hora entrada)", "FALTA" => "Falta", "VACACIONES" => "Vacaciones", _ => status },
                Tag = new { EmployeeId = emp.EmployeeId, Status = status, CurrentAttendance = emp }
            };

            // Template que usa el Background del botón
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            button.Template = template;

            button.Content = new TextBlock { Text = icon, FontSize = 14, FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal, Foreground = fgColor, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.Click += StatusButton_Click;
            return button;
        }

        private async void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var button = sender as Button;
            if (button?.Tag == null) return;

            dynamic tag = button.Tag;
            int employeeId = tag.EmployeeId;
            string newStatus = tag.Status;
            AttendanceViewModel currentAtt = tag.CurrentAttendance;

            if (currentAtt.Status == newStatus) return;

            // Verificar si el empleado está de vacaciones (excepto si se marca como vacaciones)
            if (newStatus != "VACACIONES" && (currentAtt.OnVacation || currentAtt.Status == "VACACIONES"))
            {
                MessageBox.Show(
                    "Este empleado está de vacaciones en esta fecha.\nNo se puede registrar asistencia, retardo o falta.",
                    "Empleado en Vacaciones",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string oldStatus = currentAtt.Status;
            TimeSpan? checkInTime = null;
            int lateMinutes = 0;

            if (newStatus == "RETARDO")
            {
                var result = ShowCheckInTimeDialog();
                if (result == null) return;
                checkInTime = result.Value;
                lateMinutes = _attendanceService.CalculateLateMinutes(checkInTime.Value, _expectedStartTime);
                if (lateMinutes <= 0)
                {
                    MessageBox.Show($"La hora {checkInTime:hh\\:mm} no genera retardo.\nHora esperada: {_expectedStartTime:hh\\:mm}", "Sin Retardo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else if (newStatus == "ASISTENCIA")
            {
                checkInTime = _expectedStartTime;
            }

            try
            {
                _isLoading = true;
                StatusText.Text = "Guardando...";
                StatusText.Foreground = PrimaryColor;

                var attendance = new AttendanceTable
                {
                    Id = currentAtt.Id, EmployeeId = employeeId, AttendanceDate = _selectedDate,
                    Status = newStatus, CheckInTime = checkInTime, LateMinutes = lateMinutes,
                    CreatedBy = _currentUser.Id, UpdatedBy = _currentUser.Id
                };

                var saved = await _attendanceService.SaveAttendance(attendance);

                // Actualizar el cache local en lugar de recargar todo
                var updatedViewModel = new AttendanceViewModel
                {
                    Id = saved.Id,
                    EmployeeId = currentAtt.EmployeeId,
                    EmployeeName = currentAtt.EmployeeName,
                    EmployeeCode = currentAtt.EmployeeCode,
                    Title = currentAtt.Title,
                    Initials = currentAtt.Initials,
                    AttendanceDate = _selectedDate,
                    Status = newStatus,
                    CheckInTime = checkInTime,
                    LateMinutes = lateMinutes,
                    IsJustified = false,
                    OnVacation = currentAtt.OnVacation,
                    IsHoliday = currentAtt.IsHoliday,
                    HolidayName = currentAtt.HolidayName,
                    IsWorkday = currentAtt.IsWorkday
                };

                // Actualizar cache de fecha
                string cacheKey = _selectedDate.ToString("yyyy-MM-dd");
                if (_attendanceCache.ContainsKey(cacheKey))
                {
                    var index = _attendanceCache[cacheKey].FindIndex(a => a.EmployeeId == employeeId);
                    if (index >= 0) _attendanceCache[cacheKey][index] = updatedViewModel;
                }

                // Actualizar solo la tarjeta del empleado (sin regenerar toda la lista)
                UpdateEmployeeCard(employeeId, updatedViewModel);

                // Actualizar estadísticas localmente
                UpdateStatsLocally(oldStatus, newStatus);

                StatusText.Text = $"✓ {currentAtt.EmployeeName}: {newStatus}";
                StatusText.Foreground = AttendanceColor;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = AbsentColor;
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Diálogo mejorado para seleccionar hora de entrada con TextBox editable
        /// </summary>
        private TimeSpan? ShowCheckInTimeDialog()
        {
            var dialog = new Window
            {
                Title = "Registrar Hora de Entrada",
                Width = 380,
                SizeToContent = SizeToContent.Height,
                MinHeight = 300,
                MaxHeight = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = Brushes.White
            };

            var mainPanel = new StackPanel { Margin = new Thickness(32, 24, 32, 28) };

            // Título
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Hora de Entrada",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkText,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Subtítulo con hora esperada
            mainPanel.Children.Add(new TextBlock
            {
                Text = $"Hora esperada: {_expectedStartTime:hh\\:mm}",
                FontSize = 13,
                Foreground = TextGray,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Label
            mainPanel.Children.Add(new TextBlock
            {
                Text = "¿A qué hora llegó el empleado?",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = DarkText,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Panel de entrada de hora
            var timePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // Validación: solo números y rango válido (considerando texto seleccionado)
            void ValidateHourInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            {
                var textBox = sender as TextBox;

                // Solo permitir dígitos
                if (!int.TryParse(e.Text, out _))
                {
                    e.Handled = true;
                    return;
                }

                // Calcular texto resultante considerando la selección que será reemplazada
                string currentText = textBox.Text;
                int selStart = textBox.SelectionStart;
                int selLength = textBox.SelectionLength;
                string newText = currentText.Substring(0, selStart) + e.Text + currentText.Substring(selStart + selLength);

                // Verificar rango 0-23
                if (int.TryParse(newText, out int h) && h > 23)
                {
                    e.Handled = true;
                }
            }

            void ValidateMinuteInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            {
                var textBox = sender as TextBox;

                // Solo permitir dígitos
                if (!int.TryParse(e.Text, out _))
                {
                    e.Handled = true;
                    return;
                }

                // Calcular texto resultante considerando la selección que será reemplazada
                string currentText = textBox.Text;
                int selStart = textBox.SelectionStart;
                int selLength = textBox.SelectionLength;
                string newText = currentText.Substring(0, selStart) + e.Text + currentText.Substring(selStart + selLength);

                // Verificar rango 0-59
                if (int.TryParse(newText, out int m) && m > 59)
                {
                    e.Handled = true;
                }
            }

            // Validación: prevenir pegado de texto no numérico
            void OnPaste(object sender, DataObjectPastingEventArgs e)
            {
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string text = (string)e.DataObject.GetData(typeof(string));
                    if (!int.TryParse(text, out _))
                        e.CancelCommand();
                }
                else
                {
                    e.CancelCommand();
                }
            }

            // TextBox para hora (con validación)
            var hourTextBox = new TextBox
            {
                Width = 70,
                Height = 45,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Text = "08",
                MaxLength = 2,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            hourTextBox.GotFocus += (s, ev) => hourTextBox.SelectAll();
            hourTextBox.PreviewTextInput += ValidateHourInput;
            DataObject.AddPastingHandler(hourTextBox, OnPaste);

            // Validar rango de hora (0-23) al perder foco
            hourTextBox.LostFocus += (s, ev) =>
            {
                if (int.TryParse(hourTextBox.Text, out int h))
                {
                    if (h > 23) hourTextBox.Text = "23";
                    else if (h < 0) hourTextBox.Text = "00";
                    else hourTextBox.Text = h.ToString("D2");
                }
                else
                {
                    hourTextBox.Text = "08";
                }
            };

            timePanel.Children.Add(hourTextBox);

            // Separador
            timePanel.Children.Add(new TextBlock
            {
                Text = ":",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0),
                Foreground = DarkText
            });

            // TextBox para minutos (con validación)
            var minuteTextBox = new TextBox
            {
                Width = 70,
                Height = 45,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Text = "15",
                MaxLength = 2,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            minuteTextBox.GotFocus += (s, ev) => minuteTextBox.SelectAll();
            minuteTextBox.PreviewTextInput += ValidateMinuteInput;
            DataObject.AddPastingHandler(minuteTextBox, OnPaste);

            // Validar rango de minutos (0-59) al perder foco
            minuteTextBox.LostFocus += (s, ev) =>
            {
                if (int.TryParse(minuteTextBox.Text, out int m))
                {
                    if (m > 59) minuteTextBox.Text = "59";
                    else if (m < 0) minuteTextBox.Text = "00";
                    else minuteTextBox.Text = m.ToString("D2");
                }
                else
                {
                    minuteTextBox.Text = "00";
                }
            };

            timePanel.Children.Add(minuteTextBox);

            mainPanel.Children.Add(timePanel);

            // Texto de ayuda
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Formato 24 horas (00-23 : 00-59) • Enter para guardar",
                FontSize = 11,
                Foreground = TextGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Mensaje de error/validación
            var errorText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = AbsentColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            mainPanel.Children.Add(errorText);

            // Preview del retardo
            var previewText = new TextBlock
            {
                Text = "",
                FontSize = 14,
                Foreground = LateColor,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            mainPanel.Children.Add(previewText);

            // Función para actualizar preview con validación
            void UpdatePreview()
            {
                errorText.Text = "";
                previewText.Text = "";

                if (!int.TryParse(hourTextBox.Text, out int h) || !int.TryParse(minuteTextBox.Text, out int m))
                {
                    errorText.Text = "Ingrese valores numéricos";
                    return;
                }

                if (h < 0 || h > 23)
                {
                    errorText.Text = "Hora debe ser 00-23";
                    return;
                }

                if (m < 0 || m > 59)
                {
                    errorText.Text = "Minutos debe ser 00-59";
                    return;
                }

                var time = new TimeSpan(h, m, 0);
                var mins = _attendanceService.CalculateLateMinutes(time, _expectedStartTime);
                if (mins > 0)
                    previewText.Text = $"⏰ Retardo de {mins} minutos";
                else
                    previewText.Text = "✓ Sin retardo (llegada puntual)";
            }

            hourTextBox.TextChanged += (s, ev) => UpdatePreview();
            minuteTextBox.TextChanged += (s, ev) => UpdatePreview();
            UpdatePreview(); // Inicial

            // Handler para Enter - guardar al presionar Enter en cualquier TextBox
            void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs ev)
            {
                if (ev.Key == System.Windows.Input.Key.Enter)
                {
                    dialog.DialogResult = true;
                    ev.Handled = true;
                }
                else if (ev.Key == System.Windows.Input.Key.Escape)
                {
                    dialog.DialogResult = false;
                    ev.Handled = true;
                }
            }

            hourTextBox.KeyDown += OnKeyDown;
            minuteTextBox.KeyDown += OnKeyDown;

            // Auto-avanzar a minutos cuando se ingresan 2 dígitos en hora
            hourTextBox.TextChanged += (s, ev) =>
            {
                if (hourTextBox.Text.Length == 2)
                    minuteTextBox.Focus();
            };

            // Botones
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                Width = 120,
                Height = 40,
                FontSize = 14,
                Background = BackgroundGray,
                BorderBrush = BorderGray,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 12, 0)
            };
            cancelBtn.Click += (s, ev) => dialog.DialogResult = false;
            buttonsPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "✓ Registrar",
                Width = 140,
                Height = 40,
                FontSize = 14,
                Background = LateColor,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okBtn.Click += (s, ev) => dialog.DialogResult = true;
            buttonsPanel.Children.Add(okBtn);

            mainPanel.Children.Add(buttonsPanel);
            dialog.Content = mainPanel;

            // Focus inicial en hora
            dialog.Loaded += (s, ev) =>
            {
                hourTextBox.Focus();
                hourTextBox.SelectAll();
            };

            if (dialog.ShowDialog() == true)
            {
                if (int.TryParse(hourTextBox.Text, out int hours) &&
                    int.TryParse(minuteTextBox.Text, out int minutes))
                {
                    if (hours >= 0 && hours <= 23 && minutes >= 0 && minutes <= 59)
                    {
                        return new TimeSpan(hours, minutes, 0);
                    }
                }
                MessageBox.Show("Hora inválida. Use formato HH:MM (ej: 08:30)", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return ShowCheckInTimeDialog(); // Reintentar
            }
            return null;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => Close();

        private async void PreviousMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateMonthDisplay();
            GenerateCalendarDays();
            _currentMonthStats = null; // Invalidar stats del mes anterior
            try { await LoadMonthlyStats(); } catch { }
        }

        private async void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateMonthDisplay();
            GenerateCalendarDays();
            _currentMonthStats = null; // Invalidar stats del mes anterior
            try { await LoadMonthlyStats(); } catch { }
        }

        private async void CalendarDay_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var button = sender as Button;
            if (button?.Tag != null && int.TryParse(button.Tag.ToString(), out int day))
            {
                var newDate = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                if (newDate.Date == _selectedDate.Date) return; // Ya está seleccionado

                int previousDay = _selectedDate.Day;
                _selectedDate = newDate;
                UpdateSelectedDateDisplay();

                // Solo actualizar la selección visual, no regenerar todo
                UpdateCalendarSelection(day);

                try
                {
                    _isLoading = true;
                    StatusText.Text = "Cargando...";
                    await LoadAttendanceForDate(_selectedDate);
                    StatusText.Text = "";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        private async void AddVacation_Click(object sender, RoutedEventArgs e)
        {
            var result = await ShowVacationDialog();
            if (result != null)
            {
                try
                {
                    _isLoading = true;
                    StatusText.Text = "Registrando vacaciones...";
                    StatusText.Foreground = PrimaryColor;

                    // Verificar conflictos
                    var hasConflict = await _attendanceService.HasVacationConflict(
                        result.EmployeeId, result.StartDate, result.EndDate);

                    if (hasConflict)
                    {
                        MessageBox.Show("El empleado ya tiene vacaciones registradas en ese período.",
                            "Conflicto de Fechas", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Crear la vacación
                    result.CreatedBy = _currentUser.Id;
                    await _attendanceService.CreateVacation(result);

                    // Invalidar cache y recargar
                    _attendanceCache.Clear();
                    await LoadAttendanceForDate(_selectedDate);

                    var empName = _currentAttendance?.FirstOrDefault(a => a.EmployeeId == result.EmployeeId)?.EmployeeName ?? "Empleado";
                    StatusText.Text = $"✓ Vacaciones registradas para {empName}";
                    StatusText.Foreground = VacationColor;
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                    StatusText.Foreground = AbsentColor;
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        /// <summary>
        /// Diálogo para registrar vacaciones
        /// </summary>
        private async System.Threading.Tasks.Task<VacationTable> ShowVacationDialog()
        {
            // Cargar empleados
            List<PayrollTable> employees;
            try
            {
                employees = await _attendanceService.GetActiveEmployees();
            }
            catch
            {
                MessageBox.Show("Error cargando empleados", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            if (!employees.Any())
            {
                MessageBox.Show("No hay empleados activos", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var dialog = new Window
            {
                Title = "Registrar Vacaciones",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                MinHeight = 400,
                MaxHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = Brushes.White
            };

            var mainPanel = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };

            // Título
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Registrar Vacaciones",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = VacationColor,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Selector de Empleado
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Empleado *",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = DarkText,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var employeeCombo = new ComboBox
            {
                Height = 38,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                DisplayMemberPath = "Employee",
                SelectedValuePath = "Id",
                ItemsSource = employees
            };
            mainPanel.Children.Add(employeeCombo);

            // Fecha Inicio
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Fecha Inicio *",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = DarkText,
                Margin = new Thickness(0, 16, 0, 6)
            });

            var startDatePicker = new DatePicker
            {
                Height = 38,
                FontSize = 14,
                SelectedDate = _selectedDate,
                DisplayDateStart = DateTime.Today
            };
            mainPanel.Children.Add(startDatePicker);

            // Fecha Fin
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Fecha Fin *",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = DarkText,
                Margin = new Thickness(0, 16, 0, 6)
            });

            var endDatePicker = new DatePicker
            {
                Height = 38,
                FontSize = 14,
                SelectedDate = _selectedDate.AddDays(7),
                DisplayDateStart = DateTime.Today
            };
            mainPanel.Children.Add(endDatePicker);

            // Preview de días
            var daysPreview = new TextBlock
            {
                Text = "",
                FontSize = 13,
                Foreground = VacationColor,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 8, 0, 0)
            };
            mainPanel.Children.Add(daysPreview);

            // Actualizar preview de días
            void UpdateDaysPreview()
            {
                if (startDatePicker.SelectedDate.HasValue && endDatePicker.SelectedDate.HasValue)
                {
                    if (endDatePicker.SelectedDate < startDatePicker.SelectedDate)
                    {
                        daysPreview.Text = "⚠ Fecha fin debe ser posterior a fecha inicio";
                        daysPreview.Foreground = AbsentColor;
                    }
                    else
                    {
                        var days = _attendanceService.CalculateWorkingDays(
                            startDatePicker.SelectedDate.Value,
                            endDatePicker.SelectedDate.Value);
                        daysPreview.Text = $"📅 {days} día{(days != 1 ? "s" : "")} laboral{(days != 1 ? "es" : "")}";
                        daysPreview.Foreground = VacationColor;
                    }
                }
            }

            startDatePicker.SelectedDateChanged += (s, ev) => UpdateDaysPreview();
            endDatePicker.SelectedDateChanged += (s, ev) => UpdateDaysPreview();
            UpdateDaysPreview();

            // Observaciones
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Observaciones (opcional)",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = DarkText,
                Margin = new Thickness(0, 16, 0, 6)
            });

            var notesTextBox = new TextBox
            {
                Height = 70,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10, 8, 10, 8)
            };
            mainPanel.Children.Add(notesTextBox);

            // Mensaje de error
            var errorText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = AbsentColor,
                Margin = new Thickness(0, 12, 0, 0)
            };
            mainPanel.Children.Add(errorText);

            // Botones
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                Width = 100,
                Height = 38,
                FontSize = 14,
                Background = BackgroundGray,
                BorderBrush = BorderGray,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 12, 0)
            };
            cancelBtn.Click += (s, ev) => dialog.DialogResult = false;
            buttonsPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "✓ Registrar",
                Width = 130,
                Height = 38,
                FontSize = 14,
                Background = VacationColor,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okBtn.Click += (s, ev) =>
            {
                // Validaciones
                if (employeeCombo.SelectedValue == null)
                {
                    errorText.Text = "Seleccione un empleado";
                    return;
                }
                if (!startDatePicker.SelectedDate.HasValue)
                {
                    errorText.Text = "Seleccione fecha de inicio";
                    return;
                }
                if (!endDatePicker.SelectedDate.HasValue)
                {
                    errorText.Text = "Seleccione fecha de fin";
                    return;
                }
                if (endDatePicker.SelectedDate < startDatePicker.SelectedDate)
                {
                    errorText.Text = "Fecha fin debe ser posterior a fecha inicio";
                    return;
                }

                dialog.DialogResult = true;
            };
            buttonsPanel.Children.Add(okBtn);

            mainPanel.Children.Add(buttonsPanel);
            dialog.Content = mainPanel;

            // Focus inicial
            dialog.Loaded += (s, ev) => employeeCombo.Focus();

            if (dialog.ShowDialog() == true)
            {
                return new VacationTable
                {
                    EmployeeId = (int)employeeCombo.SelectedValue,
                    StartDate = startDatePicker.SelectedDate.Value,
                    EndDate = endDatePicker.SelectedDate.Value,
                    Notes = string.IsNullOrWhiteSpace(notesTextBox.Text) ? null : notesTextBox.Text.Trim()
                };
            }

            return null;
        }

        private async void MarkAllPresent_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var sinRegistro = _currentAttendance?.Count(a => a.Status == "SIN_REGISTRO" && a.IsWorkday) ?? 0;
            if (sinRegistro == 0)
            {
                MessageBox.Show("Todos los empleados ya tienen registro.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"¿Marcar asistencia para {sinRegistro} empleados?\n\nFecha: {_selectedDate:dd/MM/yyyy}\nHora entrada: {_expectedStartTime:hh\\:mm}", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _isLoading = true;
                    StatusText.Text = "Registrando...";
                    StatusText.Foreground = PrimaryColor;

                    var count = await _attendanceService.MarkAllPresent(_selectedDate, _currentUser.Id, _expectedStartTime);

                    // Invalidar cache y recargar
                    InvalidateDateCache(_selectedDate);
                    await LoadAttendanceForDate(_selectedDate);
                    await LoadMonthlyStats();

                    StatusText.Text = $"✓ {count} empleados registrados";
                    StatusText.Foreground = AttendanceColor;
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                    StatusText.Foreground = AbsentColor;
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }
    }
}
