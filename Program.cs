using IRSDKSharper;
using System;
using System.Text;
using System.Diagnostics;

using TelemetryApp.Managers;
class Program
{

    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var manager = new SessionManager();
    }
}