using System.Text;
using IracingStintTracker;

class Program
{

    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var manager = new TelemetryManager();
    }
}