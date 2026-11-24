using StintTracker.Core;
using StintTracker.Core.Events;
using StintTracker.Core.Outbox;
using StintTracker.Core.Tracking;
using System.Text;

/// <summary>
/// Composition root: iRacing SDK → <see cref="RaceStateTracker"/> → durable
/// <see cref="EventOutbox"/> → <see cref="EventSender"/> → API.
/// </summary>
public class StintTrackerService
{
    private readonly CancellationTokenSource cts = new();
    private readonly TelemetryManager manager = new();
    private Task? senderTask;
    private TrackerStatus status = TrackerStatus.Offline;

    public event Action<bool>? OnIracingRunningChanged;
    public event Action<TrackerStatus>? OnTrackerStatusChanged;
    public event Action<bool>? OnApiConnectionChanged;

    public void Start(string apiUrl, string apiKey, double statusIntervalS = 10.0)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var factory = new EventFactory(ClientIdentity.Load());
        var tracker = new RaceStateTracker(factory, statusIntervalS);
        var outbox = new EventOutbox(EventOutbox.DefaultDirectory);
        var sender = new EventSender(outbox, EventSender.CreateHttpClient(apiUrl, apiKey));

        int pending = outbox.Count;
        if (pending > 0)
        {
            Console.WriteLine($"Outbox has {pending} unsent events from a previous run; sending them.");
        }

        sender.ConnectionChanged += connected => OnApiConnectionChanged?.Invoke(connected);

        manager.SdkConnectionChange += running =>
        {
            OnIracingRunningChanged?.Invoke(running);
            SetStatus(running ? TrackerStatus.Waiting : TrackerStatus.Offline);
        };

        manager.FrameReceived += frame =>
        {
            foreach (var ev in tracker.Process(frame))
            {
                Console.WriteLine($"Event: {ev.Type} t={ev.SessionTime:F1} driver={ev.Driver.Name}");
                outbox.Enqueue(ev);
            }

            if (tracker.RaceFinished)
            {
                SetStatus(TrackerStatus.Finished);
            }
            else if (tracker.RaceStarted)
            {
                SetStatus(TrackerStatus.Running);
            }
            else
            {
                SetStatus(TrackerStatus.Waiting);
            }
        };

        OnIracingRunningChanged?.Invoke(false);
        OnTrackerStatusChanged?.Invoke(TrackerStatus.Offline);
        OnApiConnectionChanged?.Invoke(false);

        senderTask = Task.Run(() => sender.Run(cts.Token));
        manager.Start();
    }

    private void SetStatus(TrackerStatus next)
    {
        if (next == status)
        {
            return;
        }
        status = next;
        OnTrackerStatusChanged?.Invoke(next);
    }

    public async Task Stop()
    {
        cts.Cancel();
        manager.Stop();

        if (senderTask != null)
        {
            await senderTask;
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
