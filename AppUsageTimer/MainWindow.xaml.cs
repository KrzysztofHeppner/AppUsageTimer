using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using AppUsageTimer;

namespace AppUsageTimer
{
    public class AppData
    {
        public Dictionary<string, TimeSpan>? ProcessTotalTimes { get; set; }
        public string? FilterText { get; set; }
    }

    public partial class MainWindow : Window
    {
        private Dictionary<string, TimeSpan> _processTotalTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, TimeSpan> _processSessionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

        private ObservableCollection<ProcessViewModel> ProcessEntries { get; set; } = new ObservableCollection<ProcessViewModel>();
        public ICollectionView? ProcessEntriesView { get; private set; }

        //private DispatcherTimer? _processCheckTimer;
        private DispatcherTimer? _saveTimer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _saveInterval = TimeSpan.FromMinutes(1);
        private const string DataFileName = "process_times.json";

        private List<string> _filterTerms = new List<string>();

        private HashSet<string> _previouslyRunningProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _canReallyClose = false;


        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            ProcessEntriesView = CollectionViewSource.GetDefaultView(ProcessEntries);
            ProcessEntriesView.Filter = FilterProcesses;

            this.Visibility = Visibility.Hidden;
            this.ShowInTaskbar = false;


            _stopwatch.Start();

            _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_canReallyClose)
            {
                e.Cancel = true;
                this.Visibility = Visibility.Hidden;
                this.ShowInTaskbar = false;
                Debug.WriteLine("MainWindow hidden to tray instead of closing.");
            }
            else
            {
                Debug.WriteLine("MainWindow closing explicitly.");
                SaveData();
                //_processCheckTimer?.Stop();
                _saveTimer?.Stop();
            }
            base.OnClosing(e);
        }

        public void AllowClose()
        {
            _canReallyClose = true;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            UpdateDisplay();
            SetupTimers();

        }

        private readonly Stopwatch _stopwatch = new();
        private System.Threading.Timer? _timer;

        private void SetupTimers()
        {
            //_processCheckTimer = new DispatcherTimer { Interval = _checkInterval };
            //_processCheckTimer.Tick += ProcessCheckTimer_Tick;
            //_processCheckTimer.Start();

            _saveTimer = new DispatcherTimer { Interval = _saveInterval };
            _saveTimer.Tick += SaveTimer_Tick;
            _saveTimer.Start();

            Debug.WriteLine("Timers started.");
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = FilterTextBox.Text;

            _filterTerms = filterText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(term => term.Trim())
                                     .Where(term => !string.IsNullOrEmpty(term))
                                     .ToList();

            ProcessEntriesView?.Refresh();
        }

        private bool FilterProcesses(object item)
        {
            if (_filterTerms == null || !_filterTerms.Any())
            {
                return true;
            }

            if (item is ProcessViewModel vm)
            {
                return _filterTerms.Any(term => vm.ProcessName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }


        private void ProcessCheckTimer_Tick(object? sender, EventArgs? e)
        {
            try
            {
                Process[] currentProcesses = Process.GetProcesses();
                var currentRunningProcessNames = new HashSet<string>(
                    currentProcesses.Select(p => p.ProcessName),
                    StringComparer.OrdinalIgnoreCase
                );

                var newlyStartedProcesses = currentRunningProcessNames.Except(_previouslyRunningProcessNames).ToList();

                foreach (string newProcessName in newlyStartedProcesses)
                {
                    _processSessionTimes[newProcessName] = TimeSpan.Zero;
                    Debug.WriteLine($"Session time reset for newly started/restarted process: {newProcessName}");
                }

                foreach (string name in currentRunningProcessNames)
                {
                    _processTotalTimes.TryGetValue(name, out TimeSpan currentTotalTime);
                    _processTotalTimes[name] = currentTotalTime + _checkInterval;

                    _processSessionTimes.TryGetValue(name, out TimeSpan currentSessionTime);
                    _processSessionTimes[name] = currentSessionTime + _checkInterval;

                    var existingViewModel = ProcessEntries.FirstOrDefault(p => p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (existingViewModel != null)
                    {
                        existingViewModel.TotalTime = _processTotalTimes[name];
                        existingViewModel.SessionTime = _processSessionTimes[name];
                    }
                    else
                    {
                        ProcessEntries.Add(new ProcessViewModel(name, _processTotalTimes[name], _processSessionTimes[name]));
                    }
                }

                _previouslyRunningProcessNames = currentRunningProcessNames;

            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Info: Process exited during check: {ex.Message}");
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"Warning: Access denied or error checking process: {ex.Message} (Error Code: {ex.NativeErrorCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking processes: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private void UpdateDisplay()
        {
            ProcessEntries.Clear();

            foreach (var kvp in _processTotalTimes)
            {
                ProcessEntries.Add(new ProcessViewModel(kvp.Key, kvp.Value, TimeSpan.Zero));
            }

            Debug.WriteLine($"Initial display populated with {ProcessEntries.Count} entries from loaded total data. Session times reset.");
        }


        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            SaveData();
        }

        private void LoadData()
        {
            _processSessionTimes.Clear();
            _previouslyRunningProcessNames.Clear();

            if (!File.Exists(DataFileName))
            {
                Debug.WriteLine($"Data file '{DataFileName}' not found. Starting fresh.");
                _processTotalTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
                FilterTextBox.Text = string.Empty;
                return;
            }

            try
            {
                string json = File.ReadAllText(DataFileName);
                AppData? loadedData = JsonSerializer.Deserialize<AppData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (loadedData != null)
                {
                    _processTotalTimes = loadedData.ProcessTotalTimes
                        ?? new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

                    FilterTextBox.Text = loadedData.FilterText ?? string.Empty;

                    Debug.WriteLine($"Data loaded successfully from '{DataFileName}'. Tracking {_processTotalTimes.Count} processes.");
                    Debug.WriteLine($"Loaded Filter: '{FilterTextBox.Text}'");
                }
                else
                {
                    Debug.WriteLine($"Error deserializing data from '{DataFileName}'. File might be empty or corrupt.");
                    _processTotalTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
                    FilterTextBox.Text = string.Empty;
                    System.Windows.MessageBox.Show($"Could not load previous data from {DataFileName}. Starting with empty data.\nFile may be empty or corrupt.",
                                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading data from '{DataFileName}': {ex.GetType().Name} - {ex.Message}");
                _processTotalTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
                FilterTextBox.Text = string.Empty;
                System.Windows.MessageBox.Show($"Could not load previous data from {DataFileName}. Starting with empty data.\nError: {ex.Message}",
                                "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveData()
        {
            try
            {
                var dataToSave = new AppData
                {
                    ProcessTotalTimes = _processTotalTimes,
                    FilterText = FilterTextBox.Text
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(dataToSave, options);
                File.WriteAllText(DataFileName, json);
                Debug.WriteLine($"Data saved to '{DataFileName}' at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data to '{DataFileName}': {ex.GetType().Name} - {ex.Message}");
            }
        }

        private void OnTimerTick(object? state)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessCheckTimer_Tick(null, null);
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Dispose();
            _stopwatch.Stop();
            base.OnClosed(e);
        }
    }
}