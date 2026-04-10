using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace StintTracker.Gui;


public partial class MainWindow : Window
{
    private bool isExiting = false;
    private readonly StintTrackerService tracker;
    private readonly Dictionary<TrackerStatus, (string Message, Brush color)> TrackerStatusMap = new(){
        { TrackerStatus.Offline, ("Waiting for iRacing", Brushes.Red) },
        { TrackerStatus.Waiting, ("Waiting for race", Brushes.Orange) },
        { TrackerStatus.Running, ("Monitoring Race", Brushes.Green) },
        { TrackerStatus.Finished, ("Race Finished", Brushes.Purple) }
    };


    public MainWindow()
    {
        InitializeComponent();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

<<<<<<< HEAD
        string apiUrl = config["TestApiBaseUrl"];
=======
        string apiUrl = config["ApiBaseUrl"];
>>>>>>> dist
        string apiKey = config["ApiKey"];

        tracker = new StintTrackerService();

        tracker.OnIracingRunningChanged += OnIracingStatusChanged;
        tracker.OnTrackerStatusChanged += OnTrackerStatusChanged;
        tracker.OnApiConnectionChanged += OnApiStatusChanged;

        tracker.Start(apiUrl, apiKey);
    }

    public void OnIracingStatusChanged(bool isRunning)
    {
        Dispatcher.Invoke(() =>
        {
            IracingStatusLabel.Content = isRunning ? "Running" : "Not Running";
            IracingStatusSymbol.Foreground = isRunning ? Brushes.Green : Brushes.Red;
        });
    }
    private void OnTrackerStatusChanged(TrackerStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            if (TrackerStatusMap.TryGetValue(status, out var info))
            {
                TrackerStatusLabel.Content = info.Message;
                TrackerStatusSymbol.Foreground = info.color;
            }
        });
    }

    private void OnApiStatusChanged(bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            ApiStatusLabel.Content = isConnected ? "Connected" : "Not connected";
            ApiStatusSymbol.Foreground = isConnected ? Brushes.Green : Brushes.Red;
        });
    }

    private void ExitMenuItemOnClick(object sender, RoutedEventArgs e)
    {
        isExiting = true;
        Application.Current.Shutdown();
    }

    private void ShowHideMenuItemOnClick(object sender, RoutedEventArgs e)
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!isExiting)
        {
            new ToastContentBuilder()
                 .AddText("Stint Tracker")
                 .AddText("Closed to tray")
                 .SetToastDuration(ToastDuration.Short)
                 .Show();

            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override async void OnClosed(EventArgs e)
    {
        await tracker.Stop();
        base.OnClosed(e);
    }
}