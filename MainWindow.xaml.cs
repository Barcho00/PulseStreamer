using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace HeartRateMonitor
{
    public class AppSettings
    {
        public Guid LastActiveProfileId { get; set; } = Guid.Empty;
        public bool ShowGrid { get; set; } = true;
        public bool AutoYScale { get; set; } = true;
        public double MaxY { get; set; } = 180;
        public bool IsUserDataExpanded { get; set; } = true;
        public bool IsChartSettingsExpanded { get; set; } = true;
        public bool IsZoneGuardExpanded { get; set; } = true;
    }

    public class HeartRatePoint
    {
        public double Time { get; set; }
        public double Bpm { get; set; }
    }

    public class Session : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private Guid _id = Guid.NewGuid();
        public Guid Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        private string _name = "";
        public string Name 
        { 
            get => _name; 
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public List<HeartRatePoint> History { get; set; } = new();

        private double _averageBpm = 0;
        public double AverageBpm
        {
            get => _averageBpm;
            set 
            { 
                _averageBpm = value; 
                OnPropertyChanged(nameof(AverageBpm)); 
                OnPropertyChanged(nameof(AvgBpmText)); 
            }
        }

        private int _minBpm = 0;
        public int MinBpm
        {
            get => _minBpm;
            set { _minBpm = value; OnPropertyChanged(nameof(MinBpm)); OnPropertyChanged(nameof(MinMaxString)); }
        }

        private int _maxBpm = 0;
        public int MaxBpm
        {
            get => _maxBpm;
            set { _maxBpm = value; OnPropertyChanged(nameof(MaxBpm)); OnPropertyChanged(nameof(MinMaxString)); }
        }

        public string MinMaxString => MinBpm > 0 ? $"{MinBpm} / {MaxBpm}" : "-- / --";

        private int _lastBpm = 0;
        public int LastBpm
        {
            get => _lastBpm;
            set { _lastBpm = value; OnPropertyChanged(nameof(LastBpm)); }
        }

        private string _durationString = "00:00:00";
        public string DurationString
        {
            get => _durationString;
            set { _durationString = value; OnPropertyChanged(nameof(DurationString)); }
        }

        private string _zoneString = "Strefa: --";
        public string ZoneString
        {
            get => _zoneString;
            set { _zoneString = value; OnPropertyChanged(nameof(ZoneString)); }
        }

        private SolidColorBrush _zoneBrush = new SolidColorBrush(Color.FromRgb(127, 140, 141));
        
        [System.Text.Json.Serialization.JsonIgnore]
        public SolidColorBrush ZoneBrush
        {
            get => _zoneBrush;
            set { _zoneBrush = value; OnPropertyChanged(nameof(ZoneBrush)); }
        }

        private DateTime? _startTime;
        public DateTime? StartTime
        {
            get => _startTime;
            set 
            { 
                _startTime = value; 
                OnPropertyChanged(nameof(StartTime)); 
                OnPropertyChanged(nameof(StartTimeText)); 
            }
        }

        public string StartTimeText => StartTime.HasValue ? StartTime.Value.ToString("dd.MM.yyyy HH:mm:ss") : "--";
        public string AvgBpmText => AverageBpm > 0 ? $"{Math.Round(AverageBpm)} BPM" : "-- BPM";

        private double _caloriesBurned = 0;
        public double CaloriesBurned
        {
            get => _caloriesBurned;
            set 
            { 
                _caloriesBurned = value; 
                OnPropertyChanged(nameof(CaloriesBurned)); 
                OnPropertyChanged(nameof(CaloriesBurnedText)); 
            }
        }

        public string CaloriesBurnedText => $"{Math.Round(CaloriesBurned)} kcal";

        public Session()
        {
        }

        public Session(string name)
        {
            Name = name;
        }
    }

    public partial class MainWindow : FluentWindow
    {
        private DispatcherTimer? _elapsedTimer;
        private DispatcherTimer? _connectionDetailsTimer;
        private DispatcherTimer? _beepIntervalTimer;
        
        private double _currentBeepIntervalSeconds = 1.0;
        private double _targetBeepIntervalSeconds = 1.0;
        private double _timeSinceLastBeep = 0.0;
        private readonly Queue<int> _recentBpms = new();

        private DateTime? _sessionStartTime;
        private DateTime? _connectionTime;
        private readonly ObservableCollection<Session> _historicalSessions = new();
        private System.Windows.Threading.DispatcherTimer _resizeTimer = new();
        private string _sessionsPath = "";
        private string _settingsPath = "";
        private AppSettings _globalSettings = new();
        
        private Session? _activeSession;
        private readonly BleHeartRateService _bleService = new();
        private readonly ObservableCollection<DiscoveredDevice> _discoveredDevices = new();

        // Profile Manager
        private readonly ProfileManager _profileManager = new();

        // Control flags
        private bool _isLoadingSettings = false;
        private bool _isTrainingActive = false;
        private int _pointsCollected = 0;
        private int _lastConnectedRssi = 0;
        private string _lastConnectedName = "";
        private string _lastConnectedAddress = "";
        private bool _autoConnectAttempted = false;
        private bool _isAutoConnecting = false;
        private DateTime _lastBeepTickTime = DateTime.Now;
        private DateTime? _lastHeartRateTime;

        private bool _hasReceivedFirstPoint = false;
        private double _windowDurationSeconds = 60;
        private bool _isAutoYScale = true;
        private bool _isBeepEnabled = false;

        // Training stats
        private readonly Dictionary<int, int> _zoneDurations = new()
        {
            { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
        };
        private int _zoneGuardElapsedSeconds = 0;

        public MainWindow()
        {
            _isLoadingSettings = true;
            InitializeComponent();

            // Initialize centralized file logger
            AppLogger.Initialize();
            AppLogger.Info("App", "=== Application starting (Refactored) ===");
            
            // Setup WPF.UI Theme
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            // Initialize Sound System
            SoundManager.Initialize();

            // Bind UI lists
            ListDevices.ItemsSource = _discoveredDevices;
            // Wire up BLE service events
            _bleService.DeviceDiscovered += OnDeviceDiscovered;
            _bleService.HeartRateChanged += OnHeartRateChanged;
            _bleService.BatteryLevelChanged += OnBatteryLevelChanged;
            _bleService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _bleService.ErrorOccurred += OnErrorOccurred;

            _sessionsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions");
            System.IO.Directory.CreateDirectory(_sessionsPath);
            AppLogger.Info("App", $"Sessions path: {_sessionsPath}");

            InitTimer();

            LoadSessionsFromDisk();

            // Load Profiles and settings
            _profileManager.LoadProfiles();
            ListProfiles.ItemsSource = _profileManager.Profiles;

            LoadGlobalSettings();

            // Select active profile
            var profileToSelect = _profileManager.Profiles.FirstOrDefault(p => p.Id == _globalSettings.LastActiveProfileId) 
                                 ?? _profileManager.Profiles.FirstOrDefault();
            if (profileToSelect != null)
            {
                ListProfiles.SelectedItem = profileToSelect;
            }

            _isLoadingSettings = false;

            Loaded += (s, e) =>
            {
                AppLogger.Info("App", "Window Loaded event fired");
                
                string selectedActivityLoad = "Trening";
                if (ComboActivity.SelectedItem is ComboBoxItem itemLoad && itemLoad.Tag != null)
                {
                    selectedActivityLoad = itemLoad.Tag.ToString() ?? "Trening";
                }
                _activeSession = new Session($"{selectedActivityLoad} {DateTime.Now:dd.MM.yyyy HH:mm}");
                this.DataContext = _activeSession;

                DrawCharts();

                // Trigger auto connect if enabled
                var activeProfile = _profileManager.ActiveProfile;
                if (activeProfile != null && CheckAutoConnect.IsChecked == true && !string.IsNullOrEmpty(activeProfile.LastDeviceAddress))
                {
                    AppLogger.Info("App", $"Auto-reconnect enabled for profile '{activeProfile.Name}' -> scanning for: {activeProfile.LastDeviceName} [{activeProfile.LastDeviceAddress}]");
                    _discoveredDevices.Clear();
                    _bleService.StartScanning();
                }
            };
        }

        private void InitTimer()
        {
            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _elapsedTimer.Tick += ElapsedTimer_Tick;

            _connectionDetailsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _connectionDetailsTimer.Tick += (sender, e) =>
            {
                if (_connectionTime.HasValue)
                {
                    var elapsed = DateTime.Now - _connectionTime.Value;
                    TxtConnectionDuration.Text = elapsed.ToString(@"hh\:mm\:ss");
                }
            };

            _beepIntervalTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _beepIntervalTimer.Interval = TimeSpan.FromMilliseconds(20);
            _beepIntervalTimer.Tick += BeepIntervalTimer_Tick;
        }

        private void ElapsedTimer_Tick(object? sender, EventArgs e)
        {
            if (_isTrainingActive && _sessionStartTime.HasValue && _activeSession != null)
            {
                var elapsed = DateTime.Now - _sessionStartTime.Value;
                _activeSession.DurationString = elapsed.ToString(@"hh\:mm\:ss");

                var activeProfile = _profileManager.ActiveProfile;
                if (activeProfile != null)
                {
                    // Goal Progress Update
                    if (activeProfile.GoalType != "Brak")
                    {
                        if (CardGoalProgress.Visibility != Visibility.Visible)
                            CardGoalProgress.Visibility = Visibility.Visible;

                        double progress = 0;
                        if (activeProfile.GoalType == "Kalorie")
                        {
                            progress = _activeSession.CaloriesBurned / activeProfile.TargetCalories * 100;
                            TxtGoalStatus.Text = $"{Math.Round(progress)}% ({Math.Round(_activeSession.CaloriesBurned)} / {activeProfile.TargetCalories} kcal)";
                        }
                        else if (activeProfile.GoalType == "Czas")
                        {
                            double targetSeconds = activeProfile.TargetDurationMinutes * 60;
                            progress = elapsed.TotalSeconds / targetSeconds * 100;
                            TxtGoalStatus.Text = $"{Math.Round(progress)}% ({Math.Round(elapsed.TotalMinutes, 1)} / {activeProfile.TargetDurationMinutes} min)";
                        }

                        ProgressGoal.Value = Math.Min(100, Math.Max(0, progress));
                        
                        if (progress >= 100 && ProgressGoal.Foreground != Brushes.LimeGreen)
                        {
                            ProgressGoal.Foreground = Brushes.LimeGreen;
                            SoundManager.PlayHeartbeat(); // Simple alert that goal is reached
                        }
                        else if (progress < 100 && ProgressGoal.Foreground == Brushes.LimeGreen)
                        {
                            ProgressGoal.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                        }
                    }
                    else
                    {
                        if (CardGoalProgress.Visibility != Visibility.Collapsed)
                            CardGoalProgress.Visibility = Visibility.Collapsed;
                    }

                    // Update Zone stats (seconds in zones)
                    int currentZone = FitnessCalculator.GetHeartRateZone(_activeSession.LastBpm, activeProfile.Age);
                    if (currentZone >= 1 && currentZone <= 5)
                    {
                        _zoneDurations[currentZone]++;
                    }

                    // Zone Guard (Assistant) checking
                    if (activeProfile.ZoneGuardEnabled)
                    {
                        _zoneGuardElapsedSeconds++;
                        if (_zoneGuardElapsedSeconds >= activeProfile.ZoneGuardCheckIntervalSeconds)
                        {
                            _zoneGuardElapsedSeconds = 0;
                            CheckZoneGuard(activeProfile);
                        }
                    }
                }
            }
        }

        private void CheckZoneGuard(UserProfile profile)
        {
            if (_activeSession == null || _activeSession.LastBpm <= 0) return;

            int currentBpm = _activeSession.LastBpm;
            int targetZone = profile.ZoneGuardTargetZone;
            
            var limits = FitnessCalculator.GetZoneLimits(targetZone, profile.Age);
            
            if (currentBpm < limits.Low)
            {
                AppLogger.Info("ZoneGuard", $"HR {currentBpm} < target zone limits {limits.Low}-{limits.High} (Zone {targetZone}). Alert: Accelerate.");
                SoundManager.PlayAccelerate();
            }
            else if (currentBpm > limits.High)
            {
                AppLogger.Info("ZoneGuard", $"HR {currentBpm} > target zone limits {limits.Low}-{limits.High} (Zone {targetZone}). Alert: Decelerate.");
                SoundManager.PlayDecelerate();
            }
            else
            {
                AppLogger.Debug("ZoneGuard", $"HR {currentBpm} is within target zone limits {limits.Low}-{limits.High} (Zone {targetZone}).");
            }
        }

        private void BeepIntervalTimer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            double elapsed = (now - _lastBeepTickTime).TotalSeconds;
            _lastBeepTickTime = now;

            if (_targetBeepIntervalSeconds <= 0) return;

            // Interpolate current interval towards target for smooth audio rhythm
            double lerpFactor = Math.Min(1.0, elapsed * 2.5);
            _currentBeepIntervalSeconds += (_targetBeepIntervalSeconds - _currentBeepIntervalSeconds) * lerpFactor;

            _timeSinceLastBeep += elapsed;

            if (_timeSinceLastBeep >= _currentBeepIntervalSeconds)
            {
                _timeSinceLastBeep = 0.0;
                TriggerBeepAndPulse();
            }
        }

        private void TriggerBeepAndPulse()
        {
            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile != null && _globalSettings.MaxY > 0) // Just check global scale to ensure loaded
            {
                // We use dynamic settings or active profile preferred settings
                if (ToggleSound.IsChecked == true)
                {
                    SoundManager.PlayHeartbeat();
                }
            }

            PulseHeartIcon();
        }

        private void PulseHeartIcon()
        {
            if (HeartScaleTransform == null) return;

            var pulseAnimation = new DoubleAnimation
            {
                From = 1.25,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            HeartScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            HeartScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);
        }

        #region User Profile Event Handlers

        private void ListProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListProfiles.SelectedItem is UserProfile selectedProfile)
            {
                AppLogger.Info("UI", $"Profile selected: {selectedProfile.Name}");
                _profileManager.ActiveProfileId = selectedProfile.Id;
                _globalSettings.LastActiveProfileId = selectedProfile.Id;
                
                SaveGlobalSettings();
                ApplyProfileToUI(selectedProfile);

                // Try auto-reconnect to profile last device
                if (CheckAutoConnect.IsChecked == true && !string.IsNullOrEmpty(selectedProfile.LastDeviceAddress))
                {
                    if (TxtStatus.Text != "Connected" && !_isAutoConnecting)
                    {
                        AppLogger.Info("UI", $"Auto-connecting to saved device: {selectedProfile.LastDeviceName} [{selectedProfile.LastDeviceAddress}]");
                        _discoveredDevices.Clear();
                        _bleService.StartScanning();
                    }
                }
            }
        }

        private void ApplyProfileToUI(UserProfile profile)
        {
            _isLoadingSettings = true;
            try
            {
                SliderAge.Value = profile.Age;
                SliderWeight.Value = profile.Weight;
                
                // Select GoalType
                foreach (ComboBoxItem item in ComboGoalType.Items)
                {
                    if (item.Tag?.ToString() == profile.GoalType)
                    {
                        ComboGoalType.SelectedItem = item;
                        break;
                    }
                }
                
                if (profile.GoalType == "Kalorie")
                {
                    SliderGoalValue.Value = profile.TargetCalories;
                }
                else if (profile.GoalType == "Czas")
                {
                    SliderGoalValue.Value = profile.TargetDurationMinutes;
                }
                
                // Select gender in ComboGender
                foreach (ComboBoxItem item in ComboGender.Items)
                {
                    if (item.Tag?.ToString() == profile.Gender)
                    {
                        ComboGender.SelectedItem = item;
                        break;
                    }
                }

                // Select activity in ComboActivity
                foreach (ComboBoxItem item in ComboActivity.Items)
                {
                    if (item.Tag?.ToString() == profile.PreferredActivity)
                    {
                        ComboActivity.SelectedItem = item;
                        break;
                    }
                }

                // Select Target Zone in ComboTargetZone
                foreach (ComboBoxItem item in ComboTargetZone.Items)
                {
                    if (item.Tag?.ToString() == profile.ZoneGuardTargetZone.ToString())
                    {
                        ComboTargetZone.SelectedItem = item;
                        break;
                    }
                }

                // Select Check Interval in ComboCheckInterval
                foreach (ComboBoxItem item in ComboCheckInterval.Items)
                {
                    if (item.Tag?.ToString() == profile.ZoneGuardCheckIntervalSeconds.ToString())
                    {
                        ComboCheckInterval.SelectedItem = item;
                        break;
                    }
                }

                ToggleZoneGuard.IsChecked = profile.ZoneGuardEnabled;

                // Sync live screen properties if not active
                if (!_isTrainingActive && _activeSession != null)
                {
                    string selectedActivity = "Trening";
                    if (ComboActivity.SelectedItem is ComboBoxItem item && item.Tag != null)
                    {
                        selectedActivity = item.Tag.ToString() ?? "Trening";
                    }
                    _activeSession.Name = $"{selectedActivity} {DateTime.Now:dd.MM.yyyy HH:mm}";
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("UI", "Failed to apply profile to UI", ex);
            }
            finally
            {
                _isLoadingSettings = false;
            }



            DrawCharts();
        }

        private void SaveActiveProfileSettings()
        {
            if (_isLoadingSettings) return;

            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile == null) return;

            try
            {
                activeProfile.Age = (int)SliderAge.Value;
                activeProfile.Weight = SliderWeight.Value;
                
                if (ComboGoalType.SelectedItem is ComboBoxItem goalItem)
                {
                    activeProfile.GoalType = goalItem.Tag?.ToString() ?? "Brak";
                }
                
                if (activeProfile.GoalType == "Kalorie")
                {
                    activeProfile.TargetCalories = (int)SliderGoalValue.Value;
                }
                else if (activeProfile.GoalType == "Czas")
                {
                    activeProfile.TargetDurationMinutes = (int)SliderGoalValue.Value;
                }

                if (ComboGender.SelectedItem is ComboBoxItem genderItem)
                {
                    activeProfile.Gender = genderItem.Tag?.ToString() ?? "Male";
                }

                if (ComboActivity.SelectedItem is ComboBoxItem activityItem)
                {
                    activeProfile.PreferredActivity = activityItem.Tag?.ToString() ?? "Bieżnia";
                }

                activeProfile.ZoneGuardEnabled = ToggleZoneGuard.IsChecked == true;

                if (ComboTargetZone.SelectedItem is ComboBoxItem targetZoneItem && int.TryParse(targetZoneItem.Tag?.ToString(), out int tz))
                {
                    activeProfile.ZoneGuardTargetZone = tz;
                }

                if (ComboCheckInterval.SelectedItem is ComboBoxItem checkIntervalItem && int.TryParse(checkIntervalItem.Tag?.ToString(), out int ci))
                {
                    activeProfile.ZoneGuardCheckIntervalSeconds = ci;
                }

                _profileManager.SaveProfiles();
                AppLogger.Debug("Profiles", $"Saved profile settings for: {activeProfile.Name}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Profiles", "Error saving profile parameters", ex);
            }
        }

        private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            PanelAddProfile.Visibility = PanelAddProfile.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            TxtNewProfileName.Text = "";
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile == null) return;

            if (_profileManager.Profiles.Count <= 1)
            {
                System.Windows.MessageBox.Show("Nie można usunąć jedynego profilu.", "Usuwanie Profilu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Czy na pewno chcesz usunąć profil „{activeProfile.Name}” oraz wszystkie powiązane z nim ustawienia?",
                "Usuwanie Profilu",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _profileManager.DeleteProfile(activeProfile.Id);
                ListProfiles.SelectedItem = _profileManager.Profiles.FirstOrDefault();
            }
        }

        private void BtnSaveNewProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtNewProfileName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                System.Windows.MessageBox.Show("Nazwa profilu nie może być pusta.", "Błąd", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var newProfile = _profileManager.AddProfile(name, 30, 75, "Male");
            PanelAddProfile.Visibility = Visibility.Collapsed;
            ListProfiles.SelectedItem = newProfile;
        }

        private void BtnCancelNewProfile_Click(object sender, RoutedEventArgs e)
        {
            PanelAddProfile.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Global Settings

        private void LoadGlobalSettings()
        {
            AppLogger.Info("Settings", "Loading global settings...");
            try
            {
                _settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath, System.Text.Encoding.UTF8);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        _globalSettings = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Settings", "Failed to load settings.json", ex);
            }

            ApplyGlobalSettingsToUI();
        }

        private void ApplyGlobalSettingsToUI()
        {
            _isLoadingSettings = true;
            try
            {
                ToggleGrid.IsChecked = _globalSettings.ShowGrid;
                ToggleYScale.IsChecked = _globalSettings.AutoYScale;
                SliderMaxY.Value = _globalSettings.MaxY;
                _isAutoYScale = _globalSettings.AutoYScale;

                if (ManualYGrid != null)
                {
                    ManualYGrid.Visibility = _isAutoYScale ? Visibility.Collapsed : Visibility.Visible;
                }

                if (ExpanderUserData != null) ExpanderUserData.IsExpanded = _globalSettings.IsUserDataExpanded;
                if (ExpanderChartSettings != null) ExpanderChartSettings.IsExpanded = _globalSettings.IsChartSettingsExpanded;
                if (ExpanderZoneGuard != null) ExpanderZoneGuard.IsExpanded = _globalSettings.IsZoneGuardExpanded;
            }
            catch { }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private DispatcherTimer? _globalSaveTimer;

        private void SaveGlobalSettings()
        {
            if (_globalSaveTimer == null)
            {
                _globalSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _globalSaveTimer.Tick += (s, e) =>
                {
                    _globalSaveTimer.Stop();
                    ForceSaveGlobalSettings();
                };
            }
            _globalSaveTimer.Stop();
            _globalSaveTimer.Start();
        }

        private void ForceSaveGlobalSettings()
        {
            try
            {
                if (string.IsNullOrEmpty(_settingsPath))
                {
                    _settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                }
                
                _globalSettings.ShowGrid = ToggleGrid.IsChecked == true;
                _globalSettings.AutoYScale = ToggleYScale.IsChecked == true;
                _globalSettings.MaxY = SliderMaxY.Value;

                if (ExpanderUserData != null) _globalSettings.IsUserDataExpanded = ExpanderUserData.IsExpanded;
                if (ExpanderChartSettings != null) _globalSettings.IsChartSettingsExpanded = ExpanderChartSettings.IsExpanded;
                if (ExpanderZoneGuard != null) _globalSettings.IsZoneGuardExpanded = ExpanderZoneGuard.IsExpanded;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_globalSettings, options);
                File.WriteAllText(_settingsPath, json, System.Text.Encoding.UTF8);
                AppLogger.Debug("Settings", "settings.json updated on disk");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Settings", "Failed to save settings.json", ex);
            }
        }

                private void BtnOpenHistory_Click(object sender, RoutedEventArgs e)
        {
            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile != null)
            {
                var historyWindow = new HistoryWindow(activeProfile, _historicalSessions);
                historyWindow.Owner = this;
                historyWindow.Show();
            }
            else
            {
                System.Windows.MessageBox.Show("Proszę najpierw wybrać profil użytkownika z listy po lewej stronie.", "Brak profilu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void Expander_StateChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoadingSettings)
            {
                SaveGlobalSettings();
            }
        }

        #endregion

        #region Training Lifecycle (Rozpocznij / Zakończ)

        private async void BtnTrainingControl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isTrainingActive)
                {
                    StartTraining();
                }
                else
                {
                    await StopTrainingAsync();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Training", "Error in BtnTrainingControl_Click", ex);
            }
        }

        private void StartTraining()
        {
            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile == null) return;

            AppLogger.Info("Training", "StartTraining initiated");
            _isTrainingActive = true;

            // Initialize training statistics
            foreach (var key in _zoneDurations.Keys.ToList())
            {
                _zoneDurations[key] = 0;
            }
            _zoneGuardElapsedSeconds = 0;

            // Create new active session
            string selectedActivity = "Trening";
            if (ComboActivity.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                selectedActivity = item.Tag.ToString() ?? "Trening";
            }
            string defaultName = $"{selectedActivity} {DateTime.Now:dd.MM.yyyy HH:mm}";
            _activeSession = new Session(defaultName)
            {
                StartTime = DateTime.Now,
                DurationString = "00:00:00",
                CaloriesBurned = 0,
                AverageBpm = 0,
                MinBpm = 0,
                MaxBpm = 0
            };
            this.DataContext = _activeSession;

            _lastHeartRateTime = null;
            _sessionStartTime = DateTime.Now;
            _elapsedTimer?.Start();
            _hasReceivedFirstPoint = false;
            _pointsCollected = 0;

            // Change UI Button State
            BtnTrainingControl.Content = "Zakończ Trening";
            BtnTrainingControl.Appearance = ControlAppearance.Danger;
            BtnTrainingControl.Icon = new SymbolIcon { Symbol = SymbolRegular.Stop24 };

            // Update Streamer Status
            UpdateStreamer();

            DrawCharts();
            AppLogger.Info("Training", "Training session recording started.");
        }

        private async Task StopTrainingAsync()
        {
            if (!_isTrainingActive || _activeSession == null) return;

            try
            {
                AppLogger.Info("Training", "StopTraining initiated");
                _isTrainingActive = false;
                _elapsedTimer?.Stop();

                // Store active session stats
                string finalName = _activeSession.Name;
                string finalDuration = _activeSession.DurationString;
                double finalCalories = _activeSession.CaloriesBurned;
                double finalAvg = _activeSession.AverageBpm;
                double finalMax = _activeSession.MaxBpm;

                // Save session to disk asynchronously
                var sessionToSave = _activeSession;
                await SaveSessionToDiskAsync(sessionToSave);

                if (sessionToSave.History.Count > 0 && !_historicalSessions.Contains(sessionToSave))
                {
                    _historicalSessions.Insert(0, sessionToSave);
                }

                // Show summary Dialog Custom Window
                var summaryDialog = new WorkoutSummaryWindow(
                    finalName,
                    finalDuration,
                    finalCalories,
                    finalAvg,
                    finalMax,
                    new Dictionary<int, int>(_zoneDurations)
                )
                {
                    Owner = this
                };

                // Reset UI Control Button
                BtnTrainingControl.Content = "Rozpocznij Trening";
                BtnTrainingControl.Appearance = ControlAppearance.Success;
                BtnTrainingControl.Icon = new SymbolIcon { Symbol = SymbolRegular.Play24 };

                // Stop beeps
                _beepIntervalTimer?.Stop();
                _targetBeepIntervalSeconds = 0.0;
                _recentBpms.Clear();

                // Reset timing for the new preview session
                _hasReceivedFirstPoint = false;
                _sessionStartTime = null;

                CardGoalProgress.Visibility = Visibility.Collapsed;

                // Re-instantiate placeholder preview session
                string selectedActivityEnd = "Trening";
                if (ComboActivity != null && ComboActivity.SelectedItem is ComboBoxItem itemEnd && itemEnd.Tag != null)
                {
                    selectedActivityEnd = itemEnd.Tag.ToString() ?? "Trening";
                }
                _activeSession = new Session($"{selectedActivityEnd} {DateTime.Now:dd.MM.yyyy HH:mm}");
                this.DataContext = _activeSession;

                // Update Streamer Status
                UpdateStreamer();

                DrawCharts();

                // Show the Dialog Modal
                summaryDialog.ShowDialog();
                
                AppLogger.Info("Training", "Training session stopped and saved.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Training", "Error during StopTrainingAsync", ex);
            }
        }

        #endregion

        #region BLE Event Handlers

        private void OnDeviceDiscovered(DiscoveredDevice device)
        {
            Dispatcher.Invoke(async () =>
            {
                if (!_discoveredDevices.Any(d => d.Address == device.Address))
                {
                    _discoveredDevices.Add(device);
                    AppLogger.Info("UI", $"Device added to list: {device.Name} [{device.Address}]");

                    // Auto-reconnect flow
                    var activeProfile = _profileManager.ActiveProfile;
                    if (activeProfile != null && CheckAutoConnect.IsChecked == true && 
                        !_autoConnectAttempted &&
                        !string.IsNullOrEmpty(activeProfile.LastDeviceAddress) && 
                        device.AddressString == activeProfile.LastDeviceAddress && 
                        _bleService.IsScanning)
                    {
                        AppLogger.Info("UI", $"Auto-reconnect match found! Connecting to {device.Name} [{device.Address}]");
                        _autoConnectAttempted = true;
                        _isAutoConnecting = true;

                        _bleService.StopScanning();

                        _lastConnectedRssi = device.Rssi;
                        _lastConnectedName = device.Name;
                        _lastConnectedAddress = device.AddressString;

                        ListDevices.SelectedItem = device;
                        ListDevices.IsEnabled = false;
                        BtnScan.IsEnabled = false;

                        bool success = await _bleService.ConnectAsync(device.Address);
                        AppLogger.Info("UI", $"Auto-reconnect result: success={success}");

                        ListDevices.IsEnabled = true;
                        BtnScan.IsEnabled = !success;
                        _isAutoConnecting = false;

                        if (!success)
                        {
                            AppLogger.Warn("UI", "Auto-reconnect failed — resuming scan");
                            ListDevices.SelectedItem = null;
                            _bleService.StartScanning();
                        }
                    }
                }
            });
        }

        private void OnHeartRateChanged(int bpm)
        {
            Dispatcher.Invoke(async () =>
            {
                if (_activeSession == null) return;

                _activeSession.LastBpm = bpm;

                if (bpm > 0)
                {
                    _recentBpms.Enqueue(bpm);
                    if (_recentBpms.Count > 2)
                    {
                        _recentBpms.Dequeue();
                    }
                    double avgBpm = _recentBpms.Average();
                    _targetBeepIntervalSeconds = 60.0 / avgBpm;

                    if (_beepIntervalTimer != null)
                    {
                        if (!_beepIntervalTimer.IsEnabled)
                        {
                            _timeSinceLastBeep = 0.0;
                            _lastBeepTickTime = DateTime.Now;
                            _currentBeepIntervalSeconds = _targetBeepIntervalSeconds;
                            _beepIntervalTimer.Start();
                        }
                    }
                }
                else
                {
                    _targetBeepIntervalSeconds = 0.0;
                    _beepIntervalTimer?.Stop();
                    _recentBpms.Clear();
                }

                // ALWAYS record points to the active session (preview or training)
                if (!_hasReceivedFirstPoint)
                {
                    _hasReceivedFirstPoint = true;
                    _sessionStartTime = DateTime.Now;
                }

                if (_sessionStartTime.HasValue)
                {
                    double elapsed = (DateTime.Now - _sessionStartTime.Value).TotalSeconds;
                    _activeSession.History.Add(new HeartRatePoint { Time = elapsed, Bpm = bpm });

                    // Capping preview session history to prevent memory leaks if left for hours
                    if (!_isTrainingActive && _activeSession.History.Count > 1000)
                    {
                        _activeSession.History.RemoveAt(0);
                    }
                }

                UpdateHeartRateZoneForSession(_activeSession, bpm);

                // If training is active, count statistics
                if (_isTrainingActive)
                {
                    _pointsCollected++;
                    TxtPointsCollected.Text = _pointsCollected.ToString();

                    if (_sessionStartTime.HasValue)
                    {
                        // Calories calculation
                        double hrElapsed = 1.0;
                        if (_lastHeartRateTime.HasValue)
                        {
                            hrElapsed = (DateTime.Now - _lastHeartRateTime.Value).TotalSeconds;
                            if (hrElapsed > 5.0) hrElapsed = 5.0; // clamp jump
                        }
                        
                        var activeProfile = _profileManager.ActiveProfile;
                        if (activeProfile != null)
                        {
                            double calBurned = FitnessCalculator.CalculateCaloriesBurned(
                                bpm, 
                                activeProfile.Weight, 
                                activeProfile.Age, 
                                activeProfile.Gender == "Male", 
                                activeProfile.PreferredActivity, 
                                hrElapsed
                            );
                            _activeSession.CaloriesBurned += calBurned;
                        }

                        // Update session averages
                        var bpmValues = _activeSession.History.Select(x => x.Bpm).ToList();
                        if (bpmValues.Any())
                        {
                            _activeSession.AverageBpm = Math.Round(bpmValues.Average());
                            _activeSession.MinBpm = (int)Math.Round(bpmValues.Min());
                            _activeSession.MaxBpm = (int)Math.Round(bpmValues.Max());
                        }

                        // Async saving every 10 samples
                        if (_pointsCollected % 10 == 0)
                        {
                            await SaveSessionToDiskAsync(_activeSession);
                        }
                    }
                }

                _lastHeartRateTime = DateTime.Now;
                
                DrawCharts();

                // Push telemetry to streaming pipeline
                UpdateStreamer();
            });
        }

        private void UpdateHeartRateZoneForSession(Session session, int bpm)
        {
            var activeProfile = _profileManager.ActiveProfile;
            int age = activeProfile?.Age ?? 30;

            int zone = FitnessCalculator.GetHeartRateZone(bpm, age);
            string zoneName = FitnessCalculator.GetZoneName(zone);
            Color zoneColor = FitnessCalculator.GetZoneColor(zone);
            
            var limits = FitnessCalculator.GetZoneLimits(zone, age);

            if (zone >= 1)
            {
                session.ZoneString = $"Strefa: {zoneName} (Strefa {zone}: {limits.Low}-{limits.High} BPM)";
            }
            else
            {
                session.ZoneString = $"Strefa: Odpoczynek (< {limits.High} BPM)";
            }

            session.ZoneBrush = new SolidColorBrush(zoneColor);
        }

        private void OnBatteryLevelChanged(int batteryLevel)
        {
            Dispatcher.Invoke(() =>
            {
                TxtBattery.Text = $"Bateria: {batteryLevel}%";
                if (batteryLevel > 80)
                    IconBattery.Symbol = SymbolRegular.Battery1024;
                else if (batteryLevel > 50)
                    IconBattery.Symbol = SymbolRegular.Battery524;
                else if (batteryLevel > 20)
                    IconBattery.Symbol = SymbolRegular.Battery224;
                else
                    IconBattery.Symbol = SymbolRegular.Battery024;
            });
        }

        private void OnConnectionStatusChanged(string status)
        {
            AppLogger.Info("UI", $"OnConnectionStatusChanged: {status}");
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = status;

                if (status == "Connected")
                {
                    AppLogger.Info("UI", $"Connected — device: {_lastConnectedName} [{_lastConnectedAddress}]");
                    ElStatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 124, 65)); // Green
                    BtnDisconnect.IsEnabled = true;
                    BtnScan.IsEnabled = false;
                    ScanProgress.Visibility = Visibility.Collapsed;
                    BtnScan.Content = "Skanuj";
                    BtnScan.Icon = new SymbolIcon { Symbol = SymbolRegular.Bluetooth24 };

                    // Display details
                    TxtDeviceName.Text = _lastConnectedName;
                    TxtDeviceRssi.Text = $"{_lastConnectedRssi} dBm";
                    _pointsCollected = 0;
                    TxtPointsCollected.Text = "0";
                    _connectionTime = DateTime.Now;
                    TxtConnectionDuration.Text = "00:00:00";
                    _connectionDetailsTimer?.Start();
                    PanelConnectionDetails.Visibility = Visibility.Visible;

                    // Toggle card visibilities
                    CardScan.Visibility = Visibility.Collapsed;
                    CardDeviceList.Visibility = Visibility.Collapsed;
                    CardStatus.Visibility = Visibility.Visible;

                    _autoConnectAttempted = false; // Reset for recovery

                    // Save last connected device to active profile
                    var activeProfile = _profileManager.ActiveProfile;
                    if (activeProfile != null && !_isLoadingSettings && !string.IsNullOrEmpty(_lastConnectedAddress))
                    {
                        activeProfile.LastDeviceAddress = _lastConnectedAddress;
                        activeProfile.LastDeviceName = _lastConnectedName;
                        _profileManager.SaveProfiles();
                    }
                }
                else if (status == "Disconnected")
                {
                    AppLogger.Info("UI", "Disconnected — cleaning up UI state");
                    ElStatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)); // Red
                    BtnDisconnect.IsEnabled = false;
                    BtnScan.IsEnabled = true;
                    _sessionStartTime = null;
                    _hasReceivedFirstPoint = false;

                    _connectionDetailsTimer?.Stop();
                    PanelConnectionDetails.Visibility = Visibility.Collapsed;

                    // Stop current training if disconnected
                    if (_isTrainingActive)
                    {
                        _ = StopTrainingAsync();
                    }

                    _beepIntervalTimer?.Stop();
                    _targetBeepIntervalSeconds = 0.0;
                    _recentBpms.Clear();

                    // Toggle card visibilities
                    CardScan.Visibility = Visibility.Visible;
                    CardDeviceList.Visibility = Visibility.Visible;
                    CardStatus.Visibility = Visibility.Collapsed;

                    DrawCharts();
                }
                else if (status == "Połączenie (Coast Mode)")
                {
                    AppLogger.Warn("UI", "Entering Coast Mode in UI status");
                    // Yellow/Orange dot to indicate Coast Mode signal loss
                    ElStatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 196, 15)); 
                    TxtStatus.Text = "Coast Mode (Słaby sygnał)";
                }
                else if (status == "Scanning...")
                {
                    AppLogger.Info("UI", "Scanning started");
                    ElStatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)); // Blue
                    BtnScan.Content = "Zatrzymaj";
                    BtnScan.Icon = new SymbolIcon { Symbol = SymbolRegular.Stop24 };
                    ScanProgress.Visibility = Visibility.Visible;
                    _discoveredDevices.Clear();

                    // Toggle card visibilities
                    CardScan.Visibility = Visibility.Visible;
                    CardDeviceList.Visibility = Visibility.Visible;
                    CardStatus.Visibility = Visibility.Collapsed;
                }
                else if (status == "Scan Stopped")
                {
                    AppLogger.Info("UI", "Scan stopped");
                    ElStatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)); // Red
                    BtnScan.Content = "Skanuj";
                    BtnScan.Icon = new SymbolIcon { Symbol = SymbolRegular.Bluetooth24 };
                    ScanProgress.Visibility = Visibility.Collapsed;

                    // Toggle card visibilities
                    CardScan.Visibility = Visibility.Visible;
                    CardDeviceList.Visibility = Visibility.Visible;
                    CardStatus.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void OnErrorOccurred(string error)
        {
            AppLogger.Error("UI", $"BLE error: {error} (isAutoConnecting={_isAutoConnecting})");
            Dispatcher.Invoke(() =>
            {
                if (_isAutoConnecting)
                {
                    TxtStatus.Text = $"Błąd autopołączenia: {error}";
                    ElStatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)); // Red
                    return;
                }
                System.Windows.MessageBox.Show(error, "Błąd BLE", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }

        #endregion

        #region Chart Drawing Methods

        private void DrawCharts()
        {
            if (_activeSession == null) return;
            
            var activeProfile = _profileManager.ActiveProfile;
            int age = activeProfile?.Age ?? 30;

            ChartRenderer.DrawChart(
                ChartCanvasLive,
                _activeSession?.History ?? new List<HeartRatePoint>(),
                _windowDurationSeconds,
                _isAutoYScale,
                SliderMaxY != null ? SliderMaxY.Value : 180,
                ToggleGrid.IsChecked == true,
                age,
                true
            );
        }

        private void BtnExportCsvLive_Click(object sender, RoutedEventArgs e)
        {
            ExportSessionToCsv(_activeSession);
        }

        

        private void UpdateStreamer()
        {
            if (_activeSession == null) return;

            string zoneColorHex = "#FF3B30";
            var activeProfile = _profileManager.ActiveProfile;
            int age = activeProfile?.Age ?? 30;

            int currentZone = FitnessCalculator.GetHeartRateZone(_activeSession.LastBpm, age);
            Color c = FitnessCalculator.GetZoneColor(currentZone);
            zoneColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

            LiveHeartRateStreamer.Update(
                _activeSession.LastBpm,
                currentZone,
                FitnessCalculator.GetZoneName(currentZone),
                zoneColorHex,
                _isTrainingActive,
                _activeSession.CaloriesBurned,
                _activeSession.DurationString
            );
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            AppLogger.Info("App", "=== Application closing ===");
            _bleService.Disconnect();
            
            if (_isTrainingActive && _activeSession != null)
            {
                SaveSessionToDiskAsync(_activeSession).Wait();
            }
            
            _profileManager.ForceSaveProfiles();
            ForceSaveGlobalSettings();
            base.OnClosed(e);
        }
        private void SliderDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingSettings) return;
            _windowDurationSeconds = (int)e.NewValue;
            SaveGlobalSettings();
            DrawCharts();
        }

        private void ComboGoalType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var selected = (ComboBoxItem)ComboGoalType.SelectedItem;
            if (_profileManager.ActiveProfile != null && selected != null)
            {
                _profileManager.ActiveProfile.GoalType = selected.Tag?.ToString() ?? "Brak";
                SaveActiveProfileSettings();
            }
        }

        private void SliderGoalValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingSettings) return;
            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile != null)
            {
                if (activeProfile.GoalType == "Kalorie") activeProfile.TargetCalories = (int)e.NewValue;
                else if (activeProfile.GoalType == "Czas") activeProfile.TargetDurationMinutes = (int)e.NewValue;
                SaveActiveProfileSettings();
            }
        }

        private void ToggleGrid_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            SaveGlobalSettings();
            DrawCharts();
        }

        private void ToggleYScale_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            _isAutoYScale = ToggleYScale.IsChecked == true;
            if (SliderMaxY != null) SliderMaxY.IsEnabled = !_isAutoYScale;
            SaveGlobalSettings();
            DrawCharts();
        }

        private void SliderMaxY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingSettings || _isAutoYScale) return;
            DrawCharts();
            SaveGlobalSettings();
        }

        private void ToggleSound_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            SaveGlobalSettings();
        }

        private void ToggleZoneGuard_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var activeProfile = _profileManager.ActiveProfile;
            if (activeProfile != null) {
                activeProfile.ZoneGuardEnabled = ToggleZoneGuard.IsChecked == true;
                SaveActiveProfileSettings();
            }
        }

        private void ComboTargetZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var activeProfile = _profileManager.ActiveProfile;
            var item = ComboTargetZone.SelectedItem as ComboBoxItem;
            if (activeProfile != null && item != null && int.TryParse(item.Tag?.ToString(), out int z))
            {
                activeProfile.ZoneGuardTargetZone = z;
                SaveActiveProfileSettings();
            }
        }

        private void ComboCheckInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var activeProfile = _profileManager.ActiveProfile;
            var item = ComboCheckInterval.SelectedItem as ComboBoxItem;
            if (activeProfile != null && item != null && int.TryParse(item.Tag?.ToString(), out int s))
            {
                activeProfile.ZoneGuardCheckIntervalSeconds = s;
                SaveActiveProfileSettings();
            }
        }

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (BtnToggleSidebar.IsChecked == true)
            {
                LeftPanelGrid.Visibility = Visibility.Visible;
                IconToggleSidebar.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronLeft24;
            }
            else
            {
                LeftPanelGrid.Visibility = Visibility.Collapsed;
                IconToggleSidebar.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24;
            }
        }

        private void TxtActiveSessionName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_activeSession != null)
            {
                _activeSession.Name = TxtActiveSessionName.Text;
            }
        }

        private void ComboActivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var item = ComboActivity.SelectedItem as ComboBoxItem;
            if (item != null && _profileManager.ActiveProfile != null)
            {
                _profileManager.ActiveProfile.PreferredActivity = item.Tag?.ToString() ?? "Bieżnia";
                if (!_isTrainingActive && _activeSession != null)
                {
                    _activeSession.Name = $"{_profileManager.ActiveProfile.PreferredActivity} {DateTime.Now:dd.MM.yyyy HH:mm}";
                    
                }
                SaveActiveProfileSettings();
            }
        }

        private void ChartCanvasLive_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private void CheckAutoConnect_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            SaveGlobalSettings();
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            _discoveredDevices.Clear();
            _bleService.StartScanning();
        }

        private async void ListDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var device = ListDevices.SelectedItem as DiscoveredDevice;
            if (device != null)
            {
                _bleService.StopScanning();
                await _bleService.ConnectAsync(device.Address);
            }
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _bleService.Disconnect();
        }

        private void SliderAge_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingSettings) return;
            var profile = _profileManager.ActiveProfile;
            if (profile != null)
            {
                profile.Age = (int)e.NewValue;
                SaveActiveProfileSettings();
            }
        }

        private void ComboGender_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            var profile = _profileManager.ActiveProfile;
            var item = ComboGender.SelectedItem as ComboBoxItem;
            if (profile != null && item != null)
            {
                profile.Gender = item.Content?.ToString() ?? "Male";
                SaveActiveProfileSettings();
            }
        }

        private void SliderWeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingSettings) return;
            var profile = _profileManager.ActiveProfile;
            if (profile != null)
            {
                profile.Weight = (int)e.NewValue;
                SaveActiveProfileSettings();
            }
        }

        private void LoadSessionsFromDisk()
        {
            try
            {
                _historicalSessions.Clear();
                if (!Directory.Exists(_sessionsPath)) return;
                var files = Directory.GetFiles(_sessionsPath, "session_*.json");
                var loadedList = new List<Session>();

                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var session = System.Text.Json.JsonSerializer.Deserialize<Session>(json);
                        if (session != null) loadedList.Add(session);
                    }
                    catch { }
                }

                var sorted = loadedList.OrderByDescending(s => s.StartTime).ToList();
                foreach (var session in sorted)
                {
                    _historicalSessions.Add(session);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Session", "Load error", ex);
            }
        }

        private async Task SaveSessionToDiskAsync(Session sessionToSave)
        {
            try
            {
                if (!Directory.Exists(_sessionsPath))
                    Directory.CreateDirectory(_sessionsPath);

                string json = System.Text.Json.JsonSerializer.Serialize(sessionToSave);
                string filename = $"session_{sessionToSave.StartTime:yyyyMMdd_HHmmss}_{sessionToSave.Id}.json";
                string path = System.IO.Path.Combine(_sessionsPath, filename);
                
                await File.WriteAllTextAsync(path, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Session", "Save error", ex);
            }
        }

        private void ExportSessionToCsv(Session sessionToExport)
        {
            if (sessionToExport == null || sessionToExport.History.Count == 0) return;
            
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"Export_{sessionToExport.StartTime:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = new List<string> { "Time,BPM" };
                    foreach (var pt in sessionToExport.History)
                    {
                        lines.Add($"{pt.Time},{pt.Bpm}");
                    }
                    File.WriteAllLines(dialog.FileName, lines);
                    System.Windows.MessageBox.Show("Wyeksportowano pomyślnie.", "Eksport", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

    }
}