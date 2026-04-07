using System.Text;
using System.Threading.Channels;
using System.Threading;
using IracingStintTracker;
using IracingStintTracker.Subscribers;
using IracingStintTracker.Api;

class Program
{

    static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var manager = new TelemetryManager();
        var channel = Channel.CreateUnbounded<Object>();

        var sessionSubscriber = new SessionSubscriber(manager, channel);
        var stintSubscriber = new StintSubscriber(manager, channel);
        var pitstopSubscriber = new PitstopSubscriber(manager, channel);

        var cts = new CancellationTokenSource();
        var apiClient = new ApiClient(channel);

        var apiTask = apiClient.Run(cts.Token);

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            channel.Writer.Complete();
            cts.Cancel();
            apiTask.Wait(); // wait for graceful shutdown
            Console.WriteLine("Shutting down");
        };
    }
}