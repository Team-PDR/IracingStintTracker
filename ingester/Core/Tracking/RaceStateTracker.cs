using IRSDKSharper;
using StintTracker.Core.Events;

namespace StintTracker.Core.Tracking;

/// <summary>
/// Turns a stream of telemetry frames into events (docs/event-schema.md).
///
/// Pure state machine: no SDK, no I/O, no clock. Only the client whose user is
/// driving emits car events; every client emits session_info and race_started.
/// </summary>
public sealed class RaceStateTracker
{
    /// <summary>Ticks to wait after a line crossing before LapLastLapTime is valid.</summary>
    public const int LapTickDelay = 500;

    private readonly EventFactory factory;
    private readonly double statusIntervalS;

    private int subSessionId = -1;
    private bool wasUserDriving;
    private bool wasOnPitRoad;
    private bool wasInPitBox;
    private int lastCompletedLap = -1;
    private int finalLapCompleted = -1;
    private TelemetryFrame? lastDrivingFrame;
    private TelemetryFrame? pendingLap;
    private double lastStatusTime;

    public bool RaceStarted { get; private set; }
    public bool RaceFinished { get; private set; }

    public RaceStateTracker(EventFactory factory, double statusIntervalS = 10.0)
    {
        this.factory = factory;
        this.statusIntervalS = statusIntervalS;
    }

    private void ResetState()
    {
        RaceStarted = false;
        RaceFinished = false;
        wasUserDriving = false;
        wasOnPitRoad = false;
        wasInPitBox = false;
        lastCompletedLap = -1;
        finalLapCompleted = -1;
        lastDrivingFrame = null;
        pendingLap = null;
        lastStatusTime = 0;
    }

    public IReadOnlyList<TrackerEvent> Process(TelemetryFrame f)
    {
        var events = new List<TrackerEvent>();

        if (f.Session.SubSessionId != subSessionId)
        {
            ResetState();
            subSessionId = f.Session.SubSessionId;
        }

        if (!f.IsRaceSession)
        {
            return events;
        }

        // A tow back to the pits counts as being on pit road for the whole tow.
        bool onPitRoad = f.OnPitRoad || f.TowTime > 0;
        bool isUserDriving = f.IsOnTrackCar || (f.TowTime > 0 && wasUserDriving);

        if (!RaceStarted)
        {
            if (!HasRaceStarted(f))
            {
                return events;
            }

            RaceStarted = true;
            events.Add(factory.SessionInfo(f));
            events.Add(factory.RaceStarted(f));
        }

        bool entered = isUserDriving && !wasUserDriving && !RaceFinished;
        bool exited = !isUserDriving && wasUserDriving;

        if (entered)
        {
            // Seed the edge state from the car's actual state. Without this, a
            // driver taking over mid-stop sees pit road / pit box go from a
            // stale "false" to "true" and reports a new stop that never began.
            events.Add(factory.DriverEntered(f, onPitRoad));
            wasOnPitRoad = onPitRoad;
            wasInPitBox = f.PitstopActive;
            lastCompletedLap = f.LapCompleted;
            lastStatusTime = f.SessionTime;
        }

        if (isUserDriving && !entered && !RaceFinished)
        {
            if (!wasOnPitRoad && onPitRoad)
            {
                events.Add(factory.PitRoadEntered(f));
            }

            if (!wasInPitBox && f.PitstopActive)
            {
                events.Add(factory.PitBoxEntered(f));
            }

            if (wasInPitBox && !f.PitstopActive)
            {
                events.Add(factory.PitBoxExited(f));
            }

            if (wasOnPitRoad && !onPitRoad)
            {
                events.Add(factory.PitRoadExited(f));
            }

            if (f.LapCompleted > 0 && f.LapCompleted > lastCompletedLap)
            {
                pendingLap = f;
            }

            if (f.SessionTime - lastStatusTime >= statusIntervalS)
            {
                events.Add(factory.CarStatus(f, onPitRoad));
                lastStatusTime = f.SessionTime;
            }
        }

        if (pendingLap != null && isUserDriving && f.SessionTick - pendingLap.SessionTick >= LapTickDelay)
        {
            events.Add(factory.LapCompleted(pendingLap, f.LapLastLapTime));
            pendingLap = null;
        }

        if (exited)
        {
            if (pendingLap != null)
            {
                // Left the car before the delay elapsed; report the best
                // lap time we have rather than losing the lap.
                events.Add(factory.LapCompleted(pendingLap, lastDrivingFrame!.LapLastLapTime));
                pendingLap = null;
            }

            if (!RaceFinished)
            {
                events.Add(factory.DriverExited(lastDrivingFrame!, wasOnPitRoad));
            }
        }

        if (!RaceFinished && HasRaceEnded(f))
        {
            RaceFinished = true;

            // Only the driver can see the car's finish. Spectating teammates
            // just record that the race is over.
            if (isUserDriving)
            {
                events.Add(factory.RaceFinished(f, onPitRoad));
            }
            else if (exited)
            {
                events.Add(factory.RaceFinished(lastDrivingFrame!, wasOnPitRoad));
            }
        }

        if (isUserDriving)
        {
            wasOnPitRoad = onPitRoad;
            wasInPitBox = f.PitstopActive;
            lastCompletedLap = f.LapCompleted;
            lastDrivingFrame = f;
        }
        wasUserDriving = isUserDriving;

        return events;
    }

    private static bool HasRaceStarted(TelemetryFrame f) =>
        f.SessionState == IRacingSdkEnum.SessionState.Racing && f.ClassPosition > 0;

    private bool HasRaceEnded(TelemetryFrame f)
    {
        if (f.SessionState == IRacingSdkEnum.SessionState.CoolDown)
        {
            return true;
        }

        bool isOffTrack = !f.IsOnTrack || f.TowTime > 0;
        if (f.SessionState == IRacingSdkEnum.SessionState.Checkered && isOffTrack)
        {
            return true;
        }

        bool isCheckered = (f.SessionFlags & (uint)IRacingSdkEnum.Flags.Checkered) != 0;

        if (finalLapCompleted != -1 && !isCheckered)
        {
            finalLapCompleted = -1;
        }

        if (!isCheckered)
        {
            return false;
        }

        if (finalLapCompleted == -1)
        {
            finalLapCompleted = f.LapCompleted;
            return false;
        }

        return f.LapCompleted > finalLapCompleted;
    }
}
