using IRSDKSharper;
using StintTracker.Core.Events;
using StintTracker.Core.Tracking;

namespace StintTracker.Tests;

/// <summary>Builders for telemetry frames and trackers.</summary>
internal static class Frames
{
    public const int SubSession = 86593512;
    public const int CarIdx = 2;

    public static readonly DriverIdentity Kam = new(1001, "Kam Wilson");
    public static readonly DriverIdentity Greg = new(1002, "Gregory Toumaian");

    public static SessionContext Context(DriverIdentity driver, int subSession = SubSession) => new()
    {
        SubSessionId = subSession,
        CarIdx = CarIdx,
        Driver = driver,
        Track = "Spa",
        CarName = "Cadillac V-Series.R",
        CarClass = "GTP",
        TeamId = 555,
        TeamName = "PDR",
        CarNumber = "7",
        SessionTypes = ["Practice", "Qualify", "Race"],
        SessionDurationsS = [3600, 1200, 21600],
    };

    /// <summary>A race-session frame at time <paramref name="t"/> (60 ticks per second).</summary>
    public static TelemetryFrame At(SessionContext ctx, double t, bool driving) => new()
    {
        Session = ctx,
        SessionNum = 2,
        SessionTime = t,
        SessionTick = (int)Math.Round(t * 60),
        SessionState = IRacingSdkEnum.SessionState.Racing,
        ClassPosition = 5,
        IsOnTrackCar = driving,
        IsOnTrack = driving,
        FuelLevel = 50f,
        Lap = 10,
        LapCompleted = 9,
        TrackTemp = 31.5f,
        TrackWetness = 1,
        Skies = 1,
    };

    public static RaceStateTracker Tracker(double statusIntervalS = 1e9) =>
        new(new EventFactory(new ClientInfo(Guid.Empty, "test")), statusIntervalS);

    /// <summary>Feeds frames in order and returns every event emitted.</summary>
    public static List<TrackerEvent> Run(RaceStateTracker tracker, IEnumerable<TelemetryFrame> frames) =>
        frames.SelectMany(tracker.Process).ToList();

    public static List<string> Types(IEnumerable<TrackerEvent> events) => events.Select(e => e.Type).ToList();
}
