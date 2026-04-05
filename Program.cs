using System.Text;
using IracingStintTracker;
using IracingStintTracker.Subscribers;

class Program
{

    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var manager = new TelemetryManager();

        var sessionSubscriber = new SessionSubscriber(manager);
    }
}