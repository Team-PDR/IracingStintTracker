using System.IO;
using System.Text.Json;
using StintTracker.Core.Events;

namespace StintTracker.Core.Outbox;

public sealed record OutboxItem(Guid EventId, string Json, string Path);

/// <summary>
/// Durable local queue of events not yet acknowledged by the API.
///
/// One file per event, so a crash, restart or API outage loses nothing: events
/// are deleted only after the server acknowledges them. Files are named by
/// enqueue time so they are sent in order.
/// </summary>
public sealed class EventOutbox
{
    private readonly string directory;
    private readonly string quarantine;
    private readonly object writeLock = new();
    private long sequence;

    /// <summary>Raised after an event is written. Used to wake the sender.</summary>
    public event Action? EventEnqueued;

    public EventOutbox(string directory)
    {
        this.directory = directory;
        quarantine = System.IO.Path.Combine(directory, "bad");
        Directory.CreateDirectory(directory);
    }

    public static string DefaultDirectory => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StintTracker",
        "outbox");

    public int Count => Directory.GetFiles(directory, "*.json").Length;

    public void Enqueue(TrackerEvent ev)
    {
        string json = ev.ToJson();

        lock (writeLock)
        {
            // Timestamp plus a counter keeps names unique and in order even
            // when several events are written in the same tick.
            string name = $"{DateTime.UtcNow.Ticks:D20}-{sequence++:D6}-{ev.EventId:N}.json";
            string path = System.IO.Path.Combine(directory, name);
            string tmp = path + ".tmp";

            File.WriteAllText(tmp, json);
            File.Move(tmp, path);
        }

        EventEnqueued?.Invoke();
    }

    /// <summary>The oldest events, up to <paramref name="max"/>.</summary>
    public IReadOnlyList<OutboxItem> Peek(int max)
    {
        var items = new List<OutboxItem>();

        foreach (string path in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            if (items.Count >= max)
            {
                break;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                Guid id = doc.RootElement.GetProperty("event_id").GetGuid();
                items.Add(new OutboxItem(id, json, path));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
            {
                // Unreadable events would block the queue forever; set them aside.
                Console.WriteLine($"Outbox: quarantining unreadable event {path}: {ex.Message}");
                Quarantine(path);
            }
        }

        return items;
    }

    public void Remove(IEnumerable<OutboxItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                File.Delete(item.Path);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Outbox: couldn't delete {item.Path}: {ex.Message}");
            }
        }
    }

    private void Quarantine(string path)
    {
        Directory.CreateDirectory(quarantine);
        File.Move(path, System.IO.Path.Combine(quarantine, System.IO.Path.GetFileName(path)), overwrite: true);
    }
}
