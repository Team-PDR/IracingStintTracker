using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using StintTracker.Core.Events;

namespace StintTracker.Core.Outbox;

public sealed record RejectedEvent(string? Id, string Reason);

public sealed record EventBatchResult(
    List<Guid> Accepted,
    List<Guid> Duplicates,
    List<RejectedEvent> Rejected);

/// <summary>
/// Sends outbox events to POST /v1/events in batches and deletes each event
/// once the server acknowledges it. Unacknowledged events stay in the outbox
/// and are retried with backoff, so delivery is at-least-once; the server
/// deduplicates by event_id.
/// </summary>
public sealed class EventSender
{
    public const int BatchSize = 100;
    private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly EventOutbox outbox;
    private readonly HttpClient client;
    private readonly SemaphoreSlim wake = new(0);
    private bool? connected;

    /// <summary>Raised when the API becomes reachable or unreachable.</summary>
    public event Action<bool>? ConnectionChanged;

    public EventSender(EventOutbox outbox, HttpClient client)
    {
        this.outbox = outbox;
        this.client = client;
        outbox.EventEnqueued += () => wake.Release();
    }

    public static HttpClient CreateHttpClient(string baseUrl, string apiKey)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(20),
        };
        http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return http;
    }

    public async Task Run(CancellationToken token)
    {
        await CheckHealthAsync(token);
        var backoff = TimeSpan.FromSeconds(2);

        while (!token.IsCancellationRequested)
        {
            try
            {
                bool sentAll = await SendPendingAsync(token);
                if (sentAll)
                {
                    backoff = TimeSpan.FromSeconds(2);
                    await wake.WaitAsync(IdlePoll, token);
                }
                else
                {
                    await Task.Delay(backoff, token);
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad response stop delivery for the session.
                Console.WriteLine($"Event sender error: {ex.Message}");
                SetConnected(false);
                try
                {
                    await Task.Delay(backoff, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
            }
        }

        Console.WriteLine("Event sender stopped.");
    }

    /// <summary>
    /// Sends batches until the outbox is empty or a send fails.
    /// Returns false if a send failed (the caller should back off).
    /// </summary>
    public async Task<bool> SendPendingAsync(CancellationToken token)
    {
        while (true)
        {
            var batch = outbox.Peek(BatchSize);
            if (batch.Count == 0)
            {
                return true;
            }

            var result = await PostBatchAsync(batch, token);
            if (result == null)
            {
                return false;
            }

            var acknowledged = new HashSet<Guid>(result.Accepted.Concat(result.Duplicates));
            foreach (var rejected in result.Rejected)
            {
                // A rejected event will never be accepted; retrying would only
                // block the queue. The reason is logged for debugging.
                Console.WriteLine($"Event {rejected.Id} rejected by API: {rejected.Reason}");
                if (Guid.TryParse(rejected.Id, out Guid id))
                {
                    acknowledged.Add(id);
                }
            }

            outbox.Remove(batch.Where(item => acknowledged.Contains(item.EventId)));

            if (!batch.Any(item => acknowledged.Contains(item.EventId)))
            {
                // Nothing in the batch was acknowledged; don't spin on it.
                Console.WriteLine("API acknowledged none of the batch; will retry later.");
                return false;
            }
        }
    }

    private async Task<EventBatchResult?> PostBatchAsync(IReadOnlyList<OutboxItem> batch, CancellationToken token)
    {
        // The events are already serialized; splice them into the body as-is.
        var body = new StringBuilder("{\"events\":[");
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) body.Append(',');
            body.Append(batch[i].Json);
        }
        body.Append("]}");

        try
        {
            using var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/v1/events", content, token);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Event batch failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                SetConnected(false);
                return null;
            }

            SetConnected(true);
            return await response.Content.ReadFromJsonAsync<EventBatchResult>(EventJson.Options, token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
        {
            Console.WriteLine($"Event batch failed: {ex.Message}");
            SetConnected(false);
            return null;
        }
    }

    private async Task CheckHealthAsync(CancellationToken token)
    {
        try
        {
            using var response = await client.GetAsync("/health", token);
            SetConnected(response.IsSuccessStatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
        {
            SetConnected(false);
        }
    }

    private void SetConnected(bool value)
    {
        if (connected == value)
        {
            return;
        }
        connected = value;
        ConnectionChanged?.Invoke(value);
    }
}
