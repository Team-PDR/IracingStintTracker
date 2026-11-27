using StintTracker.Core.Tracking;

namespace StintTracker.Core.Events;

/// <summary>Builds wire events from telemetry frames.</summary>
public sealed class EventFactory
{
    private readonly ClientInfo client;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<DateOnly> localToday;

    public EventFactory(ClientInfo client, Func<DateTimeOffset>? utcNow = null, Func<DateOnly>? localToday = null)
    {
        this.client = client;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this.localToday = localToday ?? (() => DateOnly.FromDateTime(DateTime.Now));
    }

    public TrackerEvent Create(string type, TelemetryFrame f, object payload) => new()
    {
        EventId = Guid.NewGuid(),
        Type = type,
        SessionId = f.Session.SubSessionId,
        CarIdx = f.Session.CarIdx,
        SessionNum = f.SessionNum,
        SessionTime = f.SessionTime,
        SessionTick = f.SessionTick,
        Driver = new DriverInfo(f.Session.Driver.UserId, f.Session.Driver.Name),
        Client = client,
        SentAtUtc = utcNow(),
        Payload = payload,
    };

    public static CarState CarStateOf(TelemetryFrame f) =>
        new(f.FuelLevel, f.ClassPosition, f.Incidents, f.Lap, f.LapCompleted);

    public static Conditions ConditionsOf(TelemetryFrame f) =>
        new(f.TrackTemp, f.TrackWetness, f.Skies, f.Precipitation);

    public TrackerEvent SessionInfo(TelemetryFrame f)
    {
        var s = f.Session;
        int duration = f.SessionNum >= 0 && f.SessionNum < s.SessionDurationsS.Count
            ? s.SessionDurationsS[f.SessionNum]
            : 0;

        return Create(EventTypes.SessionInfo, f, new SessionInfoPayload(
            s.Track,
            s.CarName,
            string.IsNullOrEmpty(s.CarClass) ? s.CarName : s.CarClass,
            s.TeamId,
            s.TeamName,
            s.CarNumber,
            duration,
            localToday().ToString("yyyy-MM-dd")));
    }

    public TrackerEvent RaceStarted(TelemetryFrame f) =>
        Create(EventTypes.RaceStarted, f, new EmptyPayload());

    public TrackerEvent DriverEntered(TelemetryFrame f, bool onPitRoad) =>
        Create(EventTypes.DriverEntered, f,
            new DriverChangePayload(CarStateOf(f), ConditionsOf(f), onPitRoad, f.PitstopActive));

    public TrackerEvent DriverExited(TelemetryFrame lastDriving, bool onPitRoad) =>
        Create(EventTypes.DriverExited, lastDriving,
            new DriverChangePayload(CarStateOf(lastDriving), ConditionsOf(lastDriving), onPitRoad, lastDriving.PitstopActive));

    public TrackerEvent PitRoadEntered(TelemetryFrame f) =>
        Create(EventTypes.PitRoadEntered, f, new PitRoadEnteredPayload(CarStateOf(f), ViaTow: f.TowTime > 0));

    public TrackerEvent PitBoxEntered(TelemetryFrame f) =>
        Create(EventTypes.PitBoxEntered, f, new PitBoxEnteredPayload(
            CarStateOf(f),
            f.PitRepairLeft,
            f.PitOptRepairLeft,
            f.FastRepairAvailable,
            new TiresRequested(f.TireChangeLF, f.TireChangeRF, f.TireChangeLR, f.TireChangeRR)));

    public TrackerEvent PitBoxExited(TelemetryFrame f) =>
        Create(EventTypes.PitBoxExited, f, new PitBoxExitedPayload(CarStateOf(f), ConditionsOf(f)));

    public TrackerEvent PitRoadExited(TelemetryFrame f) =>
        Create(EventTypes.PitRoadExited, f, new CarStatePayload(CarStateOf(f)));

    /// <param name="crossing">Frame at the moment the car crossed the line.</param>
    /// <param name="lapTime">LapLastLapTime read a few seconds later, once iRacing has updated it.</param>
    public TrackerEvent LapCompleted(TelemetryFrame crossing, float lapTime) =>
        Create(EventTypes.LapCompleted, crossing,
            new LapCompletedPayload(crossing.LapCompleted, lapTime, CarStateOf(crossing)));

    public TrackerEvent RaceFinished(TelemetryFrame f, bool onPitRoad) =>
        Create(EventTypes.RaceFinished, f, new RaceFinishedPayload(CarStateOf(f), onPitRoad));

    public TrackerEvent CarStatus(TelemetryFrame f, bool onPitRoad) =>
        Create(EventTypes.CarStatus, f, new CarStatusPayload(CarStateOf(f), onPitRoad, f.PitstopActive));
}
