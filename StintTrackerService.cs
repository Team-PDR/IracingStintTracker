using IracingStintTracker;
using IracingStintTracker.Api;
using IracingStintTracker.Dtos;
using IracingStintTracker.Subscribers;
using System.Text;
using System.Threading.Channels;

public class StintTrackerService
{
    private readonly Channel<ITrackerDto> channel = Channel.CreateUnbounded<ITrackerDto>();
    private readonly CancellationTokenSource cts = new();
    private readonly TelemetryManager manager = new();
    private Task? apiTask;

    public event Action<bool>? OnIracingRunningChanged;
    public event Action<TrackerStatus>? OnTrackerStatusChanged;
    public event Action<bool>? OnApiConnectionChanged;

    public void Start()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var apiClient = new ApiClient(channel);

        var sessionSubscriber = new SessionSubscriber(manager, channel);
        var stintSubscriber = new StintSubscriber(manager, channel);
        var pitstopSubscriber = new PitstopSubscriber(manager, channel);

        apiClient.HealthChecked += status =>
        {
            OnApiConnectionChanged?.Invoke(status);
        };

        manager.SdkConnectionChange += status =>
        {
            OnIracingRunningChanged?.Invoke(status);

            if (!status)
            {
                OnTrackerStatusChanged?.Invoke(TrackerStatus.Offline);
            }
            else
            {
                OnTrackerStatusChanged?.Invoke(TrackerStatus.Waiting);
            }
        };

        manager.RaceStarted += _ =>
        {
            OnTrackerStatusChanged?.Invoke(TrackerStatus.Running);
        };

        manager.RaceFinished += _ =>
        {
            OnTrackerStatusChanged?.Invoke(TrackerStatus.Finished);
        };

        OnIracingRunningChanged?.Invoke(false);
        OnTrackerStatusChanged?.Invoke(TrackerStatus.Offline);

        apiTask = Task.Run(() => apiClient.Run(cts.Token));
        manager.Start();
    }

    public async Task Stop()
    {
        if (cts == null) return;

        cts.Cancel();
        manager.Stop();
        channel.Writer.Complete();

        if (apiTask != null)
        {
           await apiTask;
        }

        Console.WriteLine("Shutting down");
    }
}

public enum TrackerStatus
{
    Offline,
    Waiting,
    Running,
    Finished,
}