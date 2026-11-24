using System.Net;
using System.Text.Json;
using StintTracker.Core.Events;
using StintTracker.Core.Outbox;
using static StintTracker.Tests.Frames;

namespace StintTracker.Tests;

public sealed class OutboxAndSenderTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "stint-outbox-" + Guid.NewGuid().ToString("N"));
    private readonly EventFactory factory = new(new ClientInfo(Guid.Empty, "test"));

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private TrackerEvent Event(double t) => factory.CarStatus(At(Context(Kam), t, true), onPitRoad: false);

    [Fact]
    public void Outbox_survives_restart_and_returns_events_in_enqueue_order()
    {
        var first = new EventOutbox(dir);
        var events = Enumerable.Range(0, 3).Select(i => Event(100 + i)).ToList();
        events.ForEach(first.Enqueue);

        var reopened = new EventOutbox(dir);
        var items = reopened.Peek(10);

        Assert.Equal(events.Select(e => e.EventId), items.Select(i => i.EventId));
        reopened.Remove(items.Take(2));
        Assert.Equal(events[2].EventId, Assert.Single(reopened.Peek(10)).EventId);
    }

    [Fact]
    public void Unreadable_outbox_file_is_quarantined_not_retried_forever()
    {
        var outbox = new EventOutbox(dir);
        File.WriteAllText(Path.Combine(dir, "00000000000000000000-garbage.json"), "{not json");
        outbox.Enqueue(Event(100));

        Assert.Single(outbox.Peek(10));
        Assert.Single(Directory.GetFiles(Path.Combine(dir, "bad")));
    }

    [Fact]
    public async Task Sender_deletes_acknowledged_events_and_reports_connected()
    {
        var outbox = new EventOutbox(dir);
        var events = Enumerable.Range(0, 3).Select(i => Event(100 + i)).ToList();
        events.ForEach(outbox.Enqueue);
        var handler = new FakeApi(body => Ack(
            accepted: [events[0].EventId],
            duplicates: [events[1].EventId],
            rejected: [events[2].EventId]));
        var sender = new EventSender(outbox, Http(handler));
        var states = new List<bool>();
        sender.ConnectionChanged += states.Add;

        Assert.True(await sender.SendPendingAsync(CancellationToken.None));

        Assert.Equal(0, outbox.Count);
        Assert.Equal([true], states);
        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        Assert.Equal(3, sent.RootElement.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task Sender_keeps_events_when_the_api_fails()
    {
        var outbox = new EventOutbox(dir);
        outbox.Enqueue(Event(100));
        var sender = new EventSender(outbox, Http(new FakeApi(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))));
        var states = new List<bool>();
        sender.ConnectionChanged += states.Add;

        Assert.False(await sender.SendPendingAsync(CancellationToken.None));

        Assert.Equal(1, outbox.Count);
        Assert.Equal([false], states);
    }

    [Fact]
    public async Task Sender_splits_large_backlogs_into_batches_of_100()
    {
        var outbox = new EventOutbox(dir);
        for (int i = 0; i < 150; i++) outbox.Enqueue(Event(100 + i));
        var handler = new FakeApi(body =>
        {
            using var doc = JsonDocument.Parse(body);
            var ids = doc.RootElement.GetProperty("events").EnumerateArray()
                .Select(e => e.GetProperty("event_id").GetGuid()).ToList();
            return Ack(accepted: ids);
        });

        Assert.True(await new EventSender(outbox, Http(handler)).SendPendingAsync(CancellationToken.None));

        Assert.Equal(0, outbox.Count);
        Assert.Equal(2, handler.Bodies.Count);
    }

    [Fact]
    public void Event_json_uses_the_api_field_names()
    {
        var f = At(Context(Kam), 3020, true) with { PitstopActive = true, TireChangeLF = true, PitRepairLeft = 12.5f };
        using var doc = JsonDocument.Parse(factory.PitBoxEntered(f).ToJson());
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("pit_box_entered", root.GetProperty("type").GetString());
        Assert.Equal(CarIdx, root.GetProperty("car_idx").GetInt32());
        Assert.Equal(1001, root.GetProperty("driver").GetProperty("user_id").GetInt32());
        Assert.Equal("test", root.GetProperty("client").GetProperty("app_version").GetString());
        var payload = root.GetProperty("payload");
        Assert.Equal(12.5, payload.GetProperty("required_repair_s").GetDouble());
        Assert.True(payload.GetProperty("tires_requested").GetProperty("lf").GetBoolean());
        Assert.Equal(5, payload.GetProperty("car_state").GetProperty("class_position").GetInt32());
    }

    // ---------- helpers ----------

    private static HttpClient Http(FakeApi handler) =>
        new(handler) { BaseAddress = new Uri("http://api.test") };

    private static HttpResponseMessage Ack(
        IEnumerable<Guid>? accepted = null, IEnumerable<Guid>? duplicates = null, IEnumerable<Guid>? rejected = null)
    {
        var body = new
        {
            accepted = accepted ?? [],
            duplicates = duplicates ?? [],
            rejected = (rejected ?? []).Select(id => new { id = id.ToString(), reason = "bad" }),
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FakeApi(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(token);
            Bodies.Add(body);
            return respond(body);
        }
    }
}
