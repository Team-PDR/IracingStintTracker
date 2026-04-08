using IracingStintTracker;
using IracingStintTracker.Api;
using IracingStintTracker.Dtos;
using IracingStintTracker.Subscribers;
using System.Text;
using System.Threading.Channels;

public class StintTrackerService
{
    private Channel<ITrackerDto> channel = Channel.CreateUnbounded<ITrackerDto>();
    private CancellationTokenSource cts;
    private Task apiTask;
    private TelemetryManager manager;


    public void Start()
    {

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        manager = new TelemetryManager();
        cts = new CancellationTokenSource();
        var apiClient = new ApiClient(channel);

        var sessionSubscriber = new SessionSubscriber(manager, channel);
        var stintSubscriber = new StintSubscriber(manager, channel);
        var pitstopSubscriber = new PitstopSubscriber(manager, channel);


        manager.Start();
        apiTask = Task.Run(() => apiClient.Run(cts.Token));
    }

    public async Task Stop()
    {

        cts.Cancel();
        channel.Writer.Complete();

        if (apiTask != null)
        {
           await apiTask;
        }

        manager.Stop();
        Console.WriteLine("Shutting down");
    }
}