using System.IO;
using System.Reflection;
using StintTracker.Core.Events;

namespace StintTracker.Core;

/// <summary>Identifies this installation in events (for debugging only).</summary>
public static class ClientIdentity
{
    public static ClientInfo Load()
    {
        return new ClientInfo(LoadOrCreateInstallId(), AppVersion());
    }

    private static Guid LoadOrCreateInstallId()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StintTracker");
        string file = Path.Combine(dir, "install-id");

        try
        {
            if (File.Exists(file) && Guid.TryParse(File.ReadAllText(file).Trim(), out Guid existing))
            {
                return existing;
            }

            Directory.CreateDirectory(dir);
            Guid id = Guid.NewGuid();
            File.WriteAllText(file, id.ToString());
            return id;
        }
        catch (IOException)
        {
            return Guid.Empty;
        }
    }

    private static string AppVersion()
    {
        string version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";

        // Drop the "+<commit hash>" suffix the SDK appends; the API allows 32 chars.
        int plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];
        return version.Length > 32 ? version[..32] : version;
    }
}
