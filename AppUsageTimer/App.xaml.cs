using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;

namespace AppUsageTimer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Debug.WriteLine($"Application ShutdownMode set to: {System.Windows.Application.Current.ShutdownMode}");


            _mainWindow = new MainWindow();

            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Visibility = Visibility.Hidden;
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Show();
            _mainWindow.Activate();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Process Time Tracker";

            try
            {
                Uri iconUri = new Uri("pack://application:,,,/logo.ico", UriKind.RelativeOrAbsolute);

                System.Windows.Resources.StreamResourceInfo? streamInfo = System.Windows.Application.GetResourceStream(iconUri);

                if (streamInfo != null && streamInfo.Stream != null)
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
                    Debug.WriteLine("Tray icon loaded successfully from resource.");
                }
                else
                {
                    Debug.WriteLine($"Tray icon resource not found at '{iconUri}' or stream is null. File might not be embedded as Resource or path is wrong.");
                    System.Windows.MessageBox.Show($"Could not find tray icon resource at '{iconUri}'. Make sure its Build Action is 'Resource' and the path is correct.", "Icon Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tray icon resource: {ex.GetType().Name} - {ex.Message}");
                System.Windows.MessageBox.Show($"An error occurred loading tray icon: {ex.Message}", "Icon Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            var contextMenu = new ContextMenuStrip();
            var showMenuItem = new ToolStripMenuItem("Show Tracker");
            var exitMenuItem = new ToolStripMenuItem("Exit");

            showMenuItem.Click += ShowMenuItem_Click;
            exitMenuItem.Click += ExitMenuItem_Click;

            contextMenu.Items.Add(showMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            Debug.WriteLine("Application started minimized to tray.");
        }

        private void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.ShowInTaskbar = true;
                _mainWindow.Visibility = Visibility.Visible;
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        }

        private void ShowMenuItem_Click(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void ExitMenuItem_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("Exit menu clicked. Calling Application.Shutdown().");
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Dispose();
                _notifyIcon = null;
                Debug.WriteLine("NotifyIcon disposed during OnExit.");
            }
            Debug.WriteLine("Application exiting.");
            base.OnExit(e);
        }
    }
}